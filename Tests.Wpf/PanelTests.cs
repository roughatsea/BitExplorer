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
// Make names from BitExplorer.Core available here without writing their full prefix each time. This does
// not run that library's code.
using BitExplorer.Core;
// Make names from WpfApp1.ViewModels available here without writing their full prefix each time. This does
// not run that library's code.
using WpfApp1.ViewModels;

// These scenarios test view models directly, without opening a window. "partial" lets this file
// contribute methods to the same Program class as the test runner and the other scenario files.
// Fake dialogs supply scripted answers, so a test never pauses for a real user interaction.
internal static partial class Program
{
    /// <summary>Registers the panel behavior checks with the shared pass/fail test runner.</summary>
    private static void RunPanelTests()
    {
        // Run FieldAssemblyAndMembership as a test; the quoted text names the behavior reported in the
        // results.
        Test("saved field assembly stays distinct from sorted source-bit membership", FieldAssemblyAndMembership);
        // Run FieldHistorySynchronization as a test; the quoted text names the behavior reported in the
        // results.
        Test("mapping edits, deletion, and undo keep session and inspectors synchronized", FieldHistorySynchronization);
        // Run InspectorCommands as a test; the quoted text names the behavior reported in the results.
        Test("inspector edits update dependent values and undo without a view", InspectorCommands);
        // Run UnfinishedInputs as a test; the quoted text names the behavior reported in the results.
        Test("unfinished byte and field inputs survive unrelated display notifications", UnfinishedInputs);
        // Run LabelCommands as a test; the quoted text names the behavior reported in the results.
        Test("label commands use the active field and cancellation leaves metadata unchanged", LabelCommands);
        // Run OrderedInterpretation as a test; the quoted text names the behavior reported in the results.
        Test("byte order and numbering change interpretation without changing source bits", OrderedInterpretation);
        // Run DisplayCommands as a test; the quoted text names the behavior reported in the results.
        Test("display controls preserve selection and explicit row grouping", DisplayCommands);
    }

    /// <summary>Creates the known demo packet and explicitly selects its first byte for repeatable tests.</summary>
    private static ExplorerSession CreateSession()
    {
        // Remember a new ExplorerSession object using the inputs in parentheses as session.
        var session = new ExplorerSession(DocumentModel.CreateDemo(), "Regression packet");
        // Request selection of the supplied physical bit addresses; the selection code checks its bounds
        // and limit.
        session.SelectRawBits(Enumerable.Range(0, 8).Select(bit => (long)bit));
        // Return session to the caller and leave this method.
        return session;
    }

