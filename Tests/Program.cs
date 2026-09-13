using System.Numerics;
using System.Text.Json.Nodes;
using BitExplorer.Core;

// Run with: dotnet run --project Tests/BitExplorer.Tests.csproj
// No test framework or downloaded packages are required.
internal static partial class Program
{
    private static int _passed;
    private static int _failed;
    private static readonly string ArtifactRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "test-artifacts"));
    private static readonly string ArtifactDirectory = Path.Combine(ArtifactRoot, Guid.NewGuid().ToString("N"));

    private static int Main()
    {
        Directory.CreateDirectory(ArtifactDirectory);
        try
        {
            Test("constructing a document owns its input bytes", ConstructorOwnsData);
            Test("bit addressing uses the physical MSB-first byte layout", BitAddressing);
            Test("a cross-byte field reads the expected packed value", CrossByteField);
            Test("field writes preserve every bit outside the selection", FieldWritePreservesOtherBits);
            Test("discontiguous field values follow the specified assembly order", DiscontiguousFieldOrder);
            Test("field assembly supports little-endian bytes and values wider than 64 bits", WideAndLittleEndianFields);
            Test("field edits undo and redo as one operation", FieldEditUndoRedo);
            Test("byte edit history branches and modified markers recover", ByteEditHistory);
            Test("invalid numeric field edits are rejected without mutation", InvalidFieldValues);
            Test("duplicate, negative, outside-file, and excessive field bits are rejected", InvalidFieldDefinitions);
            Test("field metadata edits participate in undo and redo", FieldMetadataHistory);
            Test("binary save preserves exact bytes and clears binary modifications", BinaryRoundTrip);
            Test("empty binary documents round trip", EmptyBinaryRoundTrip);
            Test("projects restore bytes and discontiguous field metadata", ProjectRoundTrip);
            Test("templates transfer field definitions without changing destination bytes", TemplateRoundTrip);
            Test("an out-of-range template is rejected atomically", InvalidTemplateIsAtomic);
            Test("duplicate persisted bit references are rejected without replacing existing fields", DuplicateTemplateBits);
            Test("unsupported project versions and invalid display settings are rejected", InvalidProjectMetadata);
            Test("malformed projects and templates are rejected", MalformedPersistence);
            RunFormattingTests();
            RunSelectionTests();
        }
        finally
        {
            // Only delete the unique directory created by this process inside our test output.
            var normalizedDirectory = Path.GetFullPath(ArtifactDirectory);
            if (normalizedDirectory.StartsWith(ArtifactRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(normalizedDirectory))
            {
                Directory.Delete(normalizedDirectory, recursive: true);
            }
        }

        Console.WriteLine($"\n{_passed} passed, {_failed} failed.");
        return _failed == 0 ? 0 : 1;
    }

    private static void ConstructorOwnsData()
    {
        var source = new byte[] { 0xB3, 0x6C };
        var document = new DocumentModel(source);
        source[0] = 0;
        Sequence(new byte[] { 0xB3, 0x6C }, document.Data);
        Equal(false, document.CanUndo);
        Equal(false, document.IsByteModified(0));
    }

    private static void BitAddressing()
    {
        var document = new DocumentModel(new byte[] { 0, 0 });
        document.FlipBit(0);
        document.FlipBit(7);
        document.FlipBit(8);
        document.FlipBit(15);
        Sequence(new byte[] { 0x81, 0x81 }, document.Data);
        var before = document.Data.ToArray();
        Throws<ArgumentException>(() => document.FlipBit(-1));
        Throws<ArgumentException>(() => document.FlipBit(16));
        Sequence(before, document.Data);
    }

    private static void CrossByteField()
    {
        var document = Sample();
        var field = Field("Packed", 5, 6, 7, 8, 9, 10);
        document.AddField(field);
        Equal(new BigInteger(27), document.ReadField(field));
    }

    private static void FieldWritePreservesOtherBits()
    {
        var document = Sample();
        var field = Field("Packed", 5, 6, 7, 8, 9, 10);
        document.AddField(field);
        document.SetFieldValue(field, 42); // 101010 replaces only the six selected bits.
        Sequence(new byte[] { 0xB5, 0x4C }, document.Data);
        Equal(new BigInteger(42), document.ReadField(field));
    }

    private static void DiscontiguousFieldOrder()
    {
        var document = Sample();
        var field = Field("Scattered", 0, 9, 3, 14);
        document.AddField(field);
        Equal(new BigInteger(14), document.ReadField(field));
        document.SetFieldValue(field, 5); // 0, 1, 0, 1 in the explicitly supplied order.
        Sequence(new byte[] { 0x23, 0x6E }, document.Data);
        Equal(new BigInteger(5), document.ReadField(field));

        var reversed = Field("Reverse assembly", 14, 3, 9, 0);
        document.AddField(reversed);
        Equal(new BigInteger(10), document.ReadField(reversed));
    }

    private static void FieldEditUndoRedo()
    {
        var document = Sample();
        var field = Field("Packed", 5, 6, 7, 8, 9, 10);
        document.AddField(field);
        document.SetFieldValue(field, 42);
        Equal(true, document.IsByteModified(0));
        Equal(true, document.IsByteModified(1));
        document.Undo();
        Sequence(new byte[] { 0xB3, 0x6C }, document.Data);
        Equal(1, document.Fields.Count);
        Equal(false, document.IsByteModified(0));
        Equal(false, document.IsByteModified(1));
        Equal(true, document.CanRedo);
        document.Redo();
        Sequence(new byte[] { 0xB5, 0x4C }, document.Data);
    }

    private static void WideAndLittleEndianFields()
    {
        var document = Sample();
        var littleEndian = Field("LE word", Enumerable.Range(8, 8).Concat(Enumerable.Range(0, 8)).Select(i => (long)i).ToArray());
        Equal(new BigInteger(0x6CB3), document.ReadField(littleEndian));
        document.SetFieldValue(littleEndian, 0x1234);
        Sequence(new byte[] { 0x34, 0x12 }, document.Data);

        var wide = new DocumentModel(new byte[10]);
        var field = Field("72-bit value", Enumerable.Range(3, 72).Select(i => (long)i).ToArray());
        var value = (BigInteger.One << 71) | (BigInteger.One << 37) | 5;
        wide.SetFieldValue(field, value);
        Equal(value, wide.ReadField(field));
        Equal(0, wide.Data[0] & 0xE0);
        Equal(0, wide.Data[9] & 0x1F);
        wide.Undo();
        Sequence(new byte[10], wide.Data);
    }

    private static void ByteEditHistory()
    {
        var document = Sample();
        document.SetByte(0, 0x11);
        Equal(true, document.IsBinaryDirty);
        document.Undo();
        Equal((byte)0xB3, document.Data[0]);
        Equal(false, document.IsByteModified(0));
        Equal(true, document.CanRedo);
        document.SetByte(1, 0x22);
        Equal(false, document.CanRedo);
        document.Undo();
        Sequence(new byte[] { 0xB3, 0x6C }, document.Data);
        document.Redo();
        Sequence(new byte[] { 0xB3, 0x22 }, document.Data);
    }

    private static void InvalidFieldValues()
    {
        var document = Sample();
        var field = Field("Small", 5, 6, 7);
        document.AddField(field);
        Throws<ArgumentOutOfRangeException>(() => document.SetFieldValue(field, -1));
        Throws<ArgumentOutOfRangeException>(() => document.SetFieldValue(field, 8));
        Sequence(new byte[] { 0xB3, 0x6C }, document.Data);
        document.Undo();
        Equal(0, document.Fields.Count); // rejected writes did not enter history.
    }

    private static void InvalidFieldDefinitions()
    {
        var document = Sample();
        Throws<ArgumentException>(() => document.AddField(Field("Duplicate", 1, 1)));
        Throws<ArgumentException>(() => document.AddField(Field("Negative", -1)));
        Throws<ArgumentException>(() => document.AddField(Field("Outside", 16)));
        var wide = new DocumentModel(new byte[513]);
        Throws<ArgumentException>(() => wide.AddField(Field("Too wide", Enumerable.Range(0, 4097).Select(i => (long)i).ToArray())));
        Equal(0, document.Fields.Count);
        Equal(false, document.CanUndo);
    }

    private static void FieldMetadataHistory()
    {
        var document = Sample();
        var initial = Field("Flags", 0, 3, 7);
        document.AddField(initial);
        var updated = Field("Status", 0, 3, 7);
        updated.Id = initial.Id;
        updated.Notes = "Three scattered status bits";
        document.UpdateField(updated);
        Equal("Status", document.Fields.Single().Name);
        document.Undo();
        Equal("Flags", document.Fields.Single().Name);
        document.Redo();
        Equal("Three scattered status bits", document.Fields.Single().Notes);
        document.RemoveField(initial.Id);
        Equal(0, document.Fields.Count);
        document.Undo();
        Equal("Status", document.Fields.Single().Name);
        document.Redo();
        Equal(0, document.Fields.Count);
    }

    private static void BinaryRoundTrip()
    {
        var document = Sample();
        document.SetByte(0, 0xFF);
        var path = Artifact("edited.bin");
        document.SaveBinary(path);
        document.SaveBinary(path); // exercising an atomic overwrite of an existing file.
        Sequence(new byte[] { 0xFF, 0x6C }, File.ReadAllBytes(path));
        Equal(false, document.IsBinaryDirty);
        Equal(false, document.IsByteModified(0));
        var reopened = DocumentModel.OpenBinary(path);
        Sequence(document.Data, reopened.Data);
        document.Undo();
        Equal(true, document.IsByteModified(0)); // undo is measured against the newly saved bytes.
    }

    private static void EmptyBinaryRoundTrip()
    {
        var path = Artifact("empty.bin");
        new DocumentModel().SaveBinary(path);
        Equal(0, File.ReadAllBytes(path).Length);
        var reopened = DocumentModel.OpenBinary(path);
        Equal(0, reopened.Data.Length);
        Equal(false, reopened.CanUndo);
    }

    private static void ProjectRoundTrip()
    {
        var document = Sample();
        var field = Field("Message kind", 0, 9, 3, 14);
        field.Notes = "Unicode notes: µ / λ / payload";
        field.Color = "#63C9A7";
        document.AddField(field);
        document.SetFieldValue(field, 5);
        document.Settings.ViewMode = DataViewMode.Bits;
        document.Settings.ByteBase = NumericBase.Decimal;
        document.Settings.OffsetBase = NumericBase.Decimal;
        document.Settings.BitNumbering = BitNumbering.MsbZero;
        document.Settings.BytesPerRow = 7;
        document.Settings.AutoBytesPerRow = true;
        document.Settings.OffsetWidth = 144;
        document.Settings.DataWidth = 920;
        document.Settings.AsciiWidth = 224;
        document.Settings.CharacterWidth = 7.5;
        document.Settings.ShowAnnotations = false;
        document.Settings.ShowRuler = false;
        document.NotifySettingsChanged();
        var path = Artifact("sample.bitexplorer.json");
        document.SaveProject(path);
        var reopened = DocumentModel.OpenProject(path);
        Sequence(document.Data, reopened.Data);
        Equal(1, reopened.Fields.Count);
        var restored = reopened.Fields[0];
        Equal(field.Id, restored.Id);
        Equal(field.Name, restored.Name);
        Equal(field.Notes, restored.Notes);
        Equal(field.Color, restored.Color);
        Sequence(field.OrderedBits, restored.OrderedBits);
        Equal(new BigInteger(5), reopened.ReadField(restored));
        Equal(DataViewMode.Bits, reopened.Settings.ViewMode);
        Equal(NumericBase.Decimal, reopened.Settings.ByteBase);
        Equal(NumericBase.Decimal, reopened.Settings.OffsetBase);
        Equal(BitNumbering.MsbZero, reopened.Settings.BitNumbering);
        Equal(7, reopened.Settings.BytesPerRow);
        Equal(true, reopened.Settings.AutoBytesPerRow);
        Equal(144d, reopened.Settings.OffsetWidth);
        Equal(920d, reopened.Settings.DataWidth);
        Equal(224d, reopened.Settings.AsciiWidth);
        Equal(7.5d, reopened.Settings.CharacterWidth);
        Equal(false, reopened.Settings.ShowAnnotations);
        Equal(false, reopened.Settings.ShowRuler);
        Equal(false, reopened.IsProjectDirty);
        Equal(false, reopened.CanUndo);
    }

    private static void TemplateRoundTrip()
    {
        var source = Sample();
        var field = Field("Scattered", 0, 9, 3, 14);
        field.Notes = "Assembled most significant bit first";
        source.AddField(field);
        var path = Artifact("fields.template.json");
        source.ExportTemplate(path);
        var destination = new DocumentModel(new byte[] { 0xFF, 0xFF });
        destination.ImportTemplate(path);
        Sequence(new byte[] { 0xFF, 0xFF }, destination.Data);
        Equal(1, destination.Fields.Count);
        Sequence(field.OrderedBits, destination.Fields[0].OrderedBits);
        Equal(field.Notes, destination.Fields[0].Notes);
        Equal(new BigInteger(15), destination.ReadField(destination.Fields[0]));
        destination.Undo();
        Equal(0, destination.Fields.Count);
        destination.Redo();
        Equal(1, destination.Fields.Count);
    }

    private static void InvalidTemplateIsAtomic()
    {
        var source = Sample();
        source.AddField(Field("Fits", 0));
        source.AddField(Field("Outside destination", 15));
        var path = Artifact("too-large.template.json");
        source.ExportTemplate(path);
        var destination = new DocumentModel(new byte[] { 0xA5 });
        destination.AddField(Field("Existing", 1, 2));
        Throws<InvalidDataException>(() => destination.ImportTemplate(path));
        Equal(1, destination.Fields.Count);
        Equal("Existing", destination.Fields[0].Name);
        Sequence(new byte[] { 0xA5 }, destination.Data);
        destination.Undo();
        Equal(0, destination.Fields.Count); // failed imports must leave history untouched.
    }

    private static void MalformedPersistence()
    {
        var path = Artifact("malformed.json");
        File.WriteAllText(path, "{not valid json]");
        Throws<InvalidDataException>(() => DocumentModel.OpenProject(path));
        var document = Sample();
        Throws<InvalidDataException>(() => document.ImportTemplate(path));
        Equal(false, document.CanUndo);
        Sequence(new byte[] { 0xB3, 0x6C }, document.Data);
    }

    private static void DuplicateTemplateBits()
    {
        var source = Sample();
        source.AddField(Field("Bits", 0, 1));
        var path = Artifact("duplicate.template.json");
        source.ExportTemplate(path);
        var json = JsonNode.Parse(File.ReadAllText(path))!;
        json["Fields"]![0]!["OrderedBits"] = new JsonArray(0, 0);
        File.WriteAllText(path, json.ToJsonString());
        var destination = Sample();
        destination.AddField(Field("Existing", 2));
        Throws<InvalidDataException>(() => destination.ImportTemplate(path));
        Equal("Existing", destination.Fields.Single().Name);
        Sequence(new byte[] { 0xB3, 0x6C }, destination.Data);
    }

    private static void InvalidProjectMetadata()
    {
        var document = Sample();
        var path = Artifact("invalid-metadata.project.json");
        document.SaveProject(path);
        var json = JsonNode.Parse(File.ReadAllText(path))!;
        json["Version"] = 999;
        File.WriteAllText(path, json.ToJsonString());
        Throws<InvalidDataException>(() => DocumentModel.OpenProject(path));
        json["Version"] = 1;
        json["Settings"]!["BytesPerRow"] = 0;
        File.WriteAllText(path, json.ToJsonString());
        Throws<InvalidDataException>(() => DocumentModel.OpenProject(path));
        json["Settings"]!["BytesPerRow"] = 16;
        json["Settings"]!["BitNumbering"] = 123;
        File.WriteAllText(path, json.ToJsonString());
        Throws<InvalidDataException>(() => DocumentModel.OpenProject(path));
    }

    private static void RunFormattingTests()
    {
        Test("hex and decimal notation preserve byte and offset values", NumericFormatting);
        Test("changing bit numbering changes rulers without reversing the data", BitRulers);
        Test("hex export preserves the complete file, row breaks, and ASCII alignment", HexExport);
        Test("decimal export preserves padded values and independent decimal offsets", DecimalExport);
        Test("bits-only export preserves physical bit order in either numbering convention", BitExport);
        Test("hidden columns and ruler disappear from exported text", DataOnlyExport);
        Test("partial rows align ASCII and show all intersecting fields", PartialRowAnnotations);
        Test("resized columns preserve exact character positions in text exports", ResizedColumns);
        Test("narrow columns expand to show complete fixed-width rows", NarrowColumns);
        Test("annotation rows preserve clipped labels and empty row spacing", AnnotationRows);
        Test("empty exports contain no invented data rows", EmptyExport);
        Test("invalid formatting settings fail before output is written", InvalidFormatting);
    }

    private static void NumericFormatting()
    {
        Equal("00", DisplayFormatter.FormatByte(0, NumericBase.Hexadecimal));
        Equal("A1", DisplayFormatter.FormatByte(161, NumericBase.Hexadecimal));
        Equal("FF", DisplayFormatter.FormatByte(255, NumericBase.Hexadecimal));
        Equal("000", DisplayFormatter.FormatByte(0, NumericBase.Decimal));
        Equal("161", DisplayFormatter.FormatByte(161, NumericBase.Decimal));
        Equal("255", DisplayFormatter.FormatByte(255, NumericBase.Decimal));
        Equal("00000010", DisplayFormatter.FormatOffset(16, NumericBase.Hexadecimal));
        Equal("00000016", DisplayFormatter.FormatOffset(16, NumericBase.Decimal));
        Equal("100000000", DisplayFormatter.FormatOffset(0x100000000, NumericBase.Hexadecimal));
    }

    private static void BitRulers()
    {
        var settings = Settings(2);
        settings.ViewMode = DataViewMode.Bits;
        Equal("76543210 76543210", DisplayFormatter.GetRuler(settings));
        Equal("10110011", DisplayFormatter.FormatBits(0xB3, BitNumbering.LsbZero));
        settings.BitNumbering = BitNumbering.MsbZero;
        Equal("01234567 01234567", DisplayFormatter.GetRuler(settings));
        Equal("10110011", DisplayFormatter.FormatBits(0xB3, BitNumbering.MsbZero));
        Equal("00000001", DisplayFormatter.FormatBits(1, BitNumbering.MsbZero));
        settings.ViewMode = DataViewMode.Bytes;
        Equal("00 01", DisplayFormatter.GetRuler(settings));
        settings.ByteBase = NumericBase.Decimal;
        Equal("000 001", DisplayFormatter.GetRuler(settings));
    }

    private static void HexExport()
    {
        var document = new DocumentModel(new byte[] { 0xFF, 0xF0, 0xA1, 0x48, 0x69 });
        var settings = Settings(3);
        settings.ShowRuler = true;
        Equal("              00 01 02\n  00000000    FF F0 A1    ...\n  00000003    48 69       Hi\n", Export(document, settings));
        var row = DisplayFormatter.FormatRow(document, 3, settings);
        Equal("00000003", row.Offset);
        Equal("48 69   ", row.Data);
        Equal("Hi", row.Ascii);
    }

    private static void DecimalExport()
    {
        var document = new DocumentModel(new byte[] { 0xFF, 0xF0, 0xA1, 0x48, 0x69 });
        var settings = Settings(3);
        settings.ByteBase = NumericBase.Decimal;
        settings.OffsetBase = NumericBase.Decimal;
        Equal("  00000000    255 240 161    ...\n  00000003    072 105        Hi\n", Export(document, settings));
        settings.BytesPerRow = 16;
        settings.ShowAscii = false;
        var offsets = new DocumentModel(new byte[17]);
        var lastRow = DisplayFormatter.FormatRow(offsets, 16, settings);
        Equal("00000016", lastRow.Offset);
        Equal("000", lastRow.Data);
    }

    private static void BitExport()
    {
        var document = new DocumentModel(new byte[] { 0xB3, 0x6C, 1 });
        var settings = Settings(2);
        settings.ViewMode = DataViewMode.Bits;
        settings.ShowOffsets = false;
        settings.ShowAscii = false;
        settings.ShowRuler = true;
        Equal("  76543210 76543210\n  10110011 01101100\n  00000001\n", Export(document, settings));
        settings.BitNumbering = BitNumbering.MsbZero;
        Equal("  01234567 01234567\n  10110011 01101100\n  00000001\n", Export(document, settings));
    }

    private static void DataOnlyExport()
    {
        var document = new DocumentModel(new byte[] { 0xFF, 0xF0, 0xA1, 0x48, 0x69 });
        document.AddField(Field("Flag", 0));
        var settings = Settings(3);
        settings.ShowOffsets = false;
        settings.ShowAscii = false;
        Equal("  FF F0 A1\n  48 69\n", Export(document, settings));
    }

    private static void PartialRowAnnotations()
    {
        var document = new DocumentModel(new byte[] { 0x41, 0x7F, 0x20 });
        document.AddField(Field("Crossing", 15, 16));
        document.AddField(Field("Last bit", 16));
        var settings = Settings(2);
        settings.ShowAnnotations = true;
        settings.DataWidth = 240;
        Equal("  00000000    41 7F" + new string(' ', 25) + "A.\n" +
            "              [Crossing]\n" +
            "  00000002    20" + new string(' ', 29) + "\n" +
            "              [Crossing] [Last bit]\n", Export(document, settings));
        var row = DisplayFormatter.FormatRow(document, 2, settings);
        Equal("20   ", row.Data);
        Equal(" ", row.Ascii);
        Equal("[Crossing] [Last bit]", row.Annotations);
    }

    private static void EmptyExport()
    {
        var settings = Settings(3);
        Equal(string.Empty, Export(new DocumentModel(), settings));
        settings.ShowRuler = true;
        Equal("              00 01 02\n", Export(new DocumentModel(), settings));
    }

    private static void InvalidFormatting()
    {
        var settings = Settings(0);
        using var writer = new StringWriter();
        Throws<InvalidDataException>(() => DisplayFormatter.Export(writer, Sample(), settings));
        Equal(string.Empty, writer.ToString());
        settings.BytesPerRow = 1;
        settings.DataWidth = double.NaN;
        Throws<InvalidDataException>(() => DisplayFormatter.FormatRow(Sample(), 0, settings));
    }

    private static void ResizedColumns()
    {
        var document = new DocumentModel(new byte[] { 0x41, 0x42, 0x43 });
        var settings = Settings(2);
        settings.OffsetWidth = 128;
        settings.DataWidth = 160;
        settings.AsciiWidth = 80;
        settings.ShowRuler = true;
        var widths = DisplayFormatter.GetColumnCharacterWidths(document, settings);
        Equal(16, widths.Offset);
        Equal(20, widths.Data);
        Equal(10, widths.Ascii);
        var lines = Export(document, settings).Split('\n');
        Equal(18, lines[0].IndexOf("00 01", StringComparison.Ordinal));
        Equal(18, lines[1].IndexOf("41 42", StringComparison.Ordinal));
        Equal(38, lines[1].IndexOf("AB", StringComparison.Ordinal));
        Equal(38, lines[2].IndexOf('C'));
        settings.OffsetWidth = 200;
        settings.DataWidth = 243;
        lines = Export(document, settings).Split('\n');
        Equal(27, lines[1].IndexOf("41 42", StringComparison.Ordinal));
        Equal(58, lines[1].IndexOf("AB", StringComparison.Ordinal));
        Equal(58, lines[2].IndexOf('C'));
    }

    private static void NarrowColumns()
    {
        var document = new DocumentModel(Enumerable.Repeat((byte)0x41, 16).ToArray());
        var settings = Settings(16);
        settings.OffsetWidth = settings.DataWidth = settings.AsciiWidth = 24;
        var widths = DisplayFormatter.GetColumnCharacterWidths(document, settings);
        Equal(12, widths.Offset);
        Equal(51, widths.Data);
        Equal(20, widths.Ascii);
        string line = Export(document, settings).TrimEnd('\n');
        Equal("00000000", line.Substring(2, 8));
        Equal(string.Join(' ', Enumerable.Repeat("41", 16)), line.Substring(14, 47));
        Equal(new string('A', 16), line[65..]);
    }

    private static void AnnotationRows()
    {
        var document = new DocumentModel(new byte[] { 0x41, 0x42, 0x43 });
        document.AddField(Field("Crossing", 0));
        var settings = Settings(2);
        settings.ShowAnnotations = true;
        Equal("[Crossi…", DisplayFormatter.FormatRow(document, 0, settings).Annotations);
        Equal("  00000000    41 42       AB\n              [Crossi…\n  00000002    43          C\n\n", Export(document, settings));
        var unicode = new DocumentModel(new byte[] { 0x41 });
        unicode.AddField(Field("12345😀abc", 0));
        Equal("[12345…", DisplayFormatter.FormatRow(unicode, 0, settings).Annotations);
    }

    private static DisplaySettings Settings(int bytesPerRow) => new()
    {
        BytesPerRow = bytesPerRow,
        ShowRuler = false,
        ShowAnnotations = false,
        OffsetWidth = 96,
        DataWidth = 96,
        AsciiWidth = 56
    };

    private static string Export(DocumentModel document, DisplaySettings settings)
    {
        using var writer = new StringWriter { NewLine = "\n" };
        DisplayFormatter.Export(writer, document, settings);
        return writer.ToString();
    }

    private static DocumentModel Sample() => new(new byte[] { 0xB3, 0x6C });

    private static NamedField Field(string name, params long[] orderedBits) => new()
    {
        Name = name,
        OrderedBits = orderedBits.ToList(),
        Color = "#78A9FF",
        Notes = string.Empty
    };

    private static string Artifact(string name) => Path.Combine(ArtifactDirectory, name);

    private static void Test(string name, Action action)
    {
        try
        {
            action();
            _passed++;
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception exception)
        {
            _failed++;
            Console.Error.WriteLine($"FAIL {name}: {exception}");
        }
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected <{expected}>, got <{actual}>.");
    }

    private static void Sequence<T>(IEnumerable<T> expected, IEnumerable<T> actual)
    {
        if (!expected.SequenceEqual(actual))
            throw new InvalidOperationException($"Expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}].");
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
