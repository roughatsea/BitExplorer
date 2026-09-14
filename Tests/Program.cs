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
// Make names from System.Text.Json.Nodes available here without writing their full prefix each time. This
// does not run that library's code.
using System.Text.Json.Nodes;
// Make names from BitExplorer.Core available here without writing their full prefix each time. This does
// not run that library's code.
using BitExplorer.Core;

// Run with: dotnet run --project Tests/BitExplorer.Tests.csproj
// No test framework or downloaded packages are required.
/// <summary>A console-based regression suite for the data model and exact text formatting.</summary>
/// <remarks>
/// Each test arranges a small known input, performs an operation, and checks the
/// observable result. The helpers at the bottom throw when an expectation fails.
/// "partial" lets BitSelectionTests.cs add tests to this same Program class.
/// </remarks>
internal static partial class Program
{
    // Keep running after individual failures so one run reports all broken scenarios.
    private static int _passed;
    // Reserve _failed to hold a whole number; setup can supply its value, otherwise the type's default is
    // used.
    private static int _failed;
    // Tests own only this per-run directory, underneath their compiled output.
    // A random name prevents separate runs from overwriting one another's fixtures.
    private static readonly string ArtifactRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "test-artifacts"));
    // Remember the result returned by Path.Combine(...) as ArtifactDirectory.
    private static readonly string ArtifactDirectory = Path.Combine(ArtifactRoot, Guid.NewGuid().ToString("N"));

    /// <summary>Runs all checks, cleans up temporary fixtures, and returns an automation-friendly exit code.</summary>
    /// <returns>Zero when everything passed; one when at least one test failed.</returns>
    private static int Main()
    {
        // Call Directory.CreateDirectory(...); the values in parentheses are the inputs.
        Directory.CreateDirectory(ArtifactDirectory);
        // Attempt this work. A matching catch below handles a reported exception; a finally block, when
        // present, performs cleanup on the way out.
        try
        {
            // Run ConstructorOwnsData as a test; the quoted text names the behavior reported in the
            // results.
            Test("constructing a document owns its input bytes", ConstructorOwnsData);
            // Run BitAddressing as a test; the quoted text names the behavior reported in the results.
            Test("bit addressing uses the physical MSB-first byte layout", BitAddressing);
            // Run CrossByteField as a test; the quoted text names the behavior reported in the results.
            Test("a cross-byte field reads the expected packed value", CrossByteField);
            // Run FieldWritePreservesOtherBits as a test; the quoted text names the behavior reported in
            // the results.
            Test("field writes preserve every bit outside the selection", FieldWritePreservesOtherBits);
            // Run DiscontiguousFieldOrder as a test; the quoted text names the behavior reported in the
            // results.
            Test("discontiguous field values follow the specified assembly order", DiscontiguousFieldOrder);
            // Run WideAndLittleEndianFields as a test; the quoted text names the behavior reported in the
            // results.
            Test("field assembly supports little-endian bytes and values wider than 64 bits", WideAndLittleEndianFields);
            // Run FieldEditUndoRedo as a test; the quoted text names the behavior reported in the results.
            Test("field edits undo and redo as one operation", FieldEditUndoRedo);
            // Run ByteEditHistory as a test; the quoted text names the behavior reported in the results.
            Test("byte edit history branches and modified markers recover", ByteEditHistory);
            // Run InvalidFieldValues as a test; the quoted text names the behavior reported in the results.
            Test("invalid numeric field edits are rejected without mutation", InvalidFieldValues);
            // Run InvalidFieldDefinitions as a test; the quoted text names the behavior reported in the
            // results.
            Test("duplicate, negative, outside-file, and excessive field bits are rejected", InvalidFieldDefinitions);
            // Run FieldMetadataHistory as a test; the quoted text names the behavior reported in the
            // results.
            Test("field metadata edits participate in undo and redo", FieldMetadataHistory);
            // Run BinaryRoundTrip as a test; the quoted text names the behavior reported in the results.
            Test("binary save preserves exact bytes and clears binary modifications", BinaryRoundTrip);
            // Run EmptyBinaryRoundTrip as a test; the quoted text names the behavior reported in the
            // results.
            Test("empty binary documents round trip", EmptyBinaryRoundTrip);
            // Run ProjectRoundTrip as a test; the quoted text names the behavior reported in the results.
            Test("projects restore bytes and discontiguous field metadata", ProjectRoundTrip);
            // Run TemplateRoundTrip as a test; the quoted text names the behavior reported in the results.
            Test("templates transfer field definitions without changing destination bytes", TemplateRoundTrip);
            // Run InvalidTemplateIsAtomic as a test; the quoted text names the behavior reported in the
            // results.
            Test("an out-of-range template is rejected atomically", InvalidTemplateIsAtomic);
            // Run DuplicateTemplateBits as a test; the quoted text names the behavior reported in the
            // results.
            Test("duplicate persisted bit references are rejected without replacing existing fields", DuplicateTemplateBits);
            // Run InvalidProjectMetadata as a test; the quoted text names the behavior reported in the
            // results.
            Test("unsupported project versions and invalid display settings are rejected", InvalidProjectMetadata);
            // Run MalformedPersistence as a test; the quoted text names the behavior reported in the
            // results.
            Test("malformed projects and templates are rejected", MalformedPersistence);
            // Call RunFormattingTests: Registers the output-format checks with the same runner used for
            // data-model tests.
            RunFormattingTests();
            // Call RunSelectionTests: Registers independent selection scenarios alongside the existing core
            // regression tests.
            RunSelectionTests();
        }
        // Run this cleanup when leaving the try, whether the work succeeded or reported an exception.
        finally
        {
            // finally executes even if an unexpected exception escapes a test.
            // Normalize and check containment before recursive deletion: only the
            // unique directory created by this run may be removed.
            // Only delete the unique directory created by this process inside our test output.
            var normalizedDirectory = Path.GetFullPath(ArtifactDirectory);
            // If both normalizedDirectory.StartsWith(ArtifactRoot + Path.DirectorySeparatorChar,
            // StringComparison.OrdinalIgnoreCase) is true and Directory.Exists(normalizedDirectory) is
            // true, run the following grouped instructions.
            if (normalizedDirectory.StartsWith(ArtifactRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(normalizedDirectory))
            {
                // Call Directory.Delete(...); the values in parentheses are the inputs.
                Directory.Delete(normalizedDirectory, recursive: true);
            }
        }

        // Write the supplied text followed by a line break to Console.
        Console.WriteLine($"\n{_passed} passed, {_failed} failed.");
        // Return 0 when _failed equals 0; otherwise 1 to the caller and leave this method.
        return _failed == 0 ? 0 : 1;
    }

    /// <summary>Mutating the input array after construction must not alter the document or its baseline.</summary>
    private static void ConstructorOwnsData()
    {
        // Remember a new array of byte values; the brackets specify its size and braces supply its starting
        // values as source.
        var source = new byte[] { 0xB3, 0x6C };
        // Remember a new DocumentModel object using the inputs in parentheses as document.
        var document = new DocumentModel(source);
        // Set source[0] to 0.
        source[0] = 0;
        // Check that document.Data has the same items in the same order as new byte[] { 0xB3, 0x6C }.
        Sequence(new byte[] { 0xB3, 0x6C }, document.Data);
        // Check that document.CanUndo equals the expected false; a mismatch fails this test.
        Equal(false, document.CanUndo);
        // Check that document.IsByteModified(0) equals the expected false; a mismatch fails this test.
        Equal(false, document.IsByteModified(0));
    }

    /// <summary>Flips each byte's endpoint bits; 128 + 1 must produce hex 81 in both bytes.</summary>
    private static void BitAddressing()
    {
        // Remember a new DocumentModel object using the inputs in parentheses as document.
        var document = new DocumentModel(new byte[] { 0, 0 });
        // Ask the document to invert the bit at the supplied physical file address as an undoable edit.
        document.FlipBit(0);
        // Ask the document to invert the bit at the supplied physical file address as an undoable edit.
        document.FlipBit(7);
        // Ask the document to invert the bit at the supplied physical file address as an undoable edit.
        document.FlipBit(8);
        // Ask the document to invert the bit at the supplied physical file address as an undoable edit.
        document.FlipBit(15);
        // Check that document.Data has the same items in the same order as new byte[] { 0x81, 0x81 }.
        Sequence(new byte[] { 0x81, 0x81 }, document.Data);
        // Remember a separate array containing document.Data's items as before.
        var before = document.Data.ToArray();
        // Check that the supplied operation reports the expected exception type; accepting this invalid
        // case would fail the test.
        Throws<ArgumentException>(() => document.FlipBit(-1));
        // Check that the supplied operation reports the expected exception type; accepting this invalid
        // case would fail the test.
        Throws<ArgumentException>(() => document.FlipBit(16));
        // Check that document.Data has the same items in the same order as before.
        Sequence(before, document.Data);
    }

    /// <summary>Reads six bits crossing the B3/6C boundary and expects binary 011011, decimal 27.</summary>
    private static void CrossByteField()
    {
        // Remember the result returned by Sample() as document.
        var document = Sample();
        // Remember the result returned by Field(...) as field.
        var field = Field("Packed", 5, 6, 7, 8, 9, 10);
        // Call document.AddField: Adds a validated, independently copied field in one undo step; IDs must
        // be unique.
        document.AddField(field);
        // Check that document.ReadField(field) equals the expected new BigInteger(27); a mismatch fails
        // this test.
        Equal(new BigInteger(27), document.ReadField(field));
    }

    /// <summary>Writes a crossing field and checks exact destination bytes, proving unrelated bits survived.</summary>
    private static void FieldWritePreservesOtherBits()
    {
        // Remember the result returned by Sample() as document.
        var document = Sample();
        // Remember the result returned by Field(...) as field.
        var field = Field("Packed", 5, 6, 7, 8, 9, 10);
        // Call document.AddField: Adds a validated, independently copied field in one undo step; IDs must
        // be unique.
        document.AddField(field);
        // Call document.SetFieldValue: Writes an unsigned field value as one undoable change, preserving
        // all bits outside the field.
        document.SetFieldValue(field, 42); // 101010 replaces only the six selected bits.
        // Check that document.Data has the same items in the same order as new byte[] { 0xB5, 0x4C }.
        Sequence(new byte[] { 0xB5, 0x4C }, document.Data);
        // Check that document.ReadField(field) equals the expected new BigInteger(42); a mismatch fails
        // this test.
        Equal(new BigInteger(42), document.ReadField(field));
    }

    /// <summary>The same scattered physical bits must produce different values when assembled in reverse order.</summary>
    private static void DiscontiguousFieldOrder()
    {
        // Remember the result returned by Sample() as document.
        var document = Sample();
        // Remember the result returned by Field(...) as field.
        var field = Field("Scattered", 0, 9, 3, 14);
        // Call document.AddField: Adds a validated, independently copied field in one undo step; IDs must
        // be unique.
        document.AddField(field);
        // Check that document.ReadField(field) equals the expected new BigInteger(14); a mismatch fails
        // this test.
        Equal(new BigInteger(14), document.ReadField(field));
        // Call document.SetFieldValue: Writes an unsigned field value as one undoable change, preserving
        // all bits outside the field.
        document.SetFieldValue(field, 5); // 0, 1, 0, 1 in the explicitly supplied order.
        // Check that document.Data has the same items in the same order as new byte[] { 0x23, 0x6E }.
        Sequence(new byte[] { 0x23, 0x6E }, document.Data);
        // Check that document.ReadField(field) equals the expected new BigInteger(5); a mismatch fails this
        // test.
        Equal(new BigInteger(5), document.ReadField(field));

        // Remember the result returned by Field(...) as reversed.
        var reversed = Field("Reverse assembly", 14, 3, 9, 0);
        // Call document.AddField: Adds a validated, independently copied field in one undo step; IDs must
        // be unique.
        document.AddField(reversed);
        // Check that document.ReadField(reversed) equals the expected new BigInteger(10); a mismatch fails
        // this test.
        Equal(new BigInteger(10), document.ReadField(reversed));
    }

    /// <summary>A field-value write spanning two bytes must undo as one edit, leaving the earlier label creation intact.</summary>
    private static void FieldEditUndoRedo()
    {
        // Remember the result returned by Sample() as document.
        var document = Sample();
        // Remember the result returned by Field(...) as field.
        var field = Field("Packed", 5, 6, 7, 8, 9, 10);
        // Call document.AddField: Adds a validated, independently copied field in one undo step; IDs must
        // be unique.
        document.AddField(field);
        // Call document.SetFieldValue: Writes an unsigned field value as one undoable change, preserving
        // all bits outside the field.
        document.SetFieldValue(field, 42);
        // Check that document.IsByteModified(0) equals the expected true; a mismatch fails this test.
        Equal(true, document.IsByteModified(0));
        // Check that document.IsByteModified(1) equals the expected true; a mismatch fails this test.
        Equal(true, document.IsByteModified(1));
        // Restore the state before the most recent undoable edit.
        document.Undo();
        // Check that document.Data has the same items in the same order as new byte[] { 0xB3, 0x6C }.
        Sequence(new byte[] { 0xB3, 0x6C }, document.Data);
        // Check that document.Fields.Count equals the expected 1; a mismatch fails this test.
        Equal(1, document.Fields.Count);
        // Check that document.IsByteModified(0) equals the expected false; a mismatch fails this test.
        Equal(false, document.IsByteModified(0));
        // Check that document.IsByteModified(1) equals the expected false; a mismatch fails this test.
        Equal(false, document.IsByteModified(1));
        // Check that document.CanRedo equals the expected true; a mismatch fails this test.
        Equal(true, document.CanRedo);
        // Reapply the most recently undone edit.
        document.Redo();
        // Check that document.Data has the same items in the same order as new byte[] { 0xB5, 0x4C }.
        Sequence(new byte[] { 0xB5, 0x4C }, document.Data);
    }

    /// <summary>Checks reversed byte order and a 72-bit field, including untouched bits outside its endpoints.</summary>
    private static void WideAndLittleEndianFields()
    {
        // Remember the result returned by Sample() as document.
        var document = Sample();
        // Range(start, count) generates addresses. Concatenating byte 1 before
        // byte 0 makes a little-endian interpretation without swapping stored data.
        var littleEndian = Field("LE word", Enumerable.Range(8, 8).Concat(Enumerable.Range(0, 8)).Select(i => (long)i).ToArray());
        // Check that document.ReadField(littleEndian) equals the expected new BigInteger(0x6CB3); a
        // mismatch fails this test.
        Equal(new BigInteger(0x6CB3), document.ReadField(littleEndian));
        // Call document.SetFieldValue: Writes an unsigned field value as one undoable change, preserving
        // all bits outside the field.
        document.SetFieldValue(littleEndian, 0x1234);
        // Check that document.Data has the same items in the same order as new byte[] { 0x34, 0x12 }.
        Sequence(new byte[] { 0x34, 0x12 }, document.Data);

        // Remember a new DocumentModel object using the inputs in parentheses as wide.
        var wide = new DocumentModel(new byte[10]);
        // Remember the result returned by Field(...) as field.
        var field = Field("72-bit value", Enumerable.Range(3, 72).Select(i => (long)i).ToArray());
        // Set widely separated bits, including one beyond a 64-bit integer's
        // range. OR combines those independent powers of two into one value.
        var value = (BigInteger.One << 71) | (BigInteger.One << 37) | 5;
        // Call wide.SetFieldValue: Writes an unsigned field value as one undoable change, preserving all
        // bits outside the field.
        wide.SetFieldValue(field, value);
        // Check that wide.ReadField(field) equals the expected value; a mismatch fails this test.
        Equal(value, wide.ReadField(field));
        // These masks select the three untouched leading bits and five untouched
        // trailing bits. A zero result proves the field write did not leak outside.
        Equal(0, wide.Data[0] & 0xE0);
        // Check that wide.Data[9] & 0x1F equals the expected 0; a mismatch fails this test.
        Equal(0, wide.Data[9] & 0x1F);
        // Restore the state before the most recent undoable edit.
        wide.Undo();
        // Check that wide.Data has the same items in the same order as new byte[10].
        Sequence(new byte[10], wide.Data);
    }

    /// <summary>Undo followed by a different edit must discard the old redo branch and keep byte highlights accurate.</summary>
    private static void ByteEditHistory()
    {
        // Remember the result returned by Sample() as document.
        var document = Sample();
        // Ask the document to write the supplied value at the supplied byte offset, recording an undoable
        // edit if it changes.
        document.SetByte(0, 0x11);
        // Check that document.IsBinaryDirty equals the expected true; a mismatch fails this test.
        Equal(true, document.IsBinaryDirty);
        // Restore the state before the most recent undoable edit.
        document.Undo();
        // Check that document.Data[0] equals the expected (byte)0xB3; a mismatch fails this test.
        Equal((byte)0xB3, document.Data[0]);
        // Check that document.IsByteModified(0) equals the expected false; a mismatch fails this test.
        Equal(false, document.IsByteModified(0));
        // Check that document.CanRedo equals the expected true; a mismatch fails this test.
        Equal(true, document.CanRedo);
        // Ask the document to write the supplied value at the supplied byte offset, recording an undoable
        // edit if it changes.
        document.SetByte(1, 0x22);
        // Check that document.CanRedo equals the expected false; a mismatch fails this test.
        Equal(false, document.CanRedo);
        // Restore the state before the most recent undoable edit.
        document.Undo();
        // Check that document.Data has the same items in the same order as new byte[] { 0xB3, 0x6C }.
        Sequence(new byte[] { 0xB3, 0x6C }, document.Data);
        // Reapply the most recently undone edit.
        document.Redo();
        // Check that document.Data has the same items in the same order as new byte[] { 0xB3, 0x22 }.
        Sequence(new byte[] { 0xB3, 0x22 }, document.Data);
    }

    /// <summary>A three-bit unsigned field rejects -1 and 8 without changing bytes or adding undo entries.</summary>
    private static void InvalidFieldValues()
    {
        // Remember the result returned by Sample() as document.
        var document = Sample();
        // Remember the result returned by Field(...) as field.
        var field = Field("Small", 5, 6, 7);
        // Call document.AddField: Adds a validated, independently copied field in one undo step; IDs must
        // be unique.
        document.AddField(field);
        // Check that the supplied operation reports the expected exception type; accepting this invalid
        // case would fail the test.
        Throws<ArgumentOutOfRangeException>(() => document.SetFieldValue(field, -1));
        // Check that the supplied operation reports the expected exception type; accepting this invalid
        // case would fail the test.
        Throws<ArgumentOutOfRangeException>(() => document.SetFieldValue(field, 8));
        // Check that document.Data has the same items in the same order as new byte[] { 0xB3, 0x6C }.
        Sequence(new byte[] { 0xB3, 0x6C }, document.Data);
        // Restore the state before the most recent undoable edit.
        document.Undo();
        // Check that document.Fields.Count equals the expected 0; a mismatch fails this test.
        Equal(0, document.Fields.Count); // rejected writes did not enter history.
    }

    /// <summary>Checks duplicate, negative, out-of-file, and over-limit source lists before any field is installed.</summary>
    private static void InvalidFieldDefinitions()
    {
        // Remember the result returned by Sample() as document.
        var document = Sample();
        // Check that the supplied operation reports the expected exception type; accepting this invalid
        // case would fail the test.
        Throws<ArgumentException>(() => document.AddField(Field("Duplicate", 1, 1)));
        // Check that the supplied operation reports the expected exception type; accepting this invalid
        // case would fail the test.
        Throws<ArgumentException>(() => document.AddField(Field("Negative", -1)));
        // Check that the supplied operation reports the expected exception type; accepting this invalid
        // case would fail the test.
        Throws<ArgumentException>(() => document.AddField(Field("Outside", 16)));
        // Remember a new DocumentModel object using the inputs in parentheses as wide.
        var wide = new DocumentModel(new byte[513]);
        // Check that the supplied operation reports the expected exception type; accepting this invalid
        // case would fail the test.
        Throws<ArgumentException>(() => wide.AddField(Field("Too wide", Enumerable.Range(0, 4097).Select(i => (long)i).ToArray())));
        // Check that document.Fields.Count equals the expected 0; a mismatch fails this test.
        Equal(0, document.Fields.Count);
        // Check that document.CanUndo equals the expected false; a mismatch fails this test.
        Equal(false, document.CanUndo);
    }

    /// <summary>Renaming, changing notes, and deleting labels must all be reversible without losing stable field identity.</summary>
    private static void FieldMetadataHistory()
    {
        // Remember the result returned by Sample() as document.
        var document = Sample();
        // Remember the result returned by Field(...) as initial.
        var initial = Field("Flags", 0, 3, 7);
        // Call document.AddField: Adds a validated, independently copied field in one undo step; IDs must
        // be unique.
        document.AddField(initial);
        // Remember the result returned by Field(...) as updated.
        var updated = Field("Status", 0, 3, 7);
        // Set updated.Id to initial.Id.
        updated.Id = initial.Id;
        // Set updated.Notes to the text "Three scattered status bits".
        updated.Notes = "Three scattered status bits";
        // Call document.UpdateField: Replaces the matching field from an edited draft while preserving its
        // original state for Undo.
        document.UpdateField(updated);
        // Check that document.Fields.Single().Name equals the expected "Status"; a mismatch fails this
        // test.
        Equal("Status", document.Fields.Single().Name);
        // Restore the state before the most recent undoable edit.
        document.Undo();
        // Check that document.Fields.Single().Name equals the expected "Flags"; a mismatch fails this test.
        Equal("Flags", document.Fields.Single().Name);
        // Reapply the most recently undone edit.
        document.Redo();
        // Check that document.Fields.Single().Notes equals the expected "Three scattered status bits"; a
        // mismatch fails this test.
        Equal("Three scattered status bits", document.Fields.Single().Notes);
        // Call document.RemoveField: Removes a label without changing any bytes; an unknown ID is a no-op.
        document.RemoveField(initial.Id);
        // Check that document.Fields.Count equals the expected 0; a mismatch fails this test.
        Equal(0, document.Fields.Count);
        // Restore the state before the most recent undoable edit.
        document.Undo();
        // Check that document.Fields.Single().Name equals the expected "Status"; a mismatch fails this
        // test.
        Equal("Status", document.Fields.Single().Name);
        // Reapply the most recently undone edit.
        document.Redo();
        // Check that document.Fields.Count equals the expected 0; a mismatch fails this test.
        Equal(0, document.Fields.Count);
    }

    /// <summary>Saves and overwrites a binary, reopens exact bytes, then checks Undo against the new saved baseline.</summary>
    private static void BinaryRoundTrip()
    {
        // Remember the result returned by Sample() as document.
        var document = Sample();
        // Ask the document to write the supplied value at the supplied byte offset, recording an undoable
        // edit if it changes.
        document.SetByte(0, 0xFF);
        // Remember the result returned by Artifact(...) as path.
        var path = Artifact("edited.bin");
        // Call document.SaveBinary: Writes only raw bytes, then updates the binary baseline if the write
        // succeeds.
        document.SaveBinary(path);
        // Call document.SaveBinary: Writes only raw bytes, then updates the binary baseline if the write
        // succeeds.
        document.SaveBinary(path); // exercising an atomic overwrite of an existing file.
        // Check that File.ReadAllBytes(path) has the same items in the same order as new byte[] { 0xFF,
        // 0x6C }.
        Sequence(new byte[] { 0xFF, 0x6C }, File.ReadAllBytes(path));
        // Check that document.IsBinaryDirty equals the expected false; a mismatch fails this test.
        Equal(false, document.IsBinaryDirty);
        // Check that document.IsByteModified(0) equals the expected false; a mismatch fails this test.
        Equal(false, document.IsByteModified(0));
        // Remember the result returned by DocumentModel.OpenBinary(...) as reopened.
        var reopened = DocumentModel.OpenBinary(path);
        // Check that reopened.Data has the same items in the same order as document.Data.
        Sequence(document.Data, reopened.Data);
        // Restore the state before the most recent undoable edit.
        document.Undo();
        // Check that document.IsByteModified(0) equals the expected true; a mismatch fails this test.
        Equal(true, document.IsByteModified(0)); // undo is measured against the newly saved bytes.
    }

    /// <summary>A zero-byte file is valid data and must reopen without invented bytes or history.</summary>
    private static void EmptyBinaryRoundTrip()
    {
        // Remember the result returned by Artifact(...) as path.
        var path = Artifact("empty.bin");
        // Call new DocumentModel().SaveBinary: Writes only raw bytes, then updates the binary baseline if
        // the write succeeds.
        new DocumentModel().SaveBinary(path);
        // Check that File.ReadAllBytes(path).Length equals the expected 0; a mismatch fails this test.
        Equal(0, File.ReadAllBytes(path).Length);
        // Remember the result returned by DocumentModel.OpenBinary(...) as reopened.
        var reopened = DocumentModel.OpenBinary(path);
        // Check that reopened.Data.Length equals the expected 0; a mismatch fails this test.
        Equal(0, reopened.Data.Length);
        // Check that reopened.CanUndo equals the expected false; a mismatch fails this test.
        Equal(false, reopened.CanUndo);
    }

    /// <summary>Saves a richly configured project and verifies bytes, ordered fields, Unicode notes, and every changed setting.</summary>
    private static void ProjectRoundTrip()
    {
        // Remember the result returned by Sample() as document.
        var document = Sample();
        // Remember the result returned by Field(...) as field.
        var field = Field("Message kind", 0, 9, 3, 14);
        // Set field.Notes to the text "Unicode notes: µ / λ / payload".
        field.Notes = "Unicode notes: µ / λ / payload";
        // Set field.Color to the text "#63C9A7".
        field.Color = "#63C9A7";
        // Call document.AddField: Adds a validated, independently copied field in one undo step; IDs must
        // be unique.
        document.AddField(field);
        // Call document.SetFieldValue: Writes an unsigned field value as one undoable change, preserving
        // all bits outside the field.
        document.SetFieldValue(field, 5);
        // Set document.Settings.ViewMode to DataViewMode.Bits.
        document.Settings.ViewMode = DataViewMode.Bits;
        // Set document.Settings.ByteBase to NumericBase.Decimal.
        document.Settings.ByteBase = NumericBase.Decimal;
        // Set document.Settings.OffsetBase to NumericBase.Decimal.
        document.Settings.OffsetBase = NumericBase.Decimal;
        // Set document.Settings.BitNumbering to BitNumbering.MsbZero.
        document.Settings.BitNumbering = BitNumbering.MsbZero;
        // Set document.Settings.BytesPerRow to 7.
        document.Settings.BytesPerRow = 7;
        // Set document.Settings.AutoBytesPerRow to true (yes).
        document.Settings.AutoBytesPerRow = true;
        // Set document.Settings.OffsetWidth to 144.
        document.Settings.OffsetWidth = 144;
        // Set document.Settings.DataWidth to 920.
        document.Settings.DataWidth = 920;
        // Set document.Settings.AsciiWidth to 224.
        document.Settings.AsciiWidth = 224;
        // Set document.Settings.CharacterWidth to 7.5.
        document.Settings.CharacterWidth = 7.5;
        // Set document.Settings.ShowAnnotations to false (no).
        document.Settings.ShowAnnotations = false;
        // Set document.Settings.ShowRuler to false (no).
        document.Settings.ShowRuler = false;
        // Call document.NotifySettingsChanged: Validates display settings and asks observers to refresh;
        // settings are not undo commands.
        document.NotifySettingsChanged();
        // Remember the result returned by Artifact(...) as path.
        var path = Artifact("sample.bitexplorer.json");
        // Call document.SaveProject: Saves edited bytes, display settings, and field definitions together
        // in versioned JSON.
        document.SaveProject(path);
        // A round trip means write the object, read a new object, then compare
        // observable values. It catches persistence omissions that in-memory tests miss.
        var reopened = DocumentModel.OpenProject(path);
        // Check that reopened.Data has the same items in the same order as document.Data.
        Sequence(document.Data, reopened.Data);
        // Check that reopened.Fields.Count equals the expected 1; a mismatch fails this test.
        Equal(1, reopened.Fields.Count);
        // Remember reopened.Fields[0] as restored.
        var restored = reopened.Fields[0];
        // Check that restored.Id equals the expected field.Id; a mismatch fails this test.
        Equal(field.Id, restored.Id);
        // Check that restored.Name equals the expected field.Name; a mismatch fails this test.
        Equal(field.Name, restored.Name);
        // Check that restored.Notes equals the expected field.Notes; a mismatch fails this test.
        Equal(field.Notes, restored.Notes);
        // Check that restored.Color equals the expected field.Color; a mismatch fails this test.
        Equal(field.Color, restored.Color);
        // Check that restored.OrderedBits has the same items in the same order as field.OrderedBits.
        Sequence(field.OrderedBits, restored.OrderedBits);
        // Check that reopened.ReadField(restored) equals the expected new BigInteger(5); a mismatch fails
        // this test.
        Equal(new BigInteger(5), reopened.ReadField(restored));
        // Check that reopened.Settings.ViewMode equals the expected DataViewMode.Bits; a mismatch fails
        // this test.
        Equal(DataViewMode.Bits, reopened.Settings.ViewMode);
        // Check that reopened.Settings.ByteBase equals the expected NumericBase.Decimal; a mismatch fails
        // this test.
        Equal(NumericBase.Decimal, reopened.Settings.ByteBase);
        // Check that reopened.Settings.OffsetBase equals the expected NumericBase.Decimal; a mismatch fails
        // this test.
        Equal(NumericBase.Decimal, reopened.Settings.OffsetBase);
        // Check that reopened.Settings.BitNumbering equals the expected BitNumbering.MsbZero; a mismatch
        // fails this test.
        Equal(BitNumbering.MsbZero, reopened.Settings.BitNumbering);
        // Check that reopened.Settings.BytesPerRow equals the expected 7; a mismatch fails this test.
        Equal(7, reopened.Settings.BytesPerRow);
        // Check that reopened.Settings.AutoBytesPerRow equals the expected true; a mismatch fails this
        // test.
        Equal(true, reopened.Settings.AutoBytesPerRow);
        // Check that reopened.Settings.OffsetWidth equals the expected 144d; a mismatch fails this test.
        Equal(144d, reopened.Settings.OffsetWidth);
        // Check that reopened.Settings.DataWidth equals the expected 920d; a mismatch fails this test.
        Equal(920d, reopened.Settings.DataWidth);
        // Check that reopened.Settings.AsciiWidth equals the expected 224d; a mismatch fails this test.
        Equal(224d, reopened.Settings.AsciiWidth);
        // Check that reopened.Settings.CharacterWidth equals the expected 7.5d; a mismatch fails this test.
        Equal(7.5d, reopened.Settings.CharacterWidth);
        // Check that reopened.Settings.ShowAnnotations equals the expected false; a mismatch fails this
        // test.
        Equal(false, reopened.Settings.ShowAnnotations);
        // Check that reopened.Settings.ShowRuler equals the expected false; a mismatch fails this test.
        Equal(false, reopened.Settings.ShowRuler);
        // Check that reopened.IsProjectDirty equals the expected false; a mismatch fails this test.
        Equal(false, reopened.IsProjectDirty);
        // Check that reopened.CanUndo equals the expected false; a mismatch fails this test.
        Equal(false, reopened.CanUndo);
    }

    /// <summary>Transfers a field layout to different bytes; field values must reflect the destination data, not the source.</summary>
    private static void TemplateRoundTrip()
    {
        // Remember the result returned by Sample() as source.
        var source = Sample();
        // Remember the result returned by Field(...) as field.
        var field = Field("Scattered", 0, 9, 3, 14);
        // Set field.Notes to the text "Assembled most significant bit first".
        field.Notes = "Assembled most significant bit first";
        // Call source.AddField: Adds a validated, independently copied field in one undo step; IDs must be
        // unique.
        source.AddField(field);
        // Remember the result returned by Artifact(...) as path.
        var path = Artifact("fields.template.json");
        // Call source.ExportTemplate: Saves just the reusable field layout, without binary data or display
        // settings.
        source.ExportTemplate(path);
        // Remember a new DocumentModel object using the inputs in parentheses as destination.
        var destination = new DocumentModel(new byte[] { 0xFF, 0xFF });
        // Call destination.ImportTemplate: Replaces the current field layout in one undoable operation; the
        // binary is untouched.
        destination.ImportTemplate(path);
        // Check that destination.Data has the same items in the same order as new byte[] { 0xFF, 0xFF }.
        Sequence(new byte[] { 0xFF, 0xFF }, destination.Data);
        // Check that destination.Fields.Count equals the expected 1; a mismatch fails this test.
        Equal(1, destination.Fields.Count);
        // Check that destination.Fields[0].OrderedBits has the same items in the same order as
        // field.OrderedBits.
        Sequence(field.OrderedBits, destination.Fields[0].OrderedBits);
        // Check that destination.Fields[0].Notes equals the expected field.Notes; a mismatch fails this
        // test.
        Equal(field.Notes, destination.Fields[0].Notes);
        // Check that destination.ReadField(destination.Fields[0]) equals the expected new BigInteger(15); a
        // mismatch fails this test.
        Equal(new BigInteger(15), destination.ReadField(destination.Fields[0]));
        // Restore the state before the most recent undoable edit.
        destination.Undo();
        // Check that destination.Fields.Count equals the expected 0; a mismatch fails this test.
        Equal(0, destination.Fields.Count);
        // Reapply the most recently undone edit.
        destination.Redo();
        // Check that destination.Fields.Count equals the expected 1; a mismatch fails this test.
        Equal(1, destination.Fields.Count);
    }

    /// <summary>A template containing even one out-of-range field must leave the destination layout, bytes, and history intact.</summary>
    private static void InvalidTemplateIsAtomic()
    {
        // Remember the result returned by Sample() as source.
        var source = Sample();
        // Call source.AddField: Adds a validated, independently copied field in one undo step; IDs must be
        // unique.
        source.AddField(Field("Fits", 0));
        // Call source.AddField: Adds a validated, independently copied field in one undo step; IDs must be
        // unique.
        source.AddField(Field("Outside destination", 15));
        // Remember the result returned by Artifact(...) as path.
        var path = Artifact("too-large.template.json");
        // Call source.ExportTemplate: Saves just the reusable field layout, without binary data or display
        // settings.
        source.ExportTemplate(path);
        // Remember a new DocumentModel object using the inputs in parentheses as destination.
        var destination = new DocumentModel(new byte[] { 0xA5 });
        // Call destination.AddField: Adds a validated, independently copied field in one undo step; IDs
        // must be unique.
        destination.AddField(Field("Existing", 1, 2));
        // Check that the supplied operation reports the expected exception type; accepting this invalid
        // case would fail the test.
        Throws<InvalidDataException>(() => destination.ImportTemplate(path));
        // Check that destination.Fields.Count equals the expected 1; a mismatch fails this test.
        Equal(1, destination.Fields.Count);
        // Check that destination.Fields[0].Name equals the expected "Existing"; a mismatch fails this test.
        Equal("Existing", destination.Fields[0].Name);
        // Check that destination.Data has the same items in the same order as new byte[] { 0xA5 }.
        Sequence(new byte[] { 0xA5 }, destination.Data);
        // Restore the state before the most recent undoable edit.
        destination.Undo();
        // Check that destination.Fields.Count equals the expected 0; a mismatch fails this test.
        Equal(0, destination.Fields.Count); // failed imports must leave history untouched.
    }

    /// <summary>Malformed JSON must produce a controlled file error for both projects and templates.</summary>
    private static void MalformedPersistence()
    {
        // Remember the result returned by Artifact(...) as path.
        var path = Artifact("malformed.json");
        // Call File.WriteAllText(...); the values in parentheses are the inputs.
        File.WriteAllText(path, "{not valid json]");
        // Check that the supplied operation reports the expected exception type; accepting this invalid
        // case would fail the test.
        Throws<InvalidDataException>(() => DocumentModel.OpenProject(path));
        // Remember the result returned by Sample() as document.
        var document = Sample();
        // Check that the supplied operation reports the expected exception type; accepting this invalid
        // case would fail the test.
        Throws<InvalidDataException>(() => document.ImportTemplate(path));
        // Check that document.CanUndo equals the expected false; a mismatch fails this test.
        Equal(false, document.CanUndo);
        // Check that document.Data has the same items in the same order as new byte[] { 0xB3, 0x6C }.
        Sequence(new byte[] { 0xB3, 0x6C }, document.Data);
    }

    /// <summary>Manually corrupts saved JSON so loading must detect duplicate addresses instead of trusting persisted data.</summary>
    private static void DuplicateTemplateBits()
    {
        // Remember the result returned by Sample() as source.
        var source = Sample();
        // Call source.AddField: Adds a validated, independently copied field in one undo step; IDs must be
        // unique.
        source.AddField(Field("Bits", 0, 1));
        // Remember the result returned by Artifact(...) as path.
        var path = Artifact("duplicate.template.json");
        // Call source.ExportTemplate: Saves just the reusable field layout, without binary data or display
        // settings.
        source.ExportTemplate(path);
        // JsonNode provides an editable JSON tree. The null-forgiving ! is safe
        // for this fixture because we just wrote the valid file ourselves; the
        // subsequent mutation deliberately bypasses normal model validation.
        var json = JsonNode.Parse(File.ReadAllText(path))!;
        // Set json["Fields"]![0]!["OrderedBits"] to a new JsonArray object using the inputs in parentheses.
        json["Fields"]![0]!["OrderedBits"] = new JsonArray(0, 0);
        // Call File.WriteAllText(...); the values in parentheses are the inputs.
        File.WriteAllText(path, json.ToJsonString());
        // Remember the result returned by Sample() as destination.
        var destination = Sample();
        // Call destination.AddField: Adds a validated, independently copied field in one undo step; IDs
        // must be unique.
        destination.AddField(Field("Existing", 2));
        // Check that the supplied operation reports the expected exception type; accepting this invalid
        // case would fail the test.
        Throws<InvalidDataException>(() => destination.ImportTemplate(path));
        // Check that destination.Fields.Single().Name equals the expected "Existing"; a mismatch fails this
        // test.
        Equal("Existing", destination.Fields.Single().Name);
        // Check that destination.Data has the same items in the same order as new byte[] { 0xB3, 0x6C }.
        Sequence(new byte[] { 0xB3, 0x6C }, destination.Data);
    }

    /// <summary>Checks version rejection and invalid display values by altering an otherwise valid project file.</summary>
    private static void InvalidProjectMetadata()
    {
        // Remember the result returned by Sample() as document.
        var document = Sample();
        // Remember the result returned by Artifact(...) as path.
        var path = Artifact("invalid-metadata.project.json");
        // Call document.SaveProject: Saves edited bytes, display settings, and field definitions together
        // in versioned JSON.
        document.SaveProject(path);
        // Remember the result returned by JsonNode.Parse(...) (! tells the compiler we expect a value here;
        // it does not check at runtime) as json.
        var json = JsonNode.Parse(File.ReadAllText(path))!;
        // Set json["Version"] to 999.
        json["Version"] = 999;
        // Call File.WriteAllText(...); the values in parentheses are the inputs.
        File.WriteAllText(path, json.ToJsonString());
        // Check that the supplied operation reports the expected exception type; accepting this invalid
        // case would fail the test.
        Throws<InvalidDataException>(() => DocumentModel.OpenProject(path));
        // Set json["Version"] to 1.
        json["Version"] = 1;
        // Set json["Settings"]!["BytesPerRow"] to 0.
        json["Settings"]!["BytesPerRow"] = 0;
        // Call File.WriteAllText(...); the values in parentheses are the inputs.
        File.WriteAllText(path, json.ToJsonString());
        // Check that the supplied operation reports the expected exception type; accepting this invalid
        // case would fail the test.
        Throws<InvalidDataException>(() => DocumentModel.OpenProject(path));
        // Set json["Settings"]!["BytesPerRow"] to 16.
        json["Settings"]!["BytesPerRow"] = 16;
        // Set json["Settings"]!["BitNumbering"] to 123.
        json["Settings"]!["BitNumbering"] = 123;
        // Call File.WriteAllText(...); the values in parentheses are the inputs.
        File.WriteAllText(path, json.ToJsonString());
        // Check that the supplied operation reports the expected exception type; accepting this invalid
        // case would fail the test.
        Throws<InvalidDataException>(() => DocumentModel.OpenProject(path));
    }

    /// <summary>Registers the output-format checks with the same runner used for data-model tests.</summary>
    private static void RunFormattingTests()
    {
        // Run NumericFormatting as a test; the quoted text names the behavior reported in the results.
        Test("hex and decimal notation preserve byte and offset values", NumericFormatting);
        // Run BitRulers as a test; the quoted text names the behavior reported in the results.
        Test("changing bit numbering changes rulers without reversing the data", BitRulers);
        // Run HexExport as a test; the quoted text names the behavior reported in the results.
        Test("hex export preserves the complete file, row breaks, and ASCII alignment", HexExport);
        // Run DecimalExport as a test; the quoted text names the behavior reported in the results.
        Test("decimal export preserves padded values and independent decimal offsets", DecimalExport);
        // Run BitExport as a test; the quoted text names the behavior reported in the results.
        Test("bits-only export preserves physical bit order in either numbering convention", BitExport);
        // Run DataOnlyExport as a test; the quoted text names the behavior reported in the results.
        Test("hidden columns and ruler disappear from exported text", DataOnlyExport);
        // Run PartialRowAnnotations as a test; the quoted text names the behavior reported in the results.
        Test("partial rows align ASCII and show all intersecting fields", PartialRowAnnotations);
        // Run ResizedColumns as a test; the quoted text names the behavior reported in the results.
        Test("resized columns preserve exact character positions in text exports", ResizedColumns);
        // Run NarrowColumns as a test; the quoted text names the behavior reported in the results.
        Test("narrow columns expand to show complete fixed-width rows", NarrowColumns);
        // Run AnnotationRows as a test; the quoted text names the behavior reported in the results.
        Test("annotation rows preserve clipped labels and empty row spacing", AnnotationRows);
        // Run EmptyExport as a test; the quoted text names the behavior reported in the results.
        Test("empty exports contain no invented data rows", EmptyExport);
        // Run InvalidFormatting as a test; the quoted text names the behavior reported in the results.
        Test("invalid formatting settings fail before output is written", InvalidFormatting);
    }

    /// <summary>Checks fixed-width notation and confirms large addresses grow instead of being truncated.</summary>
    private static void NumericFormatting()
    {
        // Check that DisplayFormatter.FormatByte(0, NumericBase.Hexadecimal) equals the expected "00"; a
        // mismatch fails this test.
        Equal("00", DisplayFormatter.FormatByte(0, NumericBase.Hexadecimal));
        // Check that DisplayFormatter.FormatByte(161, NumericBase.Hexadecimal) equals the expected "A1"; a
        // mismatch fails this test.
        Equal("A1", DisplayFormatter.FormatByte(161, NumericBase.Hexadecimal));
        // Check that DisplayFormatter.FormatByte(255, NumericBase.Hexadecimal) equals the expected "FF"; a
        // mismatch fails this test.
        Equal("FF", DisplayFormatter.FormatByte(255, NumericBase.Hexadecimal));
        // Check that DisplayFormatter.FormatByte(0, NumericBase.Decimal) equals the expected "000"; a
        // mismatch fails this test.
        Equal("000", DisplayFormatter.FormatByte(0, NumericBase.Decimal));
        // Check that DisplayFormatter.FormatByte(161, NumericBase.Decimal) equals the expected "161"; a
        // mismatch fails this test.
        Equal("161", DisplayFormatter.FormatByte(161, NumericBase.Decimal));
        // Check that DisplayFormatter.FormatByte(255, NumericBase.Decimal) equals the expected "255"; a
        // mismatch fails this test.
        Equal("255", DisplayFormatter.FormatByte(255, NumericBase.Decimal));
        // Check that DisplayFormatter.FormatOffset(16, NumericBase.Hexadecimal) equals the expected
        // "00000010"; a mismatch fails this test.
        Equal("00000010", DisplayFormatter.FormatOffset(16, NumericBase.Hexadecimal));
        // Check that DisplayFormatter.FormatOffset(16, NumericBase.Decimal) equals the expected "00000016";
        // a mismatch fails this test.
        Equal("00000016", DisplayFormatter.FormatOffset(16, NumericBase.Decimal));
        // Check that DisplayFormatter.FormatOffset(0x100000000, NumericBase.Hexadecimal) equals the
        // expected "100000000"; a mismatch fails this test.
        Equal("100000000", DisplayFormatter.FormatOffset(0x100000000, NumericBase.Hexadecimal));
    }

    /// <summary>Changing bit numbering must alter ruler labels without reversing the physical bit string.</summary>
    private static void BitRulers()
    {
        // Remember the result returned by Settings(...) as settings.
        var settings = Settings(2);
        // Set settings.ViewMode to DataViewMode.Bits.
        settings.ViewMode = DataViewMode.Bits;
        // Check that DisplayFormatter.GetRuler(settings) equals the expected "76543210 76543210"; a
        // mismatch fails this test.
        Equal("76543210 76543210", DisplayFormatter.GetRuler(settings));
        // Check that DisplayFormatter.FormatBits(0xB3, BitNumbering.LsbZero) equals the expected
        // "10110011"; a mismatch fails this test.
        Equal("10110011", DisplayFormatter.FormatBits(0xB3, BitNumbering.LsbZero));
        // Set settings.BitNumbering to BitNumbering.MsbZero.
        settings.BitNumbering = BitNumbering.MsbZero;
        // Check that DisplayFormatter.GetRuler(settings) equals the expected "01234567 01234567"; a
        // mismatch fails this test.
        Equal("01234567 01234567", DisplayFormatter.GetRuler(settings));
        // Check that DisplayFormatter.FormatBits(0xB3, BitNumbering.MsbZero) equals the expected
        // "10110011"; a mismatch fails this test.
        Equal("10110011", DisplayFormatter.FormatBits(0xB3, BitNumbering.MsbZero));
        // Check that DisplayFormatter.FormatBits(1, BitNumbering.MsbZero) equals the expected "00000001"; a
        // mismatch fails this test.
        Equal("00000001", DisplayFormatter.FormatBits(1, BitNumbering.MsbZero));
        // Set settings.ViewMode to DataViewMode.Bytes.
        settings.ViewMode = DataViewMode.Bytes;
        // Check that DisplayFormatter.GetRuler(settings) equals the expected "00 01"; a mismatch fails this
        // test.
        Equal("00 01", DisplayFormatter.GetRuler(settings));
        // Set settings.ByteBase to NumericBase.Decimal.
        settings.ByteBase = NumericBase.Decimal;
        // Check that DisplayFormatter.GetRuler(settings) equals the expected "000 001"; a mismatch fails
        // this test.
        Equal("000 001", DisplayFormatter.GetRuler(settings));
    }

    /// <summary>Compares the complete hex export, including ruler indentation and padding of the incomplete final row.</summary>
    private static void HexExport()
    {
        // Remember a new DocumentModel object using the inputs in parentheses as document.
        var document = new DocumentModel(new byte[] { 0xFF, 0xF0, 0xA1, 0x48, 0x69 });
        // Remember the result returned by Settings(...) as settings.
        var settings = Settings(3);
        // Set settings.ShowRuler to true (yes).
        settings.ShowRuler = true;
        // Spaces and \n are part of the expected format, not decorative test
        // formatting. Comparing the full string detects subtle alignment changes.
        Equal("              00 01 02\n  00000000    FF F0 A1    ...\n  00000003    48 69       Hi\n", Export(document, settings));
        // Remember the result returned by DisplayFormatter.FormatRow(...) as row.
        var row = DisplayFormatter.FormatRow(document, 3, settings);
        // Check that row.Offset equals the expected "00000003"; a mismatch fails this test.
        Equal("00000003", row.Offset);
        // Check that row.Data equals the expected "48 69 "; a mismatch fails this test.
        Equal("48 69   ", row.Data);
        // Check that row.Ascii equals the expected "Hi"; a mismatch fails this test.
        Equal("Hi", row.Ascii);
    }

    /// <summary>Checks three-digit decimal bytes and independently chosen decimal offsets.</summary>
    private static void DecimalExport()
    {
        // Remember a new DocumentModel object using the inputs in parentheses as document.
        var document = new DocumentModel(new byte[] { 0xFF, 0xF0, 0xA1, 0x48, 0x69 });
        // Remember the result returned by Settings(...) as settings.
        var settings = Settings(3);
        // Set settings.ByteBase to NumericBase.Decimal.
        settings.ByteBase = NumericBase.Decimal;
        // Set settings.OffsetBase to NumericBase.Decimal.
        settings.OffsetBase = NumericBase.Decimal;
        // Check that Export(document, settings) equals the expected " 00000000 255 240 161 ...\n 00000003
        // 072 105 Hi\n"; a mismatch fails this test.
        Equal("  00000000    255 240 161    ...\n  00000003    072 105        Hi\n", Export(document, settings));
        // Set settings.BytesPerRow to 16.
        settings.BytesPerRow = 16;
        // Set settings.ShowAscii to false (no).
        settings.ShowAscii = false;
        // Remember a new DocumentModel object using the inputs in parentheses as offsets.
        var offsets = new DocumentModel(new byte[17]);
        // Remember the result returned by DisplayFormatter.FormatRow(...) as lastRow.
        var lastRow = DisplayFormatter.FormatRow(offsets, 16, settings);
        // Check that lastRow.Offset equals the expected "00000016"; a mismatch fails this test.
        Equal("00000016", lastRow.Offset);
        // Check that lastRow.Data equals the expected "000"; a mismatch fails this test.
        Equal("000", lastRow.Data);
    }

    /// <summary>Checks full-file binary output with only the data column visible under both ruler conventions.</summary>
    private static void BitExport()
    {
        // Remember a new DocumentModel object using the inputs in parentheses as document.
        var document = new DocumentModel(new byte[] { 0xB3, 0x6C, 1 });
        // Remember the result returned by Settings(...) as settings.
        var settings = Settings(2);
        // Set settings.ViewMode to DataViewMode.Bits.
        settings.ViewMode = DataViewMode.Bits;
        // Set settings.ShowOffsets to false (no).
        settings.ShowOffsets = false;
        // Set settings.ShowAscii to false (no).
        settings.ShowAscii = false;
        // Set settings.ShowRuler to true (yes).
        settings.ShowRuler = true;
        // Check that Export(document, settings) equals the expected " 76543210 76543210\n 10110011
        // 01101100\n 00000001\n"; a mismatch fails this test.
        Equal("  76543210 76543210\n  10110011 01101100\n  00000001\n", Export(document, settings));
        // Set settings.BitNumbering to BitNumbering.MsbZero.
        settings.BitNumbering = BitNumbering.MsbZero;
        // Check that Export(document, settings) equals the expected " 01234567 01234567\n 10110011
        // 01101100\n 00000001\n"; a mismatch fails this test.
        Equal("  01234567 01234567\n  10110011 01101100\n  00000001\n", Export(document, settings));
    }

    /// <summary>Hidden offsets, ASCII, ruler, and labels must contribute no extra text to a data-only export.</summary>
    private static void DataOnlyExport()
    {
        // Remember a new DocumentModel object using the inputs in parentheses as document.
        var document = new DocumentModel(new byte[] { 0xFF, 0xF0, 0xA1, 0x48, 0x69 });
        // Call document.AddField: Adds a validated, independently copied field in one undo step; IDs must
        // be unique.
        document.AddField(Field("Flag", 0));
        // Remember the result returned by Settings(...) as settings.
        var settings = Settings(3);
        // Set settings.ShowOffsets to false (no).
        settings.ShowOffsets = false;
        // Set settings.ShowAscii to false (no).
        settings.ShowAscii = false;
        // Check that Export(document, settings) equals the expected " FF F0 A1\n 48 69\n"; a mismatch fails
        // this test.
        Equal("  FF F0 A1\n  48 69\n", Export(document, settings));
    }

    /// <summary>Checks row-crossing labels and preserves a real ASCII space at the end of the source data.</summary>
    private static void PartialRowAnnotations()
    {
        // Remember a new DocumentModel object using the inputs in parentheses as document.
        var document = new DocumentModel(new byte[] { 0x41, 0x7F, 0x20 });
        // Call document.AddField: Adds a validated, independently copied field in one undo step; IDs must
        // be unique.
        document.AddField(Field("Crossing", 15, 16));
        // Call document.AddField: Adds a validated, independently copied field in one undo step; IDs must
        // be unique.
        document.AddField(Field("Last bit", 16));
        // Remember the result returned by Settings(...) as settings.
        var settings = Settings(2);
        // Set settings.ShowAnnotations to true (yes).
        settings.ShowAnnotations = true;
        // Set settings.DataWidth to 240.
        settings.DataWidth = 240;
        // Check that Export(document, settings) equals the expected the expression below; a mismatch fails
        // this test.
        Equal("  00000000    41 7F" + new string(' ', 25) + "A.\n" +
            "              [Crossing]\n" +
            "  00000002    20" + new string(' ', 29) + "\n" +
            "              [Crossing] [Last bit]\n", Export(document, settings));
        // Remember the result returned by DisplayFormatter.FormatRow(...) as row.
        var row = DisplayFormatter.FormatRow(document, 2, settings);
        // Check that row.Data equals the expected "20 "; a mismatch fails this test.
        Equal("20   ", row.Data);
        // Check that row.Ascii equals the expected " "; a mismatch fails this test.
        Equal(" ", row.Ascii);
        // Check that row.Annotations equals the expected "[Crossing] [Last bit]"; a mismatch fails this
        // test.
        Equal("[Crossing] [Last bit]", row.Annotations);
    }

    /// <summary>An empty document emits no data rows; it may still emit the explicitly enabled ruler.</summary>
    private static void EmptyExport()
    {
        // Remember the result returned by Settings(...) as settings.
        var settings = Settings(3);
        // Check that Export(new DocumentModel(), settings) equals the expected string.Empty; a mismatch
        // fails this test.
        Equal(string.Empty, Export(new DocumentModel(), settings));
        // Set settings.ShowRuler to true (yes).
        settings.ShowRuler = true;
        // Check that Export(new DocumentModel(), settings) equals the expected " 00 01 02\n"; a mismatch
        // fails this test.
        Equal("              00 01 02\n", Export(new DocumentModel(), settings));
    }

    /// <summary>Invalid grouping or NaN dimensions must be rejected before output is partially written.</summary>
    private static void InvalidFormatting()
    {
        // Remember the result returned by Settings(...) as settings.
        var settings = Settings(0);
        // Remember a new StringWriter object as writer. The using declaration releases it automatically
        // when this scope ends, even after an error.
        using var writer = new StringWriter();
        // Check that the supplied operation reports the expected exception type; accepting this invalid
        // case would fail the test.
        Throws<InvalidDataException>(() => DisplayFormatter.Export(writer, Sample(), settings));
        // Check that writer.ToString() equals the expected string.Empty; a mismatch fails this test.
        Equal(string.Empty, writer.ToString());
        // Set settings.BytesPerRow to 1.
        settings.BytesPerRow = 1;
        // Set settings.DataWidth to double.NaN.
        settings.DataWidth = double.NaN;
        // Check that the supplied operation reports the expected exception type; accepting this invalid
        // case would fail the test.
        Throws<InvalidDataException>(() => DisplayFormatter.FormatRow(Sample(), 0, settings));
    }

    /// <summary>Changing pixel widths must move exported tokens to the corresponding whole-character positions.</summary>
    private static void ResizedColumns()
    {
        // Remember a new DocumentModel object using the inputs in parentheses as document.
        var document = new DocumentModel(new byte[] { 0x41, 0x42, 0x43 });
        // Remember the result returned by Settings(...) as settings.
        var settings = Settings(2);
        // Set settings.OffsetWidth to 128.
        settings.OffsetWidth = 128;
        // Set settings.DataWidth to 160.
        settings.DataWidth = 160;
        // Set settings.AsciiWidth to 80.
        settings.AsciiWidth = 80;
        // Set settings.ShowRuler to true (yes).
        settings.ShowRuler = true;
        // Remember the result returned by DisplayFormatter.GetColumnCharacterWidths(...) as widths.
        var widths = DisplayFormatter.GetColumnCharacterWidths(document, settings);
        // Check that widths.Offset equals the expected 16; a mismatch fails this test.
        Equal(16, widths.Offset);
        // Check that widths.Data equals the expected 20; a mismatch fails this test.
        Equal(20, widths.Data);
        // Check that widths.Ascii equals the expected 10; a mismatch fails this test.
        Equal(10, widths.Ascii);
        // Remember the result returned by Export(document, settings).Split(...) as lines.
        var lines = Export(document, settings).Split('\n');
        // IndexOf returns a zero-based character position. The ruler and data
        // share a start, and ASCII retains its start even on an incomplete row.
        Equal(18, lines[0].IndexOf("00 01", StringComparison.Ordinal));
        // Check that lines[1].IndexOf("41 42", StringComparison.Ordinal) equals the expected 18; a mismatch
        // fails this test.
        Equal(18, lines[1].IndexOf("41 42", StringComparison.Ordinal));
        // Check that lines[1].IndexOf("AB", StringComparison.Ordinal) equals the expected 38; a mismatch
        // fails this test.
        Equal(38, lines[1].IndexOf("AB", StringComparison.Ordinal));
        // Check that lines[2].IndexOf('C') equals the expected 38; a mismatch fails this test.
        Equal(38, lines[2].IndexOf('C'));
        // Set settings.OffsetWidth to 200.
        settings.OffsetWidth = 200;
        // Set settings.DataWidth to 243.
        settings.DataWidth = 243;
        // Set lines to the result returned by Export(document, settings).Split(...).
        lines = Export(document, settings).Split('\n');
        // Check that lines[1].IndexOf("41 42", StringComparison.Ordinal) equals the expected 27; a mismatch
        // fails this test.
        Equal(27, lines[1].IndexOf("41 42", StringComparison.Ordinal));
        // Check that lines[1].IndexOf("AB", StringComparison.Ordinal) equals the expected 58; a mismatch
        // fails this test.
        Equal(58, lines[1].IndexOf("AB", StringComparison.Ordinal));
        // Check that lines[2].IndexOf('C') equals the expected 58; a mismatch fails this test.
        Equal(58, lines[2].IndexOf('C'));
    }

    /// <summary>Requested widths smaller than their contents must expand enough to preserve every byte and ASCII character.</summary>
    private static void NarrowColumns()
    {
        // Remember a new DocumentModel object using the inputs in parentheses as document.
        var document = new DocumentModel(Enumerable.Repeat((byte)0x41, 16).ToArray());
        // Remember the result returned by Settings(...) as settings.
        var settings = Settings(16);
        // Set settings.OffsetWidth to settings.DataWidth = settings.AsciiWidth = 24.
        settings.OffsetWidth = settings.DataWidth = settings.AsciiWidth = 24;
        // Remember the result returned by DisplayFormatter.GetColumnCharacterWidths(...) as widths.
        var widths = DisplayFormatter.GetColumnCharacterWidths(document, settings);
        // Check that widths.Offset equals the expected 12; a mismatch fails this test.
        Equal(12, widths.Offset);
        // Check that widths.Data equals the expected 51; a mismatch fails this test.
        Equal(51, widths.Data);
        // Check that widths.Ascii equals the expected 20; a mismatch fails this test.
        Equal(20, widths.Ascii);
        // Remember the result returned by Export(document, settings).TrimEnd(...) as line.
        string line = Export(document, settings).TrimEnd('\n');
        // Check that line.Substring(2, 8) equals the expected "00000000"; a mismatch fails this test.
        Equal("00000000", line.Substring(2, 8));
        // Check that line.Substring(14, 47) equals the expected string.Join(' ', Enumerable.Repeat("41",
        // 16)); a mismatch fails this test.
        Equal(string.Join(' ', Enumerable.Repeat("41", 16)), line.Substring(14, 47));
        // Check that line[65..] equals the expected new string('A', 16); a mismatch fails this test.
        Equal(new string('A', 16), line[65..]);
    }

    /// <summary>Checks shared ellipsis truncation, blank annotation lines, and a cut near a two-char Unicode emoji.</summary>
    private static void AnnotationRows()
    {
        // Remember a new DocumentModel object using the inputs in parentheses as document.
        var document = new DocumentModel(new byte[] { 0x41, 0x42, 0x43 });
        // Call document.AddField: Adds a validated, independently copied field in one undo step; IDs must
        // be unique.
        document.AddField(Field("Crossing", 0));
        // Remember the result returned by Settings(...) as settings.
        var settings = Settings(2);
        // Set settings.ShowAnnotations to true (yes).
        settings.ShowAnnotations = true;
        // Check that DisplayFormatter.FormatRow(document, 0, settings).Annotations equals the expected
        // "[Crossi…"; a mismatch fails this test.
        Equal("[Crossi…", DisplayFormatter.FormatRow(document, 0, settings).Annotations);
        // Check that Export(document, settings) equals the expected " 00000000 41 42 AB\n [Crossi…\n
        // 00000002 43 C\n\n"; a mismatch fails this test.
        Equal("  00000000    41 42       AB\n              [Crossi…\n  00000002    43          C\n\n", Export(document, settings));
        // Remember a new DocumentModel object using the inputs in parentheses as unicode.
        var unicode = new DocumentModel(new byte[] { 0x41 });
        // Call unicode.AddField: Adds a validated, independently copied field in one undo step; IDs must be
        // unique.
        unicode.AddField(Field("12345😀abc", 0));
        // Check that DisplayFormatter.FormatRow(unicode, 0, settings).Annotations equals the expected
        // "[12345…"; a mismatch fails this test.
        Equal("[12345…", DisplayFormatter.FormatRow(unicode, 0, settings).Annotations);
    }

    /// <summary>Creates compact, deterministic column settings so exact expected strings stay readable in tests.</summary>
    private static DisplaySettings Settings(int bytesPerRow) => new()
    {
        // Set this new object's BytesPerRow entry to bytesPerRow.
        BytesPerRow = bytesPerRow,
        // Set this new object's ShowRuler entry to false (no).
        ShowRuler = false,
        // Set this new object's ShowAnnotations entry to false (no).
        ShowAnnotations = false,
        // Set this new object's OffsetWidth entry to 96.
        OffsetWidth = 96,
        // Set this new object's DataWidth entry to 96.
        DataWidth = 96,
        // Set this new object's AsciiWidth entry to 56.
        AsciiWidth = 56
    };

    /// <summary>Captures export text in memory, using the same newline on every operating system.</summary>
    private static string Export(DocumentModel document, DisplaySettings settings)
    {
        // using disposes the temporary writer when this helper returns. No file
        // is needed for formatting checks; persistence tests cover disk I/O separately.
        using var writer = new StringWriter { NewLine = "\n" };
        // Call DisplayFormatter.Export: Writes the entire document using the current row grouping,
        // notation, columns, ruler, and annotations.
        DisplayFormatter.Export(writer, document, settings);
        // Return writer converted to text to the caller and leave this method.
        return writer.ToString();
    }

    /// <summary>Returns a fresh two-byte fixture whose mixed bits expose shifts, order, and masking mistakes.</summary>
    private static DocumentModel Sample() => new(new byte[] { 0xB3, 0x6C });

    /// <summary>Creates a valid field fixture; params allows tests to write addresses directly as arguments.</summary>
    private static NamedField Field(string name, params long[] orderedBits) => new()
    {
        // Set this new object's Name entry to name.
        Name = name,
        // Set this new object's OrderedBits entry to a separate list containing orderedBits's items.
        OrderedBits = orderedBits.ToList(),
        // Set this new object's Color entry to the text "#78A9FF".
        Color = "#78A9FF",
        // Set this new object's Notes entry to string.Empty.
        Notes = string.Empty
    };

    /// <summary>Places a test-created file inside this run's disposable fixture directory.</summary>
    private static string Artifact(string name) => Path.Combine(ArtifactDirectory, name);

    /// <summary>Runs one test delegate, records pass/fail, and reports failures without stopping the remaining checks.</summary>
    /// <remarks>An Action is a callable operation with no parameters and no return value.</remarks>
    private static void Test(string name, Action action)
    {
        // Attempt this work. A matching catch below handles a reported exception; a finally block, when
        // present, performs cleanup on the way out.
        try
        {
            // Call action().
            action();
            // Increase _passed by one.
            _passed++;
            // Write the supplied text followed by a line break to Console.
            Console.WriteLine($"PASS {name}");
        }
        // If the preceding try reports Exception, refer to it as exception; run this recovery path.
        catch (Exception exception)
        {
            // Increase _failed by one.
            _failed++;
            // Write the supplied text followed by a line break to Console.Error.
            Console.Error.WriteLine($"FAIL {name}: {exception}");
        }
    }

    /// <summary>Asserts equality using the normal comparison rules for the generic value type T.</summary>
    // Generic helpers work for strings, numbers, enum values, and other types
    // without writing a separate assertion method for each one.
    private static void Equal<T>(T expected, T actual)
    {
        // If it is not the case that EqualityComparer<T>.Default.Equals(expected, actual) is true, stop
        // this normal path by reporting InvalidOperationException; the arguments carry the error details.
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            // Stop this normal path by reporting InvalidOperationException; the arguments carry the error
            // details.
            throw new InvalidOperationException($"Expected <{expected}>, got <{actual}>.");
    }

    /// <summary>Compares collection contents in order, instead of comparing array/list object identities.</summary>
    private static void Sequence<T>(IEnumerable<T> expected, IEnumerable<T> actual)
    {
        // If it is not the case that expected.SequenceEqual(actual) is true, stop this normal path by
        // reporting InvalidOperationException; the arguments carry the error details.
        if (!expected.SequenceEqual(actual))
            // Stop this normal path by reporting InvalidOperationException; the arguments carry the error
            // details.
            throw new InvalidOperationException($"Expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}].");
    }

    /// <summary>Passes only if the operation throws the requested exception type or a subtype.</summary>
    /// <remarks>The lambda passed by a test delays the operation until it is inside this try/catch.</remarks>
    private static void Throws<T>(Action action) where T : Exception
    {
        // Attempt this work. A matching catch below handles a reported exception; a finally block, when
        // present, performs cleanup on the way out.
        try { action(); }
        // If the preceding try reports T, refer to it as ; run this recovery path.
        catch (T) { return; }
        // Stop this normal path by reporting InvalidOperationException; the arguments carry the error
        // details.
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