    /// <summary>A field's value order must remain distinct from the sorted set of highlighted source bits.</summary>
    private static void FieldAssemblyAndMembership()
    {
        // Arrange: the packet begins B3 6C. Physical addresses count from each byte's highest bit.
        // Addresses [14, 3, 9, 0] contain [0, 1, 1, 1], so this saved order represents binary 0111 = 7.
        using var session = CreateSession();
        // Remember a new FakeUserInteractionService object as dialogs.
        var dialogs = new FakeUserInteractionService();
        // Remember a new InspectorViewModel object using the inputs in parentheses as inspector.
        var inspector = new InspectorViewModel(session, dialogs, _ => { });
        // Remember a new NamedField object; the entries in braces set its initial contents or properties as
        // field.
        var field = new NamedField { Name = "Scattered", OrderedBits = [14, 3, 9, 0] };
        // Call session.Document.AddField: Adds a validated, independently copied field in one undo step;
        // IDs must be unique.
        session.Document.AddField(field);
        // Act: choose a named field. Check that highlighting is sorted for membership, while reading
        // the value follows the field's saved order rather than silently sorting its significance.
        session.SelectField(field.Id);
        // Check that session.Selection.Bits has the same items in the same order as new long[] { 0, 3, 9,
        // 14 }.
        Sequence(new long[] { 0, 3, 9, 14 }, session.Selection.Bits);
        // Check that session.GetInterpretationBits() has the same items in the same order as
        // field.OrderedBits.
        Sequence(field.OrderedBits, session.GetInterpretationBits());
        // Check that session.InterpretationOrder equals the expected InterpretationOrder.SavedField; a
        // mismatch fails this test.
        Equal(InterpretationOrder.SavedField, session.InterpretationOrder);
        // Check that inspector.SelectionDecimal equals the expected "7"; a mismatch fails this test.
        Equal("7", inspector.SelectionDecimal);
        // Act again: raw selection deliberately leaves named-field interpretation. The same physical
        // bits in file order contain [1, 1, 1, 0], giving binary 1110 = 14 without changing the document.
        session.SelectRawBits([0, 3, 9, 14]);
        // Check that session.SelectedField equals the expected null; a mismatch fails this test.
        Equal<NamedField?>(null, session.SelectedField);
        // Check that session.InterpretationOrder equals the expected InterpretationOrder.FileOrder; a
        // mismatch fails this test.
        Equal(InterpretationOrder.FileOrder, session.InterpretationOrder);
        // Check that inspector.SelectionDecimal equals the expected "14"; a mismatch fails this test.
        Equal("14", inspector.SelectionDecimal);
        // Check that session.GetInterpretationBits() has the same items in the same order as new long[] {
        // 0, 3, 9, 14 }.
        Sequence(new long[] { 0, 3, 9, 14 }, session.GetInterpretationBits());
    }

    /// <summary>Editing, undoing, redoing, and deleting a field must update every dependent panel consistently.</summary>
    private static void FieldHistorySynchronization()
    {
        // Arrange: Message type uses physical bits 5..10 across B3 6C, whose values are 011011 = 27.
        // Clone the original mapping so undo can be compared with a stable snapshot.
        using var session = CreateSession();
        // Remember a new FakeUserInteractionService object as dialogs.
        var dialogs = new FakeUserInteractionService();
        // Remember a new InspectorViewModel object using the inputs in parentheses as inspector.
        var inspector = new InspectorViewModel(session, dialogs, _ => { });
        // Remember a new FieldsViewModel object using the inputs in parentheses as fields.
        var fields = new FieldsViewModel(session, dialogs, _ => { });
        // Remember a copy returned by session.Document.Fields.Single(field => field.Name == "Message
        // type").Clone() as original.
        var original = session.Document.Fields.Single(field => field.Name == "Message type").Clone();
        // Choose the named field with the supplied identity, synchronizing its highlighted bits and
        // interpretation.
        session.SelectField(original.Id);
        // Check that inspector.SelectionDecimal equals the expected "27"; a mismatch fails this test.
        Equal("27", inspector.SelectionDecimal);
        // Remember a copy returned by original.Clone() as edited.
        var edited = original.Clone();
        // Set edited.OrderedBits to a collection containing the listed values in their written order.
        edited.OrderedBits = [14, 3, 9, 0];
        // Act/check: replace the mapping with [14, 3, 9, 0], which reads 0111 = 7. Undo restores
        // the original mapping and value; redo reapplies the edited mapping and its interpretation.
        session.Document.UpdateField(edited);
        // Check that session.Selection.Bits has the same items in the same order as
        // edited.OrderedBits.Order().
        Sequence(edited.OrderedBits.Order(), session.Selection.Bits);
        // Check that inspector.SelectionDecimal equals the expected "7"; a mismatch fails this test.
        Equal("7", inspector.SelectionDecimal);
        // Restore the state before the most recent undoable edit.
        session.Document.Undo();
        // Check that session.GetInterpretationBits() has the same items in the same order as
        // original.OrderedBits.
        Sequence(original.OrderedBits, session.GetInterpretationBits());
        // Check that inspector.SelectionDecimal equals the expected "27"; a mismatch fails this test.
        Equal("27", inspector.SelectionDecimal);
        // Reapply the most recently undone edit.
        session.Document.Redo();
        // Check that inspector.SelectionDecimal equals the expected "7"; a mismatch fails this test.
        Equal("7", inspector.SelectionDecimal);
        // Deleting the active label must leave raw selected bits but clear saved-field mode and its
        // editor. The demo starts with four labels: deletion leaves three, and undo restores four.
        fields.DeleteLabelCommand.Execute(null);
        // Check that session.SelectedField equals the expected null; a mismatch fails this test.
        Equal<NamedField?>(null, session.SelectedField);
        // Check that session.InterpretationOrder equals the expected InterpretationOrder.FileOrder; a
        // mismatch fails this test.
        Equal(InterpretationOrder.FileOrder, session.InterpretationOrder);
        // Check that inspector.HasSelectedField equals the expected false; a mismatch fails this test.
        Equal(false, inspector.HasSelectedField);
        // Check that fields.Items.Count equals the expected 3; a mismatch fails this test.
        Equal(3, fields.Items.Count);
        // Restore the state before the most recent undoable edit.
        session.Document.Undo();
        // Check that fields.Items.Count equals the expected 4; a mismatch fails this test.
        Equal(4, fields.Items.Count);
        // Check that fields.Items.Any(field => field.Id == original.Id) equals the expected true; a
        // mismatch fails this test.
        Equal(true, fields.Items.Any(field => field.Id == original.Id));
    }

