// READING THIS FILE
// A program is a set of instructions. This file defines some of those instructions;
// defining a method does not run it. A call such as Refresh() asks it to run.
// Comments explain the next instruction or the whole block introduced below them.
// Within a running block, instructions normally run from top to bottom. Braces { }
// group a body; a closing brace ends that group. Blank lines only separate ideas.
// A semicolon ends an instruction. A long instruction can continue on several lines;
// its commas, closing parentheses and braces belong to the explanation at its start.
// Names identify values or operations: x = y stores y in x; x == y compares them.
// A dot selects something belonging to an object, and (...) supplies inputs to a call.
// See docs/ReadingTheCode.md for types, symbols, examples, and the application map.

// Make names from System.Numerics available here without writing their full prefix each time. This does not
// run that library's code.
using System.Numerics;
// Make names from System.Text available here without writing their full prefix each time. This does not run
// that library's code.
using System.Text;
// Make names from System.Text.Json available here without writing their full prefix each time. This does
// not run that library's code.
using System.Text.Json;

// Place this file's definitions in the BitExplorer.Core naming group, which prevents clashes with names in
// other groups.
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
    // Remember a new object of the required type as _redo.
    private readonly Stack<Edit> _redo = new();
    // Byte highlighting compares actual values against this copy. It is distinct
    // from the history revision numbers used by the document-level dirty flags.
    private byte[] _binaryBaseline;
    // Reserve _savedSettings to hold an object or value of type DisplaySettings; setup can supply its
    // value, otherwise the type's default is used.
    private DisplaySettings _savedSettings;
    // Each accepted edit gets a never-reused revision number. Undo restores the
    // prior numbers; Redo restores the later ones. Binary and metadata revisions
    // are separate because saving raw bytes cannot save labels or their notes.
    private long _nextRevision;
    // Reserve _revision to hold a whole number with room for larger values; setup can supply its value,
    // otherwise the type's default is used.
    private long _revision;
    // Reserve _binaryRevision to hold a whole number with room for larger values; setup can supply its
    // value, otherwise the type's default is used.
    private long _binaryRevision;
    // Reserve _metadataRevision to hold a whole number with room for larger values; setup can supply its
    // value, otherwise the type's default is used.
    private long _metadataRevision;
    // Reserve _savedBinaryRevision to hold a whole number with room for larger values; setup can supply its
    // value, otherwise the type's default is used.
    private long _savedBinaryRevision;
    // Reserve _savedProjectRevision to hold a whole number with room for larger values; setup can supply
    // its value, otherwise the type's default is used.
    private long _savedProjectRevision;
    // Reserve _savedMetadataRevision to hold a whole number with room for larger values; setup can supply
    // its value, otherwise the type's default is used.
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
        // Reject data immediately if it is null (no object was supplied).
        ArgumentNullException.ThrowIfNull(data);
        // If data.Length is greater than MaxBinaryBytes, stop this normal path by reporting
        // ArgumentException; the arguments carry the error details.
        if (data.Length > MaxBinaryBytes) throw new ArgumentException($"Files larger than {MaxBinaryBytes / 1024 / 1024} MiB are not supported.", nameof(data));
        // One copy is editable; the other records the starting values for the
        // modified-byte indicator. Neither aliases the caller's original array.
        Data = data.ToArray();
        // Set _binaryBaseline to a separate array containing Data's items.
        _binaryBaseline = Data.ToArray();
        // Set _savedSettings to a copy returned by Settings.Clone().
        _savedSettings = Settings.Clone();
        // Set FilePath to filePath.
        FilePath = filePath;
    }

    /// <summary>Validates display settings and asks observers to refresh; settings are not undo commands.</summary>
    public void NotifySettingsChanged()
    {
        // Run Settings.Validate to reject invalid input before proceeding.
        Settings.Validate();
        // Announce that the document changed so its listeners can refresh their state.
        OnChanged();
    }

    /// <summary>Changes one byte as one undoable edit; writing its existing value creates no history entry.</summary>
    /// <param name="offset">A zero-based byte offset, not a physical bit address.</param>
    /// <param name="value">The replacement value, from 0 through 255.</param>
    public void SetByte(int offset, byte value)
    {
        // Run ValidateOffset to reject invalid input before proceeding.
        ValidateOffset(offset);
        // Call ApplyBytes: Turns changed byte values into a compact before/after patch and records one
        // history item.
        ApplyBytes(new Dictionary<int, byte> { [offset] = value });
    }

    /// <summary>Inverts exactly one physical source bit while preserving the other seven bits in its byte.</summary>
    public void FlipBit(long bit)
    {
        // Run ValidateBit to reject invalid input before proceeding.
        ValidateBit(bit);
        // Division finds the byte; remainder (%) finds its position from the left.
        // Shift a single 1 to that position to create a mask. XOR (^) flips the
        // bit where the mask is 1 and leaves all other bits alone. For physical
        // position zero, the mask is 1 << 7, or binary 10000000.
        int offset = (int)(bit / 8);
        // For example, bit address 2 targets byte 0 with mask 00100000. If that
        // byte is B3 (10110011), XOR changes it to 93 (10010011). SetByte records
        // the change so Undo can restore B3. The cast makes the result a byte again.
        SetByte(offset, (byte)(Data[offset] ^ (1 << (7 - (int)(bit % 8)))));
    }

    /// <summary>Assembles an unsigned number from a field's explicit source-bit order.</summary>
    /// <remarks>The field may be a temporary selection rather than a registered label. BigInteger supports widths beyond 64 bits.</remarks>
    public BigInteger ReadField(NamedField field)
    {
        // Run ValidateField to reject invalid input before proceeding.
        ValidateField(field);
        // Start the number being assembled at zero. BigInteger can grow large
        // enough to represent a field containing thousands of bits.
        BigInteger value = BigInteger.Zero;
        // Shift the result left to make room, then append the next bit with OR
        // (|). Shifting the source right and AND-ing with 1 extracts just that
        // bit. Appending the sequence 1, 0, 1 therefore produces binary 101.
        foreach (long bit in field.OrderedBits)
            // Read the byte at bit / 8. Move its desired bit to the rightmost
            // position, then & 1 discards every other bit. Shift the growing
            // result left once and attach this next 0 or 1 at its right edge.
            value = (value << 1) | ((Data[(int)(bit / 8)] >> (7 - (int)(bit % 8))) & 1);
        // Return value to the caller and leave this method.
        return value;
    }

    /// <summary>Writes an unsigned field value as one undoable change, preserving all bits outside the field.</summary>
    /// <remarks>The first ordered source address receives the value's most significant bit.</remarks>
    public void SetFieldValue(NamedField field, BigInteger value)
    {
        // Run ValidateField to reject invalid input before proceeding.
        ValidateField(field);
        // n unsigned bits hold 0 through (2^n - 1). Shifting 1 left by n gives
        // 2^n, the first value that does not fit. Validate the field first so an
        // untrusted bit count cannot request an unbounded BigInteger shift.
        if (value.Sign < 0 || value >= (BigInteger.One << field.OrderedBits.Count))
            // Stop this normal path by reporting ArgumentOutOfRangeException; the arguments carry the error
            // details.
            throw new ArgumentOutOfRangeException(nameof(value), $"The value must fit in {field.OrderedBits.Count} unsigned bits.");
        // Collect the proposed edits in a lookup table: each key is a byte offset,
        // and its value is that byte's proposed replacement. Do not write Data yet.
        var replacements = new Dictionary<int, byte>();
        // Repeat with int i = 0 as the starting state, while i is less than field.OrderedBits.Count; after
        // each pass, increase i by one. The braces contain one pass.
        for (int i = 0; i < field.OrderedBits.Count; i++)
        {
            // Read the next physical destination address from the saved map.
            // i counts positions in the map; bit is an address in the actual file.
            long bit = field.OrderedBits[i];
            // Remember (bit divided by 8), converted to int as offset.
            int offset = (int)(bit / 8);
            // Several field bits may share a byte. Reuse its pending value so
            // this iteration retains changes already made earlier in the loop.
            byte previous = replacements.GetValueOrDefault(offset, Data[offset]);
            // Make a number with exactly the destination bit set. If bit % 8 is
            // 2, the shift is 5, giving mask 00100000 (decimal 32).
            int mask = 1 << (7 - (int)(bit % 8));
            // Read the desired value from high bit to low bit. OR sets the source
            // bit; AND with the inverted mask (~mask) clears it. The final byte
            // cast keeps the eight bits that belong to the destination byte.
            bool set = !((value >> (field.OrderedBits.Count - 1 - i)) & BigInteger.One).IsZero;
            // Save a new pending byte: OR with mask turns this bit on; AND with
            // ~mask turns it off. Both preserve all the byte's other positions,
            // including positions changed by an earlier pass through this loop.
            replacements[offset] = (byte)(set ? previous | mask : previous & ~mask);
        }
        // Call ApplyBytes: Turns changed byte values into a compact before/after patch and records one
        // history item.
        ApplyBytes(replacements);
    }

    /// <summary>Adds a validated, independently copied field in one undo step; IDs must be unique.</summary>
    public void AddField(NamedField field)
    {
        // Run ValidateField to reject invalid input before proceeding.
        ValidateField(field);
        // If Fields.Any(f => f.Id == field.Id) is true, stop this normal path by reporting
        // ArgumentException; the arguments carry the error details.
        if (Fields.Any(f => f.Id == field.Id)) throw new ArgumentException("A field with this ID already exists.", nameof(field));
        // Remember a separate list containing Fields.Select(f => f.Clone())'s items as next.
        var next = Fields.Select(f => f.Clone()).ToList();
        // Add field.Clone() to next.
        next.Add(field.Clone());
        // Call ApplyFields: Validates a candidate layout, then records independent before/after metadata
        // snapshots.
        ApplyFields(next);
    }

    /// <summary>Removes a label without changing any bytes; an unknown ID is a no-op.</summary>
    public void RemoveField(Guid id)
    {
        // If it is not the case that Fields.Any(f => f.Id == id) is true, leave this method immediately.
        if (!Fields.Any(f => f.Id == id)) return;
        // Call ApplyFields: Validates a candidate layout, then records independent before/after metadata
        // snapshots.
        ApplyFields(Fields.Where(f => f.Id != id).Select(f => f.Clone()).ToList());
    }

    /// <summary>Replaces the matching field from an edited draft while preserving its original state for Undo.</summary>
    /// <remarks>Clone the live field before editing it. Mutating the live instance first would lose the original values.</remarks>
    public void UpdateField(NamedField field)
    {
        // Run ValidateField to reject invalid input before proceeding.
        ValidateField(field);
        // Remember the position of the first matching item, or -1 if there is no match as index.
        int index = Fields.FindIndex(f => f.Id == field.Id);
        // If index is less than 0, stop this normal path by reporting ArgumentException; the arguments
        // carry the error details.
        if (index < 0) throw new ArgumentException("The field no longer exists in this document.", nameof(field));
        // If FieldsEqual(Fields[index], field) is true, leave this method immediately.
        if (FieldsEqual(Fields[index], field)) return;
        // Remember a separate list containing Fields.Select(f => f.Clone())'s items as next.
        var next = Fields.Select(f => f.Clone()).ToList();
        // Set next[index] to a copy returned by field.Clone().
        next[index] = field.Clone();
        // Call ApplyFields: Validates a candidate layout, then records independent before/after metadata
        // snapshots.
        ApplyFields(next);
    }

    /// <summary>Reverses the newest edit and moves it to the redo stack; does nothing if history is empty.</summary>
    public void Undo()
    {
        // If it is not the case that _undo.TryPop(out Edit? edit) is true, leave this method immediately.
        if (!_undo.TryPop(out Edit? edit)) return;
        // Call edit.Revert().
        edit.Revert();
        // Restore revision identities as well as values, so undoing back to a
        // saved state restores the appropriate clean/dirty indicators too.
        (_revision, _binaryRevision, _metadataRevision) = edit.Before;
        // Put edit on top of _redo; it will be the next item taken from this stack.
        _redo.Push(edit);
        // Announce that the document changed so its listeners can refresh their state.
        OnChanged();
    }

    /// <summary>Reapplies the newest undone edit and restores its later revision identities.</summary>
    public void Redo()
    {
        // If it is not the case that _redo.TryPop(out Edit? edit) is true, leave this method immediately.
        if (!_redo.TryPop(out Edit? edit)) return;
        // Invoke the Apply operation stored in this history entry to install its edited state.
        edit.Apply();
        // Set (_revision, _binaryRevision, _metadataRevision) to edit.After.
        (_revision, _binaryRevision, _metadataRevision) = edit.After;
        // Put edit on top of _undo; it will be the next item taken from this stack.
        _undo.Push(edit);
        // Announce that the document changed so its listeners can refresh their state.
        OnChanged();
    }

    /// <summary>Compares a byte with its value on open or at the most recent binary save.</summary>
    /// <remarks>Unlike IsBinaryDirty, this checks actual bytes, so manually restoring a byte removes its highlight.</remarks>
    public bool IsByteModified(int offset)
    {
        // Run ValidateOffset to reject invalid input before proceeding.
        ValidateOffset(offset);
        // Return whether Data[offset] differs from _binaryBaseline[offset] to the caller and leave this
        // method.
        return Data[offset] != _binaryBaseline[offset];
    }

    /// <summary>Checks file size, reads its bytes, and returns a fresh document with empty undo history.</summary>
    public static DocumentModel OpenBinary(string path)
    {
        // Call ValidateFileLength: Rejects missing or oversized files before allocating space to read their
        // contents.
        ValidateFileLength(path, MaxBinaryBytes, "binary");
        // Return a new DocumentModel object using the inputs in parentheses to the caller and leave this
        // method.
        return new DocumentModel(File.ReadAllBytes(path), Path.GetFullPath(path));
    }

    /// <summary>Writes only raw bytes, then updates the binary baseline if the write succeeds.</summary>
    /// <remarks>Labels and display settings require a project save. Existing undo history is retained.</remarks>
    public void SaveBinary(string path)
    {
        // Call AtomicWrite: Writes a complete temporary file before replacing or creating the destination.
        AtomicWrite(path, stream => stream.Write(Data));
        // Set FilePath to the full filesystem path for path.
        FilePath = Path.GetFullPath(path);
        // Set _binaryBaseline to a separate array containing Data's items.
        _binaryBaseline = Data.ToArray();
        // Set _savedBinaryRevision to _binaryRevision.
        _savedBinaryRevision = _binaryRevision;
        // Announce that the document changed so its listeners can refresh their state.
        OnChanged();
    }

    /// <summary>Saves edited bytes, display settings, and field definitions together in versioned JSON.</summary>
    public void SaveProject(string path)
    {
        // Run ValidateDocument to reject invalid input before proceeding.
        ValidateDocument();
        // Remember a new ProjectFile object; the entries in braces set its initial contents or properties
        // as project.
        var project = new ProjectFile
        {
            // Set this new object's Version entry to 1.
            Version = 1, Data = Data, SourcePath = FilePath,
            // Set this new object's Settings entry to a copy returned by Settings.Clone().
            Settings = Settings.Clone(), Fields = Fields.Select(f => f.Clone()).ToList()
        };
        // System.Text.Json represents byte[] as base64 in JSON. The stored source
        // path is descriptive; the project contains its own complete data copy.
        AtomicWrite(path, stream => JsonSerializer.Serialize(stream, project, JsonOptions));
        // Set ProjectPath to the full filesystem path for path.
        ProjectPath = Path.GetFullPath(path);
        // Set _savedProjectRevision to _revision.
        _savedProjectRevision = _revision;
        // Set _savedMetadataRevision to _metadataRevision.
        _savedMetadataRevision = _metadataRevision;
        // Set _savedSettings to a copy returned by Settings.Clone().
        _savedSettings = Settings.Clone();
        // Announce that the document changed so its listeners can refresh their state.
        OnChanged();
    }

    /// <summary>Reads and validates a self-contained project without reading its stored source path.</summary>
    /// <remarks>Unsupported versions and invalid fields/settings are rejected before a document is returned.</remarks>
    public static DocumentModel OpenProject(string path)
    {
        // JSON/base64 takes more space than raw bytes, hence this larger file
        // limit. The constructor still applies the separate binary-data limit.
        ProjectFile project = ReadJson<ProjectFile>(path, 512L * 1024 * 1024);
        // If project.Version differs from 1, stop this normal path by reporting InvalidDataException; the
        // arguments carry the error details.
        if (project.Version != 1) throw new InvalidDataException("Unsupported project version. Expected version 1.");
        // If project.Data equals null, or project.Settings equals null, or project.Fields equals null, stop
        // this normal path by reporting InvalidDataException; the arguments carry the error details.
        if (project.Data == null || project.Settings == null || project.Fields == null)
            // Stop this normal path by reporting InvalidDataException; the arguments carry the error
            // details.
            throw new InvalidDataException("The project is missing data, display settings, or fields.");
        // Attempt this work. A matching catch below handles a reported exception; a finally block, when
        // present, performs cleanup on the way out.
        try
        {
            // Remember a new DocumentModel object using the inputs in parentheses; the entries in braces
            // set its initial contents or properties as document.
            var document = new DocumentModel(project.Data, project.SourcePath)
            {
                // Set this new object's Settings entry to project.Settings.
                Settings = project.Settings,
                // Set this new object's Fields entry to project.Fields.
                Fields = project.Fields,
                // Set this new object's ProjectPath entry to the full filesystem path for path.
                ProjectPath = Path.GetFullPath(path)
            };
            // Run document.ValidateDocument to reject invalid input before proceeding.
            document.ValidateDocument();
            // Set document._savedSettings to a copy returned by document.Settings.Clone().
            document._savedSettings = document.Settings.Clone();
            // Return document to the caller and leave this method.
            return document;
        }
        // If the preceding try reports ArgumentException, refer to it as ex; run this recovery path.
        catch (ArgumentException ex) { throw new InvalidDataException("The project contains invalid binary data or field definitions.", ex); }
    }

    /// <summary>Saves just the reusable field layout, without binary data or display settings.</summary>
    public void ExportTemplate(string path)
    {
        // Run ValidateDocument to reject invalid input before proceeding.
        ValidateDocument();
        // Remember a new TemplateFile object; the entries in braces set its initial contents or properties
        // as template.
        var template = new TemplateFile { Version = 1, Fields = Fields.Select(f => f.Clone()).ToList() };
        // Call AtomicWrite: Writes a complete temporary file before replacing or creating the destination.
        AtomicWrite(path, stream => JsonSerializer.Serialize(stream, template, JsonOptions));
    }

    /// <summary>Replaces the current field layout in one undoable operation; the binary is untouched.</summary>
    /// <remarks>Every source bit must fit this document. Validation finishes before the current layout changes.</remarks>
    public void ImportTemplate(string path)
    {
        // Remember the result returned by ReadJson<TemplateFile>(...) as template.
        TemplateFile template = ReadJson<TemplateFile>(path, 64L * 1024 * 1024);
        // If template.Version differs from 1, stop this normal path by reporting InvalidDataException; the
        // arguments carry the error details.
        if (template.Version != 1) throw new InvalidDataException("Unsupported field template version. Expected version 1.");
        // If template.Fields equals null, stop this normal path by reporting InvalidDataException; the
        // arguments carry the error details.
        if (template.Fields == null) throw new InvalidDataException("The template is missing its field layout.");
        // Attempt this work. A matching catch below handles a reported exception; a finally block, when
        // present, performs cleanup on the way out.
        try { ApplyFields(template.Fields); }
        // If the preceding try reports ArgumentException, refer to it as ex; run this recovery path.
        catch (ArgumentException ex) { throw new InvalidDataException("The template has invalid fields or refers to bits outside this file.", ex); }
    }

    /// <summary>Builds a deterministic example packet with readable text and several kinds of packed fields.</summary>
    /// <remarks>The demo is initial content, not a series of user edits, so it starts with empty undo history.</remarks>
    public static DocumentModel CreateDemo()
    {
        // Remember a new array of byte values; the brackets specify its size as bytes.
        var bytes = new byte[512];
        // Set bytes[0] to 0xB3.
        bytes[0] = 0xB3; bytes[1] = 0x6C; bytes[2] = 0xA1; bytes[3] = 0x48;
        // Set bytes[4] to 0x02.
        bytes[4] = 0x02; bytes[5] = 0x00; bytes[6] = 0x7D; bytes[7] = 0x9A;
        // Call Encoding.ASCII.GetBytes("BITEXPLORER / CUSTOM TELEMETRY\0").CopyTo(...); the values in
        // parentheses are the inputs.
        Encoding.ASCII.GetBytes("BITEXPLORER / CUSTOM TELEMETRY\0").CopyTo(bytes, 16);
        // Populate reproducible payload bytes. AND 255 keeps the lowest eight
        // bits of each generated value; this is sample data, not encryption.
        for (int i = 64; i < bytes.Length; i++) bytes[i] = (byte)((i * 37 + (i >> 3) * 11) & 255);
        // Repeat with int i = 0 as the starting state, while i is less than 8; after each pass, increase i
        // by one. The braces contain one pass.
        for (int i = 0; i < 8; i++)
        {
            // Remember 64 + i multiplied by 32 (addition, or joining text) as offset.
            int offset = 64 + i * 32;
            // Set bytes[offset] to (0x80 OR i (a bit is 1 where either has 1)), converted to byte.
            bytes[offset] = (byte)(0x80 | i);
            // Set bytes[offset + 1] to (20 + i * 3 (addition, or joining text)), converted to byte.
            bytes[offset + 1] = (byte)(20 + i * 3);
            // Call Encoding.ASCII.GetBytes($"SENSOR-{i + 1:00}").CopyTo(...); the values in parentheses are
            // the inputs.
            Encoding.ASCII.GetBytes($"SENSOR-{i + 1:00}").CopyTo(bytes, offset + 8);
        }
        // Remember a new DocumentModel object using the inputs in parentheses as document.
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
        // Return document to the caller and leave this method.
        return document;
    }

    /// <summary>Turns changed byte values into a compact before/after patch and records one history item.</summary>
    // Store only changed bytes: a one-bit edit should not copy the entire file
    // into history. If every supplied value is unchanged, there is nothing to undo.
    private void ApplyBytes(Dictionary<int, byte> replacements)
    {
        // Remember a separate array containing the expression below's items as changes.
        var changes = replacements.Where(pair => Data[pair.Key] != pair.Value)
            .Select(pair => (Offset: pair.Key, Before: Data[pair.Key], After: pair.Value)).ToArray();
        // If changes.Length equals 0, leave this method immediately.
        if (changes.Length == 0) return;
        // Call Execute: Runs an edit, assigns revision identities, and starts a new branch of undo history.
        Execute(() => { foreach (var change in changes) Data[change.Offset] = change.After; },
            // This second operation is the inverse: for each saved change, put its
            // Before byte back at the same offset. binary: true marks a byte edit.
            () => { foreach (var change in changes) Data[change.Offset] = change.Before; }, binary: true);
    }

    /// <summary>Validates a candidate layout, then records independent before/after metadata snapshots.</summary>
    private void ApplyFields(List<NamedField> fields)
    {
        // Run ValidateFields to reject invalid input before proceeding.
        ValidateFields(fields);
        // If both Fields.Count equals fields.Count and Fields.Zip(fields).All(pair =>
        // FieldsEqual(pair.First, pair.Second)) is true, leave this method immediately.
        if (Fields.Count == fields.Count && Fields.Zip(fields).All(pair => FieldsEqual(pair.First, pair.Second))) return;
        // Remember a separate list containing Fields.Select(f => f.Clone())'s items as before.
        var before = Fields.Select(f => f.Clone()).ToList();
        // Remember a separate list containing fields.Select(f => f.Clone())'s items as after.
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
        // Remember _nextRevision after increasing it by one as next.
        long next = ++_nextRevision;
        // Remember a new Edit object using the inputs in parentheses as edit.
        var edit = new Edit(apply, revert, (_revision, _binaryRevision, _metadataRevision),
            (next, binary ? next : _binaryRevision, binary ? _metadataRevision : next));
        // Invoke the Apply operation stored in this history entry to install its edited state.
        edit.Apply();
        // Set (_revision, _binaryRevision, _metadataRevision) to edit.After.
        (_revision, _binaryRevision, _metadataRevision) = edit.After;
        // Put edit on top of _undo; it will be the next item taken from this stack.
        _undo.Push(edit);
        // Undo followed by a new edit discards the old redo path. Its revision
        // numbers are never reused, even if the new edit happens to look similar.
        _redo.Clear();
        // Announce that the document changed so its listeners can refresh their state.
        OnChanged();
    }

    /// <summary>Checks a byte address before it can be used as an array index.</summary>
    private void ValidateOffset(int offset)
    {
        // If offset is less than 0, or offset is at least Data.Length, stop this normal path by reporting
        // ArgumentOutOfRangeException; the arguments carry the error details.
        if (offset < 0 || offset >= Data.Length) throw new ArgumentOutOfRangeException(nameof(offset), "The byte offset is outside this file.");
    }

    /// <summary>Checks a physical bit address against eight times the byte count.</summary>
    private void ValidateBit(long bit)
    {
        // If bit is less than 0, or bit is at least Data.LongLength * 8, stop this normal path by reporting
        // ArgumentOutOfRangeException; the arguments carry the error details.
        if (bit < 0 || bit >= Data.LongLength * 8) throw new ArgumentOutOfRangeException(nameof(bit), "The physical bit address is outside this file.");
    }

    /// <summary>Checks identity, readable metadata, a bounded width, and valid distinct source bits.</summary>
    private void ValidateField(NamedField field)
    {
        // Reject field immediately if it is null (no object was supplied).
        ArgumentNullException.ThrowIfNull(field);
        // If field.Id equals Guid.Empty, stop this normal path by reporting ArgumentException; the
        // arguments carry the error details.
        if (field.Id == Guid.Empty) throw new ArgumentException("A field must have a nonempty ID.", nameof(field));
        // If string.IsNullOrWhiteSpace(field.Name) is true, or field.Name.Length is greater than 256, or
        // field.Name.Any(char.IsControl) is true, stop this normal path by reporting ArgumentException; the
        // arguments carry the error details.
        if (string.IsNullOrWhiteSpace(field.Name) || field.Name.Length > 256 || field.Name.Any(char.IsControl))
            // Stop this normal path by reporting ArgumentException; the arguments carry the error details.
            throw new ArgumentException("A field name must contain 1–256 characters and no control characters.", nameof(field));
        // If field.Notes equals null, or field.Notes.Length is greater than 65536, stop this normal path by
        // reporting ArgumentException; the arguments carry the error details.
        if (field.Notes == null || field.Notes.Length > 65536) throw new ArgumentException("Field notes must contain at most 65,536 characters.", nameof(field));
        // If field.Color equals null, or field.Color.Length differs from 7, or field.Color[0] differs from
        // '#', or it is not the case that field.Color.AsSpan(1).ContainsOnlyHex() is true, stop this normal
        // path by reporting ArgumentException; the arguments carry the error details.
        if (field.Color == null || field.Color.Length != 7 || field.Color[0] != '#' || !field.Color.AsSpan(1).ContainsOnlyHex())
            // Stop this normal path by reporting ArgumentException; the arguments carry the error details.
            throw new ArgumentException("A field color must use #RRGGBB hexadecimal notation.", nameof(field));
        // If field.OrderedBits equals null, or field.OrderedBits.Count matches < 1 or > MaxFieldBits, stop
        // this normal path by reporting ArgumentException; the arguments carry the error details.
        if (field.OrderedBits == null || field.OrderedBits.Count is < 1 or > MaxFieldBits)
            // Stop this normal path by reporting ArgumentException; the arguments carry the error details.
            throw new ArgumentException($"A field must contain 1–{MaxFieldBits} bits.", nameof(field));
        // One source bit cannot appear twice in a field: it would then have two
        // numeric roles and writes would be ambiguous. Different fields may overlap.
        var seen = new HashSet<long>();
        // Take each item from field.OrderedBits in turn, call the current item bit, and run the following
        // grouped instructions.
        foreach (long bit in field.OrderedBits)
        {
            // Run ValidateBit to reject invalid input before proceeding.
            ValidateBit(bit);
            // If it is not the case that seen.Add(bit) is true, stop this normal path by reporting
            // ArgumentException; the arguments carry the error details.
            if (!seen.Add(bit)) throw new ArgumentException("A physical bit may only appear once within a field.", nameof(field));
        }
    }

    /// <summary>Checks the entire layout, including unique IDs and the total bit-reference count.</summary>
    private void ValidateFields(List<NamedField> fields)
    {
        // If fields.Count is greater than MaxFields, stop this normal path by reporting ArgumentException;
        // the arguments carry the error details.
        if (fields.Count > MaxFields) throw new ArgumentException($"At most {MaxFields} named fields are supported.", nameof(fields));
        // Remember 0 as total.
        long total = 0;
        // Remember a new HashSet<Guid> object as ids.
        var ids = new HashSet<Guid>();
        // Take each item from fields in turn, call the current item field, and run the following grouped
        // instructions.
        foreach (NamedField field in fields)
        {
            // Run ValidateField to reject invalid input before proceeding.
            ValidateField(field);
            // If it is not the case that ids.Add(field.Id) is true, stop this normal path by reporting
            // ArgumentException; the arguments carry the error details.
            if (!ids.Add(field.Id)) throw new ArgumentException("Field IDs must be unique.", nameof(fields));
            // Add field.OrderedBits.Count to total and keep the result there (+=).
            total += field.OrderedBits.Count;
            // If total is greater than MaxTotalFieldBits, stop this normal path by reporting
            // ArgumentException; the arguments carry the error details.
            if (total > MaxTotalFieldBits) throw new ArgumentException("The field layout contains too many bit references.", nameof(fields));
        }
    }

    /// <summary>Checks settings and field definitions before project/template persistence.</summary>
    private void ValidateDocument()
    {
        // Reject Settings immediately if it is null (no object was supplied).
        ArgumentNullException.ThrowIfNull(Settings);
        // Run Settings.Validate to reject invalid input before proceeding.
        Settings.Validate();
        // Run ValidateFields to reject invalid input before proceeding.
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
        // Call ValidateFileLength: Rejects missing or oversized files before allocating space to read their
        // contents.
        ValidateFileLength(path, maxBytes, "JSON");
        // Attempt this work. A matching catch below handles a reported exception; a finally block, when
        // present, performs cleanup on the way out.
        try
        {
            // Remember the result returned by File.OpenRead(...) as stream. The using declaration releases
            // it automatically when this scope ends, even after an error.
            using var stream = File.OpenRead(path);
            // Return the result returned by JsonSerializer.Deserialize<T>(...), falling back to throw new
            // InvalidDataException("The JSON file is empty.") if it is null to the caller and leave this
            // method.
            return JsonSerializer.Deserialize<T>(stream, JsonOptions) ?? throw new InvalidDataException("The JSON file is empty.");
        }
        // If the preceding try reports JsonException, refer to it as ex; run this recovery path.
        catch (JsonException ex) { throw new InvalidDataException("The file does not contain a valid BitExplorer JSON document.", ex); }
    }

    /// <summary>Rejects missing or oversized files before allocating space to read their contents.</summary>
    private static void ValidateFileLength(string path, long maxBytes, string kind)
    {
        // Reject path if it is missing, empty, or contains only spacing characters.
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        // Remember a new FileInfo object using the inputs in parentheses as info.
        var info = new FileInfo(path);
        // If it is not the case that info.Exists is true, stop this normal path by reporting
        // FileNotFoundException; the arguments carry the error details.
        if (!info.Exists) throw new FileNotFoundException("The selected file does not exist.", path);
        // If info.Length is greater than maxBytes, stop this normal path by reporting InvalidDataException;
        // the arguments carry the error details.
        if (info.Length > maxBytes) throw new InvalidDataException($"The {kind} file is too large (maximum {maxBytes / 1024 / 1024} MiB).");
    }

    /// <summary>Writes a complete temporary file before replacing or creating the destination.</summary>
    /// <param name="path">The destination; its parent directory must already exist.</param>
    /// <param name="write">The byte-writing or JSON-serialization operation for the temporary stream.</param>
    /// <remarks>I/O errors propagate; callers update saved-state baselines only after this method succeeds.</remarks>
    private static void AtomicWrite(string path, Action<FileStream> write)
    {
        // Reject path if it is missing, empty, or contains only spacing characters.
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        // Remember the full filesystem path for path as fullPath.
        string fullPath = Path.GetFullPath(path);
        // Remember the folder portion of fullPath (! tells the compiler we expect a value here; it does not
        // check at runtime) as directory.
        string directory = Path.GetDirectoryName(fullPath)!;
        // The same directory keeps the final rename/replace on the same volume.
        // A random name plus CreateNew avoids reusing an existing temporary file.
        string temporary = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        // Attempt this work. A matching catch below handles a reported exception; a finally block, when
        // present, performs cleanup on the way out.
        try
        {
            // Use var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
            // FileShare.None) for this block; release it automatically when leaving the block, including on
            // errors.
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                // Call write(...); the values in parentheses are the inputs.
                write(stream);
                // Flush before closing and publishing the file. The existing
                // destination has not been touched while the bytes are written.
                stream.Flush(flushToDisk: true);
            }
            // Replace avoids exposing a partially written existing file. A first
            // save uses Move because there is no destination to replace yet.
            if (File.Exists(fullPath)) File.Replace(temporary, fullPath, null);
            // If the preceding condition was false, run this alternative path.
            else File.Move(temporary, fullPath);
        }
        // Run this cleanup when leaving the try, whether the work succeeded or reported an exception.
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
        // Take each item from value in turn, call the current item c, and if it is not the case that
        // char.IsAsciiHexDigit(c) is true, return false (no) to the caller and leave this method.
        foreach (char c in value) if (!char.IsAsciiHexDigit(c)) return false;
        // Return true (yes) to the caller and leave this method.
        return true;
    }
}
