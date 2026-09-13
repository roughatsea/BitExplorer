using System.Numerics;
using System.Text;
using System.Text.Json;

namespace BitExplorer.Core;

/// <summary>An editable binary and its interpretation. Physical bit indices run MSB first through the file.</summary>
public sealed class DocumentModel
{
    public const int MaxBinaryBytes = 256 * 1024 * 1024;
    public const int MaxFieldBits = 4096;
    public const int MaxFields = 4096;
    private const int MaxTotalFieldBits = 1_048_576;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true, MaxDepth = 32 };
    private readonly Stack<Edit> _undo = new();
    private readonly Stack<Edit> _redo = new();
    private byte[] _binaryBaseline;
    private DisplaySettings _savedSettings;
    private long _nextRevision;
    private long _revision;
    private long _binaryRevision;
    private long _metadataRevision;
    private long _savedBinaryRevision;
    private long _savedProjectRevision;
    private long _savedMetadataRevision;

    public byte[] Data { get; private set; }
    public string? FilePath { get; private set; }
    public string? ProjectPath { get; private set; }
    public List<NamedField> Fields { get; private set; } = [];
    public DisplaySettings Settings { get; set; } = new();
    public bool IsBinaryDirty => _binaryRevision != _savedBinaryRevision;
    public bool IsProjectDirty => _revision != _savedProjectRevision || !SettingsEqual(Settings, _savedSettings);
    public bool IsDirty => ProjectPath != null ? IsProjectDirty :
        IsBinaryDirty || _metadataRevision != _savedMetadataRevision || !SettingsEqual(Settings, _savedSettings);
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public event EventHandler? Changed;

    public DocumentModel() : this([]) { }

    public DocumentModel(byte[] data, string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length > MaxBinaryBytes) throw new ArgumentException($"Files larger than {MaxBinaryBytes / 1024 / 1024} MiB are not supported.", nameof(data));
        Data = data.ToArray();
        _binaryBaseline = Data.ToArray();
        _savedSettings = Settings.Clone();
        FilePath = filePath;
    }

    public void NotifySettingsChanged()
    {
        Settings.Validate();
        OnChanged();
    }

    public void SetByte(int offset, byte value)
    {
        ValidateOffset(offset);
        ApplyBytes(new Dictionary<int, byte> { [offset] = value });
    }

    public void FlipBit(long bit)
    {
        ValidateBit(bit);
        int offset = (int)(bit / 8);
        SetByte(offset, (byte)(Data[offset] ^ (1 << (7 - (int)(bit % 8)))));
    }

    public BigInteger ReadField(NamedField field)
    {
        ValidateField(field);
        BigInteger value = BigInteger.Zero;
        foreach (long bit in field.OrderedBits)
            value = (value << 1) | ((Data[(int)(bit / 8)] >> (7 - (int)(bit % 8))) & 1);
        return value;
    }

    public void SetFieldValue(NamedField field, BigInteger value)
    {
        ValidateField(field);
        if (value.Sign < 0 || value >= (BigInteger.One << field.OrderedBits.Count))
            throw new ArgumentOutOfRangeException(nameof(value), $"The value must fit in {field.OrderedBits.Count} unsigned bits.");
        var replacements = new Dictionary<int, byte>();
        for (int i = 0; i < field.OrderedBits.Count; i++)
        {
            long bit = field.OrderedBits[i];
            int offset = (int)(bit / 8);
            byte previous = replacements.GetValueOrDefault(offset, Data[offset]);
            int mask = 1 << (7 - (int)(bit % 8));
            bool set = !((value >> (field.OrderedBits.Count - 1 - i)) & BigInteger.One).IsZero;
            replacements[offset] = (byte)(set ? previous | mask : previous & ~mask);
        }
        ApplyBytes(replacements);
    }

    public void AddField(NamedField field)
    {
        ValidateField(field);
        if (Fields.Any(f => f.Id == field.Id)) throw new ArgumentException("A field with this ID already exists.", nameof(field));
        var next = Fields.Select(f => f.Clone()).ToList();
        next.Add(field.Clone());
        ApplyFields(next);
    }

    public void RemoveField(Guid id)
    {
        if (!Fields.Any(f => f.Id == id)) return;
        ApplyFields(Fields.Where(f => f.Id != id).Select(f => f.Clone()).ToList());
    }

    public void UpdateField(NamedField field)
    {
        ValidateField(field);
        int index = Fields.FindIndex(f => f.Id == field.Id);
        if (index < 0) throw new ArgumentException("The field no longer exists in this document.", nameof(field));
        if (FieldsEqual(Fields[index], field)) return;
        var next = Fields.Select(f => f.Clone()).ToList();
        next[index] = field.Clone();
        ApplyFields(next);
    }

    public void Undo()
    {
        if (!_undo.TryPop(out Edit? edit)) return;
        edit.Revert();
        (_revision, _binaryRevision, _metadataRevision) = edit.Before;
        _redo.Push(edit);
        OnChanged();
    }

    public void Redo()
    {
        if (!_redo.TryPop(out Edit? edit)) return;
        edit.Apply();
        (_revision, _binaryRevision, _metadataRevision) = edit.After;
        _undo.Push(edit);
        OnChanged();
    }

    public bool IsByteModified(int offset)
    {
        ValidateOffset(offset);
        return Data[offset] != _binaryBaseline[offset];
    }

    public static DocumentModel OpenBinary(string path)
    {
        ValidateFileLength(path, MaxBinaryBytes, "binary");
        return new DocumentModel(File.ReadAllBytes(path), Path.GetFullPath(path));
    }

    public void SaveBinary(string path)
    {
        AtomicWrite(path, stream => stream.Write(Data));
        FilePath = Path.GetFullPath(path);
        _binaryBaseline = Data.ToArray();
        _savedBinaryRevision = _binaryRevision;
        OnChanged();
    }

    public void SaveProject(string path)
    {
        ValidateDocument();
        var project = new ProjectFile
        {
            Version = 1, Data = Data, SourcePath = FilePath,
            Settings = Settings.Clone(), Fields = Fields.Select(f => f.Clone()).ToList()
        };
        AtomicWrite(path, stream => JsonSerializer.Serialize(stream, project, JsonOptions));
        ProjectPath = Path.GetFullPath(path);
        _savedProjectRevision = _revision;
        _savedMetadataRevision = _metadataRevision;
        _savedSettings = Settings.Clone();
        OnChanged();
    }

    public static DocumentModel OpenProject(string path)
    {
        ProjectFile project = ReadJson<ProjectFile>(path, 512L * 1024 * 1024);
        if (project.Version != 1) throw new InvalidDataException("Unsupported project version. Expected version 1.");
        if (project.Data == null || project.Settings == null || project.Fields == null)
            throw new InvalidDataException("The project is missing data, display settings, or fields.");
        try
        {
            var document = new DocumentModel(project.Data, project.SourcePath)
            {
                Settings = project.Settings,
                Fields = project.Fields,
                ProjectPath = Path.GetFullPath(path)
            };
            document.ValidateDocument();
            document._savedSettings = document.Settings.Clone();
            return document;
        }
        catch (ArgumentException ex) { throw new InvalidDataException("The project contains invalid binary data or field definitions.", ex); }
    }

    public void ExportTemplate(string path)
    {
        ValidateDocument();
        var template = new TemplateFile { Version = 1, Fields = Fields.Select(f => f.Clone()).ToList() };
        AtomicWrite(path, stream => JsonSerializer.Serialize(stream, template, JsonOptions));
    }

    /// <summary>Replaces the current field layout in one undoable operation; the binary is untouched.</summary>
    public void ImportTemplate(string path)
    {
        TemplateFile template = ReadJson<TemplateFile>(path, 64L * 1024 * 1024);
        if (template.Version != 1) throw new InvalidDataException("Unsupported field template version. Expected version 1.");
        if (template.Fields == null) throw new InvalidDataException("The template is missing its field layout.");
        try { ApplyFields(template.Fields); }
        catch (ArgumentException ex) { throw new InvalidDataException("The template has invalid fields or refers to bits outside this file.", ex); }
    }

    public static DocumentModel CreateDemo()
    {
        var bytes = new byte[512];
        bytes[0] = 0xB3; bytes[1] = 0x6C; bytes[2] = 0xA1; bytes[3] = 0x48;
        bytes[4] = 0x02; bytes[5] = 0x00; bytes[6] = 0x7D; bytes[7] = 0x9A;
        Encoding.ASCII.GetBytes("BITEXPLORER / CUSTOM TELEMETRY\0").CopyTo(bytes, 16);
        for (int i = 64; i < bytes.Length; i++) bytes[i] = (byte)((i * 37 + (i >> 3) * 11) & 255);
        for (int i = 0; i < 8; i++)
        {
            int offset = 64 + i * 32;
            bytes[offset] = (byte)(0x80 | i);
            bytes[offset + 1] = (byte)(20 + i * 3);
            Encoding.ASCII.GetBytes($"SENSOR-{i + 1:00}").CopyTo(bytes, offset + 8);
        }
        var document = new DocumentModel(bytes);
        document.Fields =
        [
            new NamedField { Name = "Version", Color = "#6EABFF", Notes = "Header version, first three physical bits.", OrderedBits = [0, 1, 2] },
            new NamedField { Name = "Enabled", Color = "#54BFA6", Notes = "Single-bit enable flag; click its bit in the inspector to flip it.", OrderedBits = [3] },
            new NamedField { Name = "Message type", Color = "#D9AD68", Notes = "Six-bit packed field spanning the first two bytes.", OrderedBits = [5, 6, 7, 8, 9, 10] },
            new NamedField { Name = "Payload length", Color = "#CB98EE", Notes = "Little-endian 16-bit length: byte 5 supplies the high eight value bits, followed by byte 4.", OrderedBits = Enumerable.Range(40, 8).Concat(Enumerable.Range(32, 8)).Select(i => (long)i).ToList() }
        ];
        return document;
    }

    private void ApplyBytes(Dictionary<int, byte> replacements)
    {
        var changes = replacements.Where(pair => Data[pair.Key] != pair.Value)
            .Select(pair => (Offset: pair.Key, Before: Data[pair.Key], After: pair.Value)).ToArray();
        if (changes.Length == 0) return;
        Execute(() => { foreach (var change in changes) Data[change.Offset] = change.After; },
            () => { foreach (var change in changes) Data[change.Offset] = change.Before; }, binary: true);
    }

    private void ApplyFields(List<NamedField> fields)
    {
        ValidateFields(fields);
        if (Fields.Count == fields.Count && Fields.Zip(fields).All(pair => FieldsEqual(pair.First, pair.Second))) return;
        var before = Fields.Select(f => f.Clone()).ToList();
        var after = fields.Select(f => f.Clone()).ToList();
        Execute(() => Fields = after.Select(f => f.Clone()).ToList(),
            () => Fields = before.Select(f => f.Clone()).ToList(), binary: false);
    }

    private void Execute(Action apply, Action revert, bool binary)
    {
        long next = ++_nextRevision;
        var edit = new Edit(apply, revert, (_revision, _binaryRevision, _metadataRevision),
            (next, binary ? next : _binaryRevision, binary ? _metadataRevision : next));
        edit.Apply();
        (_revision, _binaryRevision, _metadataRevision) = edit.After;
        _undo.Push(edit);
        _redo.Clear();
        OnChanged();
    }

    private void ValidateOffset(int offset)
    {
        if (offset < 0 || offset >= Data.Length) throw new ArgumentOutOfRangeException(nameof(offset), "The byte offset is outside this file.");
    }

    private void ValidateBit(long bit)
    {
        if (bit < 0 || bit >= Data.LongLength * 8) throw new ArgumentOutOfRangeException(nameof(bit), "The physical bit address is outside this file.");
    }

    private void ValidateField(NamedField field)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (field.Id == Guid.Empty) throw new ArgumentException("A field must have a nonempty ID.", nameof(field));
        if (string.IsNullOrWhiteSpace(field.Name) || field.Name.Length > 256 || field.Name.Any(char.IsControl))
            throw new ArgumentException("A field name must contain 1–256 characters and no control characters.", nameof(field));
        if (field.Notes == null || field.Notes.Length > 65536) throw new ArgumentException("Field notes must contain at most 65,536 characters.", nameof(field));
        if (field.Color == null || field.Color.Length != 7 || field.Color[0] != '#' || !field.Color.AsSpan(1).ContainsOnlyHex())
            throw new ArgumentException("A field color must use #RRGGBB hexadecimal notation.", nameof(field));
        if (field.OrderedBits == null || field.OrderedBits.Count is < 1 or > MaxFieldBits)
            throw new ArgumentException($"A field must contain 1–{MaxFieldBits} bits.", nameof(field));
        var seen = new HashSet<long>();
        foreach (long bit in field.OrderedBits)
        {
            ValidateBit(bit);
            if (!seen.Add(bit)) throw new ArgumentException("A physical bit may only appear once within a field.", nameof(field));
        }
    }

    private void ValidateFields(List<NamedField> fields)
    {
        if (fields.Count > MaxFields) throw new ArgumentException($"At most {MaxFields} named fields are supported.", nameof(fields));
        long total = 0;
        var ids = new HashSet<Guid>();
        foreach (NamedField field in fields)
        {
            ValidateField(field);
            if (!ids.Add(field.Id)) throw new ArgumentException("Field IDs must be unique.", nameof(fields));
            total += field.OrderedBits.Count;
            if (total > MaxTotalFieldBits) throw new ArgumentException("The field layout contains too many bit references.", nameof(fields));
        }
    }

    private void ValidateDocument()
    {
        ArgumentNullException.ThrowIfNull(Settings);
        Settings.Validate();
        ValidateFields(Fields);
    }

    private static bool FieldsEqual(NamedField left, NamedField right) => left.Id == right.Id && left.Name == right.Name &&
        left.Color == right.Color && left.Notes == right.Notes && left.OrderedBits.SequenceEqual(right.OrderedBits);

    private static bool SettingsEqual(DisplaySettings a, DisplaySettings b) => a.ViewMode == b.ViewMode && a.ByteBase == b.ByteBase &&
        a.OffsetBase == b.OffsetBase && a.BitNumbering == b.BitNumbering && a.BytesPerRow == b.BytesPerRow &&
        a.AutoBytesPerRow == b.AutoBytesPerRow && a.ShowOffsets == b.ShowOffsets && a.ShowAscii == b.ShowAscii &&
        a.ShowRuler == b.ShowRuler && a.ShowAnnotations == b.ShowAnnotations && a.OffsetWidth == b.OffsetWidth &&
        a.DataWidth == b.DataWidth && a.AsciiWidth == b.AsciiWidth;

    private static T ReadJson<T>(string path, long maxBytes)
    {
        ValidateFileLength(path, maxBytes, "JSON");
        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<T>(stream, JsonOptions) ?? throw new InvalidDataException("The JSON file is empty.");
        }
        catch (JsonException ex) { throw new InvalidDataException("The file does not contain a valid BitExplorer JSON document.", ex); }
    }

    private static void ValidateFileLength(string path, long maxBytes, string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("The selected file does not exist.", path);
        if (info.Length > maxBytes) throw new InvalidDataException($"The {kind} file is too large (maximum {maxBytes / 1024 / 1024} MiB).");
    }

    private static void AtomicWrite(string path, Action<FileStream> write)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath)!;
        string temporary = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                write(stream);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(fullPath)) File.Replace(temporary, fullPath, null);
            else File.Move(temporary, fullPath);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private sealed record Edit(Action Apply, Action Revert,
        (long Revision, long Binary, long Metadata) Before, (long Revision, long Binary, long Metadata) After);

    private sealed class ProjectFile
    {
        public int Version { get; set; }
        public byte[]? Data { get; set; }
        public string? SourcePath { get; set; }
        public DisplaySettings? Settings { get; set; }
        public List<NamedField>? Fields { get; set; }
    }

    private sealed class TemplateFile
    {
        public int Version { get; set; }
        public List<NamedField>? Fields { get; set; }
    }
}

internal static class HexValidation
{
    public static bool ContainsOnlyHex(this ReadOnlySpan<char> value)
    {
        foreach (char c in value) if (!char.IsAsciiHexDigit(c)) return false;
        return true;
    }
}