    /// <summary>Inspector commands must edit the shared document, refresh dependent values, and reject invalid bytes.</summary>
    private static void InspectorCommands()
    {
        // Arrange: B3 is binary 10110011. Its third physical bit (index 2) has weight 32.
        using var session = CreateSession();
        // Remember a new FakeUserInteractionService object as dialogs.
        var dialogs = new FakeUserInteractionService();
        // Remember a new InspectorViewModel object using the inputs in parentheses as inspector.
        var inspector = new InspectorViewModel(session, dialogs, _ => { });
        // Remember the only matching item; finding none or more than one reports an error as version.
        var version = session.Document.Fields.Single(field => field.Name == "Version");
        // Act: flip that 1 to 0. Check B3 - 20(hex) = 93(hex) = 147(decimal), and the three-bit
        // Version field changes from 101(binary, 5) to 100(binary, 4). Undo must restore both views.
        inspector.Bits[2].FlipCommand.Execute(null);
        // Check that session.Document.Data[0] equals the expected (byte)0x93; a mismatch fails this test.
        Equal((byte)0x93, session.Document.Data[0]);
        // Check that session.Document.ReadField(version) equals the expected new BigInteger(4); a mismatch
        // fails this test.
        Equal(new BigInteger(4), session.Document.ReadField(version));
        // Check that inspector.ByteHex equals the expected "93"; a mismatch fails this test.
        Equal("93", inspector.ByteHex);
        // Check that inspector.ByteDecimal equals the expected "147"; a mismatch fails this test.
        Equal("147", inspector.ByteDecimal);
        // Restore the state before the most recent undoable edit.
        session.Document.Undo();
        // Check that inspector.ByteHex equals the expected "B3"; a mismatch fails this test.
        Equal("B3", inspector.ByteHex);
        // Check that inspector.Bits[2].IsSet equals the expected true; a mismatch fails this test.
        Equal(true, inspector.Bits[2].IsSet);
        // FF is the largest valid byte, 255 decimal. In hex mode, "100" means 256, so rejection
        // must leave FF untouched and report one validation error through the fake dialog service.
        inspector.ByteInput = "FF";
        // Invoke inspector.ApplyByteCommand.Execute to perform the action stored in that command; what
        // changes depends on which command it is.
        inspector.ApplyByteCommand.Execute(null);
        // Check that session.Document.Data[0] equals the expected (byte)255; a mismatch fails this test.
        Equal((byte)255, session.Document.Data[0]);
        // Set inspector.ByteInput to the text "100".
        inspector.ByteInput = "100";
        // Invoke inspector.ApplyByteCommand.Execute to perform the action stored in that command; what
        // changes depends on which command it is.
        inspector.ApplyByteCommand.Execute(null);
        // Check that session.Document.Data[0] equals the expected (byte)255; a mismatch fails this test.
        Equal((byte)255, session.Document.Data[0]);
        // Check that dialogs.Errors.Count equals the expected 1; a mismatch fails this test.
        Equal(1, dialogs.Errors.Count);
    }

