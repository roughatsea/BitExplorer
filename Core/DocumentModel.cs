using System.Numerics;
using System.Text;
using System.Text.Json;

namespace BitExplorer.Core;

/// <summary>An editable binary and its interpretation. Physical bit indices run MSB first through the file.</summary>
/// <remarks>
/// A byte contains eight bits. A physical bit address is byteOffset * 8 plus a
/// position counted from the left, so address zero has weight 128 in byte zero.
/// This model owns edits, field metadata, history, and files; it knows nothing
/// about WPF windows. Call the edit methods instead of modifying exposed lists
/// or bytes directly, so validation, undo, and change notifications remain correct.
/// </remarks>
public sealed class DocumentModel
{
    /// <summary>The maximum binary size held in memory, in bytes: 256 MiB.</summary>
    public const int MaxBinaryBytes = 256 * 1024 * 1024;
    /// <summary>The maximum number of source bits in a single interpreted field.</summary>
    public const int MaxFieldBits = 4096;
    /// <summary>The maximum number of named field definitions in a document.</summary>
    public const int MaxFields = 4096;
    // Per-field limits alone could still permit a huge collection of bit lists.
    // This additional total bounds the combined size of all field definitions.
    private const int MaxTotalFieldBits = 1_048_576;
    // JSON is readable, accepts property-name casing differences, and has a
    // nesting-depth limit. The versioned DTOs near the bottom define its shape.
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true, MaxDepth = 32 };
    // A stack returns the most recently pushed item first, matching Undo/Redo.
    private readonly Stack<Edit> _undo = new();
    private readonly Stack<Edit> _redo = new();
    // Byte highlighting compares actual values against this copy. It is distinct
    // from the history revision numbers used by the document-level dirty flags.
    private byte[] _binaryBaseline;
    private DisplaySettings _savedSettings;
    // Each accepted edit gets a never-reused revision number. Undo restores the
    // prior numbers; Redo restores the later ones. Binary and metadata revisions
    // are separate because saving raw bytes cannot save labels or their notes.
    private long _nextRevision;
    private long _revision;
    private long _binaryRevision;
    private long _metadataRevision;
    private long _savedBinaryRevision;
    private long _savedProjectRevision;
    private long _savedMetadataRevision;

    /// <summary>The current bytes; callers should read here and write through SetByte, FlipBit, or SetFieldValue.</summary>
    public byte[] Data { get; private set; }
    /// <summary>The binary's source/last save path, if known; it is not necessarily the project path.</summary>
    public string? FilePath { get; private set; }
    /// <summary>The active project path after opening or saving a project.</summary>
    public string? ProjectPath { get; private set; }
    /// <summary>The current field definitions. Use the field-edit methods to change this collection.</summary>
    public List<NamedField> Fields { get; private set; } = [];
    /// <summary>Display preferences; call NotifySettingsChanged after assigning or updating them.</summary>
    public DisplaySettings Settings { get; set; } = new();
    /// <summary>Whether binary-edit history differs from the last binary save or initial loaded state.</summary>
    /// <remarks>This compares history positions, not every byte; manually typing old values is still an edit.</remarks>
    public bool IsBinaryDirty => _binaryRevision != _savedBinaryRevision;
    /// <summary>Whether edits or display preferences differ from the last saved/loaded project state.</summary>
    public bool IsProjectDirty => _revision != _savedProjectRevision || !SettingsEqual(Settings, _savedSettings);
    /// <summary>The unsaved-state flag for the active project, or for binary plus metadata when no project exists.</summary>
    // Saving a project can clear IsDirty while IsBinaryDirty remains true: the
    // edited bytes are in the project but were not written back as a binary.
    public bool IsDirty => ProjectPath != null ? IsProjectDirty :
        IsBinaryDirty || _metadataRevision != _savedMetadataRevision || !SettingsEqual(Settings, _savedSettings);
    /// <summary>Whether at least one applied edit can be reversed.</summary>
    public bool CanUndo => _undo.Count > 0;
    /// <summary>Whether a previously undone edit can be applied again.</summary>
    public bool CanRedo => _redo.Count > 0;
    /// <summary>Raised after edits, history changes, saves, or explicitly announced settings changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Creates a valid empty document.</summary>
    public DocumentModel() : this([]) { }

    /// <summary>Creates a document that owns independent copies of the supplied bytes.</summary>
    /// <param name="data">Initial contents; later changes to the caller's array do not affect this document.</param>
    /// <param name="filePath">Optional source metadata; this constructor does not open or read that path.</param>
    public DocumentModel(byte[] data, string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length > MaxBinaryBytes) throw new ArgumentException($"Files larger than {MaxBinaryBytes / 1024 / 1024} MiB are not supported.", nameof(data));
        // One copy is editable; the other records the starting values for the
        // modified-byte indicator. Neither aliases the caller's original array.
        Data = data.ToArray();
        _binaryBaseline = Data.ToArray();
        _savedSettings = Settings.Clone();
        FilePath = filePath;
    }

    /// <summary>Validates display settings and asks observers to refresh; settings are not undo commands.</summary>
    public void NotifySettingsChanged()
    {
        Settings.Validate();
        OnChanged();
    }

    /// <summary>Changes one byte as one undoable edit; writing its existing value creates no history entry.</summary>
    /// <param name="offset">A zero-based byte offset, not a physical bit address.</param>
    /// <param name="value">The replacement value, from 0 through 255.</param>
    public void SetByte(int offset, byte value)
    {
        ValidateOffset(offset);
        ApplyBytes(new Dictionary<int, byte> { [offset] = value });
    }

    /// <summary>Inverts exactly one physical source bit while preserving the other seven bits in its byte.</summary>
    public void FlipBit(long bit)
    {
        ValidateBit(bit);
        // Division finds the byte; remainder (%) finds its position from the left.
        // Shift a single 1 to that position to create a mask. XOR (^) flips the
        // bit where the mask is 1 and leaves all other bits alone. For physical
        // position zero, the mask is 1 << 7, or binary 10000000.
        int offset = (int)(bit / 8);
        SetByte(offset, (byte)(Data[offset] ^ (1 << (7 - (int)(bit % 8)))));
    }

    /// <summary>Assembles an unsigned number from a field's explicit source-bit order.</summary>
    /// <remarks>The field may be a temporary selection rather than a registered label. BigInteger supports widths beyond 64 bits.</remarks>
    public BigInteger ReadField(NamedField field)
    {
        ValidateField(field);
        BigInteger value = BigInteger.Zero;
        // Shift the result left to make room, then append the next bit with OR
        // (|). Shifting the source right and AND-ing with 1 extracts just that
        // bit. Appending the sequence 1, 0, 1 therefore produces binary 101.
        foreach (long bit in field.OrderedBits)
            value = (value << 1) | ((Data[(int)(bit / 8)] >> (7 - (int)(bit % 8))) & 1);
        return value;
    }

    /// <summary>Writes an unsigned field value as one undoable change, preserving all bits outside the field.</summary>
    /// <remarks>The first ordered source address receives the value's most significant bit.</remarks>
    public void SetFieldValue(NamedField field, BigInteger value)
    {
        ValidateField(field);
        // n unsigned bits hold 0 through (2^n - 1). Shifting 1 left by n gives
        // 2^n, the first value that does not fit. Validate the field first so an
        // untrusted bit count cannot request an unbounded BigInteger shift.
        if (value.Sign < 0 || value >= (BigInteger.One << field.OrderedBits.Count))
            throw new ArgumentOutOfRangeException(nameof(value), $"The value must fit in {field.OrderedBits.Count} unsigned bits.");
        var replacements = new Dictionary<int, byte>();
        for (int i = 0; i < field.OrderedBits.Count; i++)
        {
            long bit = field.OrderedBits[i];
            int offset = (int)(bit / 8);
            // Several field bits may share a byte. Reuse its pending value so
            // this iteration retains changes already made earlier in the loop.
            byte previous = replacements.GetValueOrDefault(offset, Data[offset]);
            int mask = 1 << (7 - (int)(bit % 8));
            // Read the desired value from high bit to low bit. OR sets the source
            // bit; AND with the inverted mask (~mask) clears it. The final byte
            // cast keeps the eight bits that belong to the destination byte.
            bool set = !((value >> (field.OrderedBits.Count - 1 - i)) & BigInteger.One).IsZero;
            replacements[offset] = (byte)(set ? previous | mask : previous & ~mask);
        }
        ApplyBytes(replacements);
    }

    /// <summary>Adds a validated, independently copied field in one undo step; IDs must be unique.</summary>
    public void AddField(NamedField field)
    {
        ValidateField(field);
        if (Fields.Any(f => f.Id == field.Id)) throw new ArgumentException("A field with this ID already exists.", nameof(field));
        var next = Fields.Select(f => f.Clone()).ToList();
        next.Add(field.Clone());
        ApplyFields(next);
    }

    /// <summary>Removes a label without changing any bytes; an unknown ID is a no-op.</summary>
    public void RemoveField(Guid id)
    {
        if (!Fields.Any(f => f.Id == id)) return;
        ApplyFields(Fields.Where(f => f.Id != id).Select(f => f.Clone()).ToList());
    }

    /// <summary>Replaces the matching field from an edited draft while preserving its original state for Undo.</summary>
    /// <remarks>Clone the live field before editing it. Mutating the live instance first would lose the original values.</remarks>
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

    /// <summary>Reverses the newest edit and moves it to the redo stack; does nothing if history is empty.</summary>
    public void Undo()
    {
        if (!_undo.TryPop(out Edit? edit)) return;
        edit.Revert();
        // Restore revision identities as well as values, so undoing back to a
        // saved state restores the appropriate clean/dirty indicators too.
        (_revision, _binaryRevision, _metadataRevision) = edit.Before;
        _redo.Push(edit);
        OnChanged();
    }

    /// <summary>Reapplies the newest undone edit and restores its later revision identities.</summary>
    public void Redo()
    {
        if (!_redo.TryPop(out Edit? edit)) return;
        edit.Apply();
        (_revision, _binaryRevision, _metadataRevision) = edit.After;
        _undo.Push(edit);
        OnChanged();
    }

    /// <summary>Compares a byte with its value on open or at the most recent binary save.</summary>
    /// <remarks>Unlike IsBinaryDirty, this checks actual bytes, so manually restoring a byte removes its highlight.</remarks>
    public bool IsByteModified(int offset)
    {
        ValidateOffset(offset);
        return Data[offset] != _binaryBaseline[offset];
    }

    /// <summary>Checks file size, reads its bytes, and returns a fresh document with empty undo history.</summary>
    public static DocumentModel OpenBinary(string path)
    {
        ValidateFileLength(path, MaxBinaryBytes, "binary");
        return new DocumentModel(File.ReadAllBytes(path), Path.GetFullPath(path));
    }

    /// <summary>Writes only raw bytes, then updates the binary baseline if the write succeeds.</summary>
    /// <remarks>Labels and display settings require a project save. Existing undo history is retained.</remarks>
    public void SaveBinary(string path)
    {
        AtomicWrite(path, stream => stream.Write(Data));
        FilePath = Path.GetFullPath(path);
        _binaryBaseline = Data.ToArray();
        _savedBinaryRevision = _binaryRevision;
        OnChanged();
    }

    /// <summary>Saves edited bytes, display settings, and field definitions together in versioned JSON.</summary>
    public void SaveProject(string path)
    {
        ValidateDocument();
        var project = new ProjectFile
        {
            Version = 1, Data = Data, SourcePath = FilePath,
            Settings = Settings.Clone(), Fields = Fields.Select(f => f.Clone()).ToList()
        };
        // System.Text.Json represents byte[] as base64 in JSON. The stored source
        // path is descriptive; the project contains its own complete data copy.
        AtomicWrite(path, stream => JsonSerializer.Serialize(stream, project, JsonOptions));
        ProjectPath = Path.GetFullPath(path);
        _savedProjectRevision = _revision;
        _savedMetadataRevision = _metadataRevision;
        _savedSettings = Settings.Clone();
        OnChanged();
    }

    /// <summary>Reads and validates a self-contained project without reading its stored source path.</summary>
    /// <remarks>Unsupported versions and invalid fields/settings are rejected before a document is returned.</remarks>
    public static DocumentModel OpenProject(string path)
    {
        // JSON/base64 takes more space than raw bytes, hence this larger file
        // limit. The constructor still applies the separate binary-data limit.
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

    /// <summary>Saves just the reusable field layout, without binary data or display settings.</summary>
    public void ExportTemplate(string path)
    {
        ValidateDocument();
        var template = new TemplateFile { Version = 1, Fields = Fields.Select(f => f.Clone()).ToList() };
        AtomicWrite(path, stream => JsonSerializer.Serialize(stream, template, JsonOptions));
    }

    /// <summary>Replaces the current field layout in one undoable operation; the binary is untouched.</summary>
    /// <remarks>Every source bit must fit this document. Validation finishes before the current layout changes.</remarks>
    public void ImportTemplate(string path)
    {
        TemplateFile template = ReadJson<TemplateFile>(path, 64L * 1024 * 1024);
        if (template.Version != 1) throw new InvalidDataException("Unsupported field template version. Expected version 1.");
        if (template.Fields == null) throw new InvalidDataException("The template is missing its field layout.");
        try { ApplyFields(template.Fields); }
        catch (ArgumentException ex) { throw new InvalidDataException("The template has invalid fields or refers to bits outside this file.", ex); }
    }

    /// <summary>Builds a deterministic example packet with readable text and several kinds of packed fields.</summary>
    /// <remarks>The demo is initial content, not a series of user edits, so it starts with empty undo history.</remarks>
    public static DocumentModel CreateDemo()
    {
        var bytes = new byte[512];
        bytes[0] = 0xB3; bytes[1] = 0x6C; bytes[2] = 0xA1; bytes[3] = 0x48;
        bytes[4] = 0x02; bytes[5] = 0x00; bytes[6] = 0x7D; bytes[7] = 0x9A;
        Encoding.ASCII.GetBytes("BITEXPLORER / CUSTOM TELEMETRY\0").CopyTo(bytes, 16);
        // Populate reproducible payload bytes. AND 255 keeps the lowest eight
        // bits of each generated value; this is sample data, not encryption.
        for (int i = 64; i < bytes.Length; i++) bytes[i] = (byte)((i * 37 + (i >> 3) * 11) & 255);
        for (int i = 0; i < 8; i++)
        {
            int offset = 64 + i * 32;
            bytes[offset] = (byte)(0x80 | i);
            bytes[offset + 1] = (byte)(20 + i * 3);
            Encoding.ASCII.GetBytes($"SENSOR-{i + 1:00}").CopyTo(bytes, offset + 8);
        }
        var document = new DocumentModel(bytes);
        // The payload length illustrates little-endian interpretation: byte 5's
        // bits precede byte 4's bits in the numeric value, without rearranging Data.
        document.Fields =
        [
            new NamedField { Name = "Version", Color = "#6EABFF", Notes = "Header version, first three physical bits.", OrderedBits = [0, 1, 2] },
            new NamedField { Name = "Enabled", Color = "#54BFA6", Notes = "Single-bit enable flag; click its bit in the inspector to flip it.", OrderedBits = [3] },
            new NamedField { Name = "Message type", Color = "#D9AD68", Notes = "Six-bit packed field spanning the first two bytes.", OrderedBits = [5, 6, 7, 8, 9, 10] },
            new NamedField { Name = "Payload length", Color = "#CB98EE", Notes = "Little-endian 16-bit length: byte 5 supplies the high eight value bits, followed by byte 4.", OrderedBits = Enumerable.Range(40, 8).Concat(Enumerable.Range(32, 8)).Select(i => (long)i).ToList() }
        ];
        return document;
    }

    /// <summary>Turns changed byte values into a compact before/after patch and records one history item.</summary>
    // Store only changed bytes: a one-bit edit should not copy the entire file
    // into history. If every supplied value is unchanged, there is nothing to undo.
    private void ApplyBytes(Dictionary<int, byte> replacements)
    {
        var changes = replacements.Where(pair => Data[pair.Key] != pair.Value)
            .Select(pair => (Offset: pair.Key, Before: Data[pair.Key], After: pair.Value)).ToArray();
        if (changes.Length == 0) return;
        Execute(() => { foreach (var change in changes) Data[change.Offset] = change.After; },
            () => { foreach (var change in changes) Data[change.Offset] = change.Before; }, binary: true);
    }

    /// <summary>Validates a candidate layout, then records independent before/after metadata snapshots.</summary>
    private void ApplyFields(List<NamedField> fields)
    {
        ValidateFields(fields);
        if (Fields.Count == fields.Count && Fields.Zip(fields).All(pair => FieldsEqual(pair.First, pair.Second))) return;
        var before = Fields.Select(f => f.Clone()).ToList();
        var after = fields.Select(f => f.Clone()).ToList();
        // Clone again on each history application. Otherwise modifying a restored
        // field could mutate the snapshot needed for a later Undo or Redo.
        Execute(() => Fields = after.Select(f => f.Clone()).ToList(),
            () => Fields = before.Select(f => f.Clone()).ToList(), binary: false);
    }

    /// <summary>Runs an edit, assigns revision identities, and starts a new branch of undo history.</summary>
    /// <param name="apply">An action that installs the edited state.</param>
    /// <param name="revert">An action that reinstates the original state.</param>
    /// <param name="binary">True for byte changes, false for field metadata changes.</param>
    private void Execute(Action apply, Action revert, bool binary)
    {
        long next = ++_nextRevision;
        var edit = new Edit(apply, revert, (_revision, _binaryRevision, _metadataRevision),
            (next, binary ? next : _binaryRevision, binary ? _metadataRevision : next));
        edit.Apply();
        (_revision, _binaryRevision, _metadataRevision) = edit.After;
        _undo.Push(edit);
        // Undo followed by a new edit discards the old redo path. Its revision
        // numbers are never reused, even if the new edit happens to look similar.
        _redo.Clear();
        OnChanged();
    }

    /// <summary>Checks a byte address before it can be used as an array index.</summary>
    private void ValidateOffset(int offset)
    {
        if (offset < 0 || offset >= Data.Length) throw new ArgumentOutOfRangeException(nameof(offset), "The byte offset is outside this file.");
    }

    /// <summary>Checks a physical bit address against eight times the byte count.</summary>
    private void ValidateBit(long bit)
    {
        if (bit < 0 || bit >= Data.LongLength * 8) throw new ArgumentOutOfRangeException(nameof(bit), "The physical bit address is outside this file.");
    }

    /// <summary>Checks identity, readable metadata, a bounded width, and valid distinct source bits.</summary>
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
        // One source bit cannot appear twice in a field: it would then have two
        // numeric roles and writes would be ambiguous. Different fields may overlap.
        var seen = new HashSet<long>();
        foreach (long bit in field.OrderedBits)
        {
            ValidateBit(bit);
            if (!seen.Add(bit)) throw new ArgumentException("A physical bit may only appear once within a field.", nameof(field));
        }
    }

    /// <summary>Checks the entire layout, including unique IDs and the total bit-reference count.</summary>
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

    /// <summary>Checks settings and field definitions before project/template persistence.</summary>
    private void ValidateDocument()
    {
        ArgumentNullException.ThrowIfNull(Settings);
        Settings.Validate();
        ValidateFields(Fields);
    }

    /// <summary>Compares field contents, including source order rather than merely membership.</summary>
    private static bool FieldsEqual(NamedField left, NamedField right) => left.Id == right.Id && left.Name == right.Name &&
        left.Color == right.Color && left.Notes == right.Notes && left.OrderedBits.SequenceEqual(right.OrderedBits);

    /// <summary>Compares user-controlled display choices for dirty-state tracking.</summary>
    // CharacterWidth is intentionally omitted: measuring the current font on a
    // different screen should not, by itself, mark an opened project as edited.
    private static bool SettingsEqual(DisplaySettings a, DisplaySettings b) => a.ViewMode == b.ViewMode && a.ByteBase == b.ByteBase &&
        a.OffsetBase == b.OffsetBase && a.BitNumbering == b.BitNumbering && a.BytesPerRow == b.BytesPerRow &&
        a.AutoBytesPerRow == b.AutoBytesPerRow && a.ShowOffsets == b.ShowOffsets && a.ShowAscii == b.ShowAscii &&
        a.ShowRuler == b.ShowRuler && a.ShowAnnotations == b.ShowAnnotations && a.OffsetWidth == b.OffsetWidth &&
        a.DataWidth == b.DataWidth && a.AsciiWidth == b.AsciiWidth;

    /// <summary>Reads bounded JSON as the requested storage type and translates parser errors into file errors.</summary>
    /// <typeparam name="T">The expected stored shape, such as ProjectFile or TemplateFile.</typeparam>
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

    /// <summary>Rejects missing or oversized files before allocating space to read their contents.</summary>
    private static void ValidateFileLength(string path, long maxBytes, string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("The selected file does not exist.", path);
        if (info.Length > maxBytes) throw new InvalidDataException($"The {kind} file is too large (maximum {maxBytes / 1024 / 1024} MiB).");
    }

    /// <summary>Writes a complete temporary file before replacing or creating the destination.</summary>
    /// <param name="path">The destination; its parent directory must already exist.</param>
    /// <param name="write">The byte-writing or JSON-serialization operation for the temporary stream.</param>
    /// <remarks>I/O errors propagate; callers update saved-state baselines only after this method succeeds.</remarks>
    private static void AtomicWrite(string path, Action<FileStream> write)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath)!;
        // The same directory keeps the final rename/replace on the same volume.
        // A random name plus CreateNew avoids reusing an existing temporary file.
        string temporary = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                write(stream);
                // Flush before closing and publishing the file. The existing
                // destination has not been touched while the bytes are written.
                stream.Flush(flushToDisk: true);
            }
            // Replace avoids exposing a partially written existing file. A first
            // save uses Move because there is no destination to replace yet.
            if (File.Exists(fullPath)) File.Replace(temporary, fullPath, null);
            else File.Move(temporary, fullPath);
        }
        finally
        {
            // A successful move/replace consumes the temporary path. If writing
            // failed, clean up that temporary file rather than leaving it behind.
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    /// <summary>Notifies subscribers when present; ?. also handles an event with no listeners.</summary>
    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>A reversible operation and the history identities on either side of it.</summary>
    // Action delegates capture patches/snapshots. Each tuple packages the overall,
    // binary, and metadata revisions so all three can be restored together.
    private sealed record Edit(Action Apply, Action Revert,
        (long Revision, long Binary, long Metadata) Before, (long Revision, long Binary, long Metadata) After);

    /// <summary>The JSON storage shape, separate from the live model's events and undo history.</summary>
    // DTO means data transfer object: only the values intended for persistence.
    private sealed class ProjectFile
    {
        /// <summary>Storage-format version, checked before accepting a loaded project.</summary>
        public int Version { get; set; }
        /// <summary>Embedded bytes; null lets validation detect a missing JSON property.</summary>
        public byte[]? Data { get; set; }
        /// <summary>Informational binary path; loading a project does not read it.</summary>
        public string? SourcePath { get; set; }
        /// <summary>The saved grid and export preferences.</summary>
        public DisplaySettings? Settings { get; set; }
        /// <summary>The saved field definitions and their explicit source order.</summary>
        public List<NamedField>? Fields { get; set; }
    }

    /// <summary>A smaller JSON shape for reusing a field layout with another compatible binary.</summary>
    private sealed class TemplateFile
    {
        /// <summary>The template-format version, independent of binary contents.</summary>
        public int Version { get; set; }
        /// <summary>The source-bit mappings; no binary data is stored in a template.</summary>
        public List<NamedField>? Fields { get; set; }
    }
}

/// <summary>An allocation-free helper for validating the digits in an RGB color.</summary>
internal static class HexValidation
{
    /// <summary>Returns true only when every character is an ASCII hexadecimal digit.</summary>
    /// <remarks>A span views existing characters, avoiding a new string allocation for the substring.</remarks>
    public static bool ContainsOnlyHex(this ReadOnlySpan<char> value)
    {
        foreach (char c in value) if (!char.IsAsciiHexDigit(c)) return false;
        return true;
    }
}