    /// <summary>Unrelated refreshes must preserve partially typed values; changes to their actual targets must refresh them.</summary>
    private static void UnfinishedInputs()
    {
        // Arrange: select the cross-byte Message type field and leave two deliberately unfinished edits.
        using var session = CreateSession();
        // Remember a new FakeUserInteractionService object as dialogs.
        var dialogs = new FakeUserInteractionService();
        // Remember a new InspectorViewModel object using the inputs in parentheses as inspector.
        var inspector = new InspectorViewModel(session, dialogs, _ => { });
        // Choose the named field with the supplied identity, synchronizing its highlighted bits and
        // interpretation.
        session.SelectField(session.Document.Fields.Single(field => field.Name == "Message type").Id);
        // Set inspector.ByteInput to the text "C".
        inspector.ByteInput = "C";
        // Set inspector.FieldValueInput to the text "0x".
        inspector.FieldValueInput = "0x";
        // Act/check: changing ruler/column appearance or byte 100 should not overwrite drafts aimed
        // at the selected field and byte 1. Those edits concern different state from these notifications.
        session.Document.Settings.ShowRuler = !session.Document.Settings.ShowRuler;
        // Add 8 to session.Document.Settings.OffsetWidth and keep the result there (+=).
        session.Document.Settings.OffsetWidth += 8;
        // Call session.Document.NotifySettingsChanged: Validates display settings and asks observers to
        // refresh; settings are not undo commands.
        session.Document.NotifySettingsChanged();
        // Check that inspector.ByteInput equals the expected "C"; a mismatch fails this test.
        Equal("C", inspector.ByteInput);
        // Check that inspector.FieldValueInput equals the expected "0x"; a mismatch fails this test.
        Equal("0x", inspector.FieldValueInput);
        // Ask the document to write the supplied value at the supplied byte offset, recording an undoable
        // edit if it changes.
        session.Document.SetByte(100, 0xAB);
        // Check that inspector.ByteInput equals the expected "C"; a mismatch fails this test.
        Equal("C", inspector.ByteInput);
        // Check that inspector.FieldValueInput equals the expected "0x"; a mismatch fails this test.
        Equal("0x", inspector.FieldValueInput);
        // Now change the actual field to 42 = binary 101010. Its final three bits replace the first
        // three bits of byte 1: 6C (01101100) becomes 4C (01001100). Both drafts should refresh because
        // their underlying target values changed, not merely because a generic notification arrived.
        session.Document.SetFieldValue(session.SelectedField!, 42);
        // Check that inspector.FieldValueInput equals the expected "42"; a mismatch fails this test.
        Equal("42", inspector.FieldValueInput);
        // Check that inspector.ByteInput equals the expected "4C"; a mismatch fails this test.
        Equal("4C", inspector.ByteInput);
    }

    /// <summary>Label commands must target the selected field, respect cancelled edits, and save the chosen assembly order.</summary>
    private static void LabelCommands()
    {
        // Arrange: select Enabled. The fake editor intentionally changes its draft and then returns
        // null, which represents Cancel; this detects accidental editing of the live field object.
        using var session = CreateSession();
        // Remember a new FakeUserInteractionService object as dialogs.
        var dialogs = new FakeUserInteractionService();
        // Remember a new FieldsViewModel object using the inputs in parentheses as fields.
        var fields = new FieldsViewModel(session, dialogs, _ => { });
        // Remember the only matching item; finding none or more than one reports an error as target.
        var target = session.Document.Fields.Single(field => field.Name == "Enabled");
        // Set fields.SelectedItem to the only matching item; finding none or more than one reports an
        // error.
        fields.SelectedItem = fields.Items.Single(field => field.Id == target.Id);
        // Set dialogs.FieldEditor to a small operation described after =>; it is saved for the receiving
        // code to call.
        dialogs.FieldEditor = (draft, _) => { draft.Name = "Canceled edit"; return null; };
        // Act/check: a cancelled rename must keep both the original name and the empty undo history.
        fields.EditLabelCommand.Execute(null);
        // Check that dialogs.FieldDrafts.Single().Id equals the expected target.Id; a mismatch fails this
        // test.
        Equal(target.Id, dialogs.FieldDrafts.Single().Id);
        // Check that session.Document.Fields.Single(field => field.Id == target.Id).Name equals the
        // expected "Enabled"; a mismatch fails this test.
        Equal("Enabled", session.Document.Fields.Single(field => field.Id == target.Id).Name);
        // Check that session.Document.CanUndo equals the expected false; a mismatch fails this test.
        Equal(false, session.Document.CanUndo);
        // Returning the draft represents Done. The accepted rename must be one undoable document edit.
        dialogs.FieldEditor = (draft, _) => { draft.Name = "Renamed enable"; return draft; };
        // Invoke fields.EditLabelCommand.Execute to perform the action stored in that command; what
        // changes depends on which command it is.
        fields.EditLabelCommand.Execute(null);
        // Check that session.Document.Fields.Single(field => field.Id == target.Id).Name equals the
        // expected "Renamed enable"; a mismatch fails this test.
        Equal("Renamed enable", session.Document.Fields.Single(field => field.Id == target.Id).Name);
        // Restore the state before the most recent undoable edit.
        session.Document.Undo();
        // Check that session.Document.Fields.Single(field => field.Id == target.Id).Name equals the
        // expected "Enabled"; a mismatch fails this test.
        Equal("Enabled", session.Document.Fields.Single(field => field.Id == target.Id).Name);

        // Create a new two-byte field while byte groups are interpreted in reverse order. Capture
        // that order before adding the label and ensure its saved mapping retains it exactly.
        session.SelectRawBits(Enumerable.Range(0, 16).Select(bit => (long)bit));
        // Set session.InterpretationOrder to InterpretationOrder.ReverseBytes.
        session.InterpretationOrder = InterpretationOrder.ReverseBytes;
        // Remember a separate array containing session.GetInterpretationBits()'s items as expectedOrder.
        var expectedOrder = session.GetInterpretationBits().ToArray();
        // Set dialogs.FieldEditor to a small operation described after =>; it is saved for the receiving
        // code to call.
        dialogs.FieldEditor = (draft, _) => { draft.Name = "Word"; return draft; };
        // Invoke fields.AddLabelCommand.Execute to perform the action stored in that command; what
        // changes depends on which command it is.
        fields.AddLabelCommand.Execute(null);
        // Check that session.SelectedField!.Name equals the expected "Word"; a mismatch fails this test.
        Equal("Word", session.SelectedField!.Name);
        // Check that session.SelectedField.OrderedBits has the same items in the same order as
        // expectedOrder.
        Sequence(expectedOrder, session.SelectedField.OrderedBits);
        // Restore the state before the most recent undoable edit.
        session.Document.Undo();
        // Check that session.SelectedField equals the expected null; a mismatch fails this test.
        Equal<NamedField?>(null, session.SelectedField);
        // Check that fields.Items.Count equals the expected 4; a mismatch fails this test.
        Equal(4, fields.Items.Count);
    }

    /// <summary>Byte order changes the interpreted number; bit-number labels change only descriptions, never source membership.</summary>
    private static void OrderedInterpretation()
    {
        // Arrange: select B3 6C as a 16-bit value. B36C(hex) is 179*256 + 108 = 45932.
        // Its high bit is set, so signed two's-complement interpretation is 45932 - 65536 = -19604.
        using var session = CreateSession();
        // Remember a new InspectorViewModel object using the inputs in parentheses as inspector.
        var inspector = new InspectorViewModel(session, new FakeUserInteractionService(), _ => { });
        // Request selection of the supplied physical bit addresses; the selection code checks its bounds
        // and limit.
        session.SelectRawBits(Enumerable.Range(0, 16).Select(bit => (long)bit));
        // Check that inspector.SelectionDecimal equals the expected "45932"; a mismatch fails this test.
        Equal("45932", inspector.SelectionDecimal);
        // Check that inspector.SelectionSigned equals the expected "-19604"; a mismatch fails this test.
        Equal("-19604", inspector.SelectionSigned);
        // Check that inspector.SelectionHex equals the expected "0xB36C"; a mismatch fails this test.
        Equal("0xB36C", inspector.SelectionHex);
        // Act: reverse byte groups. 6CB3(hex) is 108*256 + 179 = 27827. Its high bit is clear,
        // so signed and unsigned interpretations agree; the selected physical bits remain 0..15.
        inspector.InterpretationOrderIndex = 1;
        // Check that inspector.SelectionDecimal equals the expected "27827"; a mismatch fails this test.
        Equal("27827", inspector.SelectionDecimal);
        // Check that inspector.SelectionSigned equals the expected "27827"; a mismatch fails this test.
        Equal("27827", inspector.SelectionSigned);
        // Check that inspector.SelectionHex equals the expected "0x6CB3"; a mismatch fails this test.
        Equal("0x6CB3", inspector.SelectionHex);
        // Check that session.Selection.Bits has the same items in the same order as Enumerable.Range(0,
        // 16).Select(bit => (long)bit).
        Sequence(Enumerable.Range(0, 16).Select(bit => (long)bit), session.Selection.Bits);
        // Relabel the leftmost bit as bit 0. The number remains 27827, but its significance endpoints
        // should describe byte 1's leftmost bit and byte 0's rightmost bit with the new ruler labels.
        session.Document.Settings.BitNumbering = BitNumbering.MsbZero;
        // Call session.Document.NotifySettingsChanged: Validates display settings and asks observers to
        // refresh; settings are not undo commands.
        session.Document.NotifySettingsChanged();
        // Check that inspector.SelectionDecimal equals the expected "27827"; a mismatch fails this test.
        Equal("27827", inspector.SelectionDecimal);
        // Check that inspector.FieldEnds equals the expected "Field MSB → byte 1, bit 0\nField LSB → byte
        // 0, bit 7"; a mismatch fails this test.
        Equal("Field MSB → byte 1, bit 0\nField LSB → byte 0, bit 7", inspector.FieldEnds);
    }

    /// <summary>Display changes must preserve selected data and manual row grouping; invalid width updates must be atomic.</summary>
    private static void DisplayCommands()
    {
        // Arrange: select six bits spanning two bytes and remember both membership and the active cursor.
        using var session = CreateSession();
        // Remember a new FakeUserInteractionService object as dialogs.
        var dialogs = new FakeUserInteractionService();
        // Remember a new DisplaySettingsViewModel object using the inputs in parentheses as display.
        var display = new DisplaySettingsViewModel(session, dialogs, _ => { });
        // Request selection of the supplied physical bit addresses; the selection code checks its bounds
        // and limit.
        session.SelectRawBits([5, 6, 7, 8, 9, 10]);
        // Remember a separate array containing session.Selection.Bits's items as bits.
        var bits = session.Selection.Bits.ToArray();
        // Remember session.Selection.ActiveBit as active.
        long active = session.Selection.ActiveBit;
        // Set display.BytesPerRow to 16.
        display.BytesPerRow = 16;
        // Act/check: switch to bits and back. With automatic fitting disabled, the explicitly selected
        // 16 bytes per row must remain 16, even though each byte now occupies eight binary characters.
        display.SetBitsViewCommand.Execute(null);
        // Check that session.Document.Settings.ViewMode equals the expected DataViewMode.Bits; a mismatch
        // fails this test.
        Equal(DataViewMode.Bits, session.Document.Settings.ViewMode);
        // Check that session.Document.Settings.BytesPerRow equals the expected 16; a mismatch fails this
        // test.
        Equal(16, session.Document.Settings.BytesPerRow);
        // Invoke display.SetBytesViewCommand.Execute to perform the action stored in that command; what
        // changes depends on which command it is.
        display.SetBytesViewCommand.Execute(null);
        // Check that session.Selection.Bits has the same items in the same order as bits.
        Sequence(bits, session.Selection.Bits);
        // Check that session.Selection.ActiveBit equals the expected active; a mismatch fails this test.
        Equal(active, session.Selection.ActiveBit);
        // Check that session.Document.Settings.BytesPerRow equals the expected 16; a mismatch fails this
        // test.
        Equal(16, session.Document.Settings.BytesPerRow);
        // An unfinished width draft must survive an unrelated ruler toggle just like inspector drafts.
        display.DataWidthInput = "1";
        // Set display.ShowRuler to the opposite true-or-false answer to display.ShowRuler.
        display.ShowRuler = !display.ShowRuler;
        // Check that display.DataWidthInput equals the expected "1"; a mismatch fails this test.
        Equal("1", display.DataWidthInput);
        // Remember session.Document.Settings.OffsetWidth as previousOffset.
        double previousOffset = session.Document.Settings.OffsetWidth;
        // Remember session.Document.Settings.DataWidth as previousData.
        double previousData = session.Document.Settings.DataWidth;
        // Set display.OffsetWidthInput to the text "144".
        display.OffsetWidthInput = "144";
        // Set display.DataWidthInput to the text "not a width".
        display.DataWidthInput = "not a width";
        // Set display.AsciiWidthInput to the text "200".
        display.AsciiWidthInput = "200";
        // Apply all widths together. If the data width cannot be parsed, even the valid offset width
        // must remain unchanged: atomic validation means the update succeeds completely or not at all.
        display.ApplyWidthsCommand.Execute(null);
        // Check that session.Document.Settings.OffsetWidth equals the expected previousOffset; a mismatch
        // fails this test.
        Equal(previousOffset, session.Document.Settings.OffsetWidth);
        // Check that session.Document.Settings.DataWidth equals the expected previousData; a mismatch fails
        // this test.
        Equal(previousData, session.Document.Settings.DataWidth);
        // Check that dialogs.Errors.Count equals the expected 1; a mismatch fails this test.
        Equal(1, dialogs.Errors.Count);
        // Correct the invalid input and check the three requested widths, plus unchanged selected bits.
        display.DataWidthInput = "800";
        // Invoke display.ApplyWidthsCommand.Execute to perform the action stored in that command; what
        // changes depends on which command it is.
        display.ApplyWidthsCommand.Execute(null);
        // Check that session.Document.Settings.OffsetWidth equals the expected 144d; a mismatch fails this
        // test.
        Equal(144d, session.Document.Settings.OffsetWidth);
        // Check that session.Document.Settings.DataWidth equals the expected 800d; a mismatch fails this
        // test.
        Equal(800d, session.Document.Settings.DataWidth);
        // Check that session.Document.Settings.AsciiWidth equals the expected 200d; a mismatch fails this
        // test.
        Equal(200d, session.Document.Settings.AsciiWidth);
        // Check that session.Selection.Bits has the same items in the same order as bits.
        Sequence(bits, session.Selection.Bits);
    }
}
