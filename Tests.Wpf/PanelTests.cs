using System.Numerics;
using BitExplorer.Core;
using WpfApp1.ViewModels;

// These scenarios test view models directly, without opening a window. "partial" lets this file
// contribute methods to the same Program class as the test runner and the other scenario files.
// Fake dialogs supply scripted answers, so a test never pauses for a real user interaction.
internal static partial class Program
{
    /// <summary>Registers the panel behavior checks with the shared pass/fail test runner.</summary>
    private static void RunPanelTests()
    {
        Test("saved field assembly stays distinct from sorted source-bit membership", FieldAssemblyAndMembership);
        Test("mapping edits, deletion, and undo keep session and inspectors synchronized", FieldHistorySynchronization);
        Test("inspector edits update dependent values and undo without a view", InspectorCommands);
        Test("unfinished byte and field inputs survive unrelated display notifications", UnfinishedInputs);
        Test("label commands use the active field and cancellation leaves metadata unchanged", LabelCommands);
        Test("byte order and numbering change interpretation without changing source bits", OrderedInterpretation);
        Test("display controls preserve selection and explicit row grouping", DisplayCommands);
    }

    /// <summary>Creates the known demo packet and explicitly selects its first byte for repeatable tests.</summary>
    private static ExplorerSession CreateSession()
    {
        var session = new ExplorerSession(DocumentModel.CreateDemo(), "Regression packet");
        session.SelectRawBits(Enumerable.Range(0, 8).Select(bit => (long)bit));
        return session;
    }

    /// <summary>A field's value order must remain distinct from the sorted set of highlighted source bits.</summary>
    private static void FieldAssemblyAndMembership()
    {
        // Arrange: the packet begins B3 6C. Physical addresses count from each byte's highest bit.
        // Addresses [14, 3, 9, 0] contain [0, 1, 1, 1], so this saved order represents binary 0111 = 7.
        using var session = CreateSession();
        var dialogs = new FakeUserInteractionService();
        var inspector = new InspectorViewModel(session, dialogs, _ => { });
        var field = new NamedField { Name = "Scattered", OrderedBits = [14, 3, 9, 0] };
        session.Document.AddField(field);
        // Act: choose a named field. Check that highlighting is sorted for membership, while reading
        // the value follows the field's saved order rather than silently sorting its significance.
        session.SelectField(field.Id);
        Sequence(new long[] { 0, 3, 9, 14 }, session.Selection.Bits);
        Sequence(field.OrderedBits, session.GetInterpretationBits());
        Equal(InterpretationOrder.SavedField, session.InterpretationOrder);
        Equal("7", inspector.SelectionDecimal);
        // Act again: raw selection deliberately leaves named-field interpretation. The same physical
        // bits in file order contain [1, 1, 1, 0], giving binary 1110 = 14 without changing the document.
        session.SelectRawBits([0, 3, 9, 14]);
        Equal<NamedField?>(null, session.SelectedField);
        Equal(InterpretationOrder.FileOrder, session.InterpretationOrder);
        Equal("14", inspector.SelectionDecimal);
        Sequence(new long[] { 0, 3, 9, 14 }, session.GetInterpretationBits());
    }

    /// <summary>Editing, undoing, redoing, and deleting a field must update every dependent panel consistently.</summary>
    private static void FieldHistorySynchronization()
    {
        // Arrange: Message type uses physical bits 5..10 across B3 6C, whose values are 011011 = 27.
        // Clone the original mapping so undo can be compared with a stable snapshot.
        using var session = CreateSession();
        var dialogs = new FakeUserInteractionService();
        var inspector = new InspectorViewModel(session, dialogs, _ => { });
        var fields = new FieldsViewModel(session, dialogs, _ => { });
        var original = session.Document.Fields.Single(field => field.Name == "Message type").Clone();
        session.SelectField(original.Id);
        Equal("27", inspector.SelectionDecimal);
        var edited = original.Clone();
        edited.OrderedBits = [14, 3, 9, 0];
        // Act/check: replace the mapping with [14, 3, 9, 0], which reads 0111 = 7. Undo restores
        // the original mapping and value; redo reapplies the edited mapping and its interpretation.
        session.Document.UpdateField(edited);
        Sequence(edited.OrderedBits.Order(), session.Selection.Bits);
        Equal("7", inspector.SelectionDecimal);
        session.Document.Undo();
        Sequence(original.OrderedBits, session.GetInterpretationBits());
        Equal("27", inspector.SelectionDecimal);
        session.Document.Redo();
        Equal("7", inspector.SelectionDecimal);
        // Deleting the active label must leave raw selected bits but clear saved-field mode and its
        // editor. The demo starts with four labels: deletion leaves three, and undo restores four.
        fields.DeleteLabelCommand.Execute(null);
        Equal<NamedField?>(null, session.SelectedField);
        Equal(InterpretationOrder.FileOrder, session.InterpretationOrder);
        Equal(false, inspector.HasSelectedField);
        Equal(3, fields.Items.Count);
        session.Document.Undo();
        Equal(4, fields.Items.Count);
        Equal(true, fields.Items.Any(field => field.Id == original.Id));
    }

    /// <summary>Inspector commands must edit the shared document, refresh dependent values, and reject invalid bytes.</summary>
    private static void InspectorCommands()
    {
        // Arrange: B3 is binary 10110011. Its third physical bit (index 2) has weight 32.
        using var session = CreateSession();
        var dialogs = new FakeUserInteractionService();
        var inspector = new InspectorViewModel(session, dialogs, _ => { });
        var version = session.Document.Fields.Single(field => field.Name == "Version");
        // Act: flip that 1 to 0. Check B3 - 20(hex) = 93(hex) = 147(decimal), and the three-bit
        // Version field changes from 101(binary, 5) to 100(binary, 4). Undo must restore both views.
        inspector.Bits[2].FlipCommand.Execute(null);
        Equal((byte)0x93, session.Document.Data[0]);
        Equal(new BigInteger(4), session.Document.ReadField(version));
        Equal("93", inspector.ByteHex);
        Equal("147", inspector.ByteDecimal);
        session.Document.Undo();
        Equal("B3", inspector.ByteHex);
        Equal(true, inspector.Bits[2].IsSet);
        // FF is the largest valid byte, 255 decimal. In hex mode, "100" means 256, so rejection
        // must leave FF untouched and report one validation error through the fake dialog service.
        inspector.ByteInput = "FF";
        inspector.ApplyByteCommand.Execute(null);
        Equal((byte)255, session.Document.Data[0]);
        inspector.ByteInput = "100";
        inspector.ApplyByteCommand.Execute(null);
        Equal((byte)255, session.Document.Data[0]);
        Equal(1, dialogs.Errors.Count);
    }

    /// <summary>Unrelated refreshes must preserve partially typed values; changes to their actual targets must refresh them.</summary>
    private static void UnfinishedInputs()
    {
        // Arrange: select the cross-byte Message type field and leave two deliberately unfinished edits.
        using var session = CreateSession();
        var dialogs = new FakeUserInteractionService();
        var inspector = new InspectorViewModel(session, dialogs, _ => { });
        session.SelectField(session.Document.Fields.Single(field => field.Name == "Message type").Id);
        inspector.ByteInput = "C";
        inspector.FieldValueInput = "0x";
        // Act/check: changing ruler/column appearance or byte 100 should not overwrite drafts aimed
        // at the selected field and byte 1. Those edits concern different state from these notifications.
        session.Document.Settings.ShowRuler = !session.Document.Settings.ShowRuler;
        session.Document.Settings.OffsetWidth += 8;
        session.Document.NotifySettingsChanged();
        Equal("C", inspector.ByteInput);
        Equal("0x", inspector.FieldValueInput);
        session.Document.SetByte(100, 0xAB);
        Equal("C", inspector.ByteInput);
        Equal("0x", inspector.FieldValueInput);
        // Now change the actual field to 42 = binary 101010. Its final three bits replace the first
        // three bits of byte 1: 6C (01101100) becomes 4C (01001100). Both drafts should refresh because
        // their underlying target values changed, not merely because a generic notification arrived.
        session.Document.SetFieldValue(session.SelectedField!, 42);
        Equal("42", inspector.FieldValueInput);
        Equal("4C", inspector.ByteInput);
    }

    /// <summary>Label commands must target the selected field, respect cancelled edits, and save the chosen assembly order.</summary>
    private static void LabelCommands()
    {
        // Arrange: select Enabled. The fake editor intentionally changes its draft and then returns
        // null, which represents Cancel; this detects accidental editing of the live field object.
        using var session = CreateSession();
        var dialogs = new FakeUserInteractionService();
        var fields = new FieldsViewModel(session, dialogs, _ => { });
        var target = session.Document.Fields.Single(field => field.Name == "Enabled");
        fields.SelectedItem = fields.Items.Single(field => field.Id == target.Id);
        dialogs.FieldEditor = (draft, _) => { draft.Name = "Canceled edit"; return null; };
        // Act/check: a cancelled rename must keep both the original name and the empty undo history.
        fields.EditLabelCommand.Execute(null);
        Equal(target.Id, dialogs.FieldDrafts.Single().Id);
        Equal("Enabled", session.Document.Fields.Single(field => field.Id == target.Id).Name);
        Equal(false, session.Document.CanUndo);
        // Returning the draft represents Done. The accepted rename must be one undoable document edit.
        dialogs.FieldEditor = (draft, _) => { draft.Name = "Renamed enable"; return draft; };
        fields.EditLabelCommand.Execute(null);
        Equal("Renamed enable", session.Document.Fields.Single(field => field.Id == target.Id).Name);
        session.Document.Undo();
        Equal("Enabled", session.Document.Fields.Single(field => field.Id == target.Id).Name);

        // Create a new two-byte field while byte groups are interpreted in reverse order. Capture
        // that order before adding the label and ensure its saved mapping retains it exactly.
        session.SelectRawBits(Enumerable.Range(0, 16).Select(bit => (long)bit));
        session.InterpretationOrder = InterpretationOrder.ReverseBytes;
        var expectedOrder = session.GetInterpretationBits().ToArray();
        dialogs.FieldEditor = (draft, _) => { draft.Name = "Word"; return draft; };
        fields.AddLabelCommand.Execute(null);
        Equal("Word", session.SelectedField!.Name);
        Sequence(expectedOrder, session.SelectedField.OrderedBits);
        session.Document.Undo();
        Equal<NamedField?>(null, session.SelectedField);
        Equal(4, fields.Items.Count);
    }

    /// <summary>Byte order changes the interpreted number; bit-number labels change only descriptions, never source membership.</summary>
    private static void OrderedInterpretation()
    {
        // Arrange: select B3 6C as a 16-bit value. B36C(hex) is 179*256 + 108 = 45932.
        // Its high bit is set, so signed two's-complement interpretation is 45932 - 65536 = -19604.
        using var session = CreateSession();
        var inspector = new InspectorViewModel(session, new FakeUserInteractionService(), _ => { });
        session.SelectRawBits(Enumerable.Range(0, 16).Select(bit => (long)bit));
        Equal("45932", inspector.SelectionDecimal);
        Equal("-19604", inspector.SelectionSigned);
        Equal("0xB36C", inspector.SelectionHex);
        // Act: reverse byte groups. 6CB3(hex) is 108*256 + 179 = 27827. Its high bit is clear,
        // so signed and unsigned interpretations agree; the selected physical bits remain 0..15.
        inspector.InterpretationOrderIndex = 1;
        Equal("27827", inspector.SelectionDecimal);
        Equal("27827", inspector.SelectionSigned);
        Equal("0x6CB3", inspector.SelectionHex);
        Sequence(Enumerable.Range(0, 16).Select(bit => (long)bit), session.Selection.Bits);
        // Relabel the leftmost bit as bit 0. The number remains 27827, but its significance endpoints
        // should describe byte 1's leftmost bit and byte 0's rightmost bit with the new ruler labels.
        session.Document.Settings.BitNumbering = BitNumbering.MsbZero;
        session.Document.NotifySettingsChanged();
        Equal("27827", inspector.SelectionDecimal);
        Equal("Field MSB → byte 1, bit 0\nField LSB → byte 0, bit 7", inspector.FieldEnds);
    }

    /// <summary>Display changes must preserve selected data and manual row grouping; invalid width updates must be atomic.</summary>
    private static void DisplayCommands()
    {
        // Arrange: select six bits spanning two bytes and remember both membership and the active cursor.
        using var session = CreateSession();
        var dialogs = new FakeUserInteractionService();
        var display = new DisplaySettingsViewModel(session, dialogs, _ => { });
        session.SelectRawBits([5, 6, 7, 8, 9, 10]);
        var bits = session.Selection.Bits.ToArray();
        long active = session.Selection.ActiveBit;
        display.BytesPerRow = 16;
        // Act/check: switch to bits and back. With automatic fitting disabled, the explicitly selected
        // 16 bytes per row must remain 16, even though each byte now occupies eight binary characters.
        display.SetBitsViewCommand.Execute(null);
        Equal(DataViewMode.Bits, session.Document.Settings.ViewMode);
        Equal(16, session.Document.Settings.BytesPerRow);
        display.SetBytesViewCommand.Execute(null);
        Sequence(bits, session.Selection.Bits);
        Equal(active, session.Selection.ActiveBit);
        Equal(16, session.Document.Settings.BytesPerRow);
        // An unfinished width draft must survive an unrelated ruler toggle just like inspector drafts.
        display.DataWidthInput = "1";
        display.ShowRuler = !display.ShowRuler;
        Equal("1", display.DataWidthInput);
        double previousOffset = session.Document.Settings.OffsetWidth;
        double previousData = session.Document.Settings.DataWidth;
        display.OffsetWidthInput = "144";
        display.DataWidthInput = "not a width";
        display.AsciiWidthInput = "200";
        // Apply all widths together. If the data width cannot be parsed, even the valid offset width
        // must remain unchanged: atomic validation means the update succeeds completely or not at all.
        display.ApplyWidthsCommand.Execute(null);
        Equal(previousOffset, session.Document.Settings.OffsetWidth);
        Equal(previousData, session.Document.Settings.DataWidth);
        Equal(1, dialogs.Errors.Count);
        // Correct the invalid input and check the three requested widths, plus unchanged selected bits.
        display.DataWidthInput = "800";
        display.ApplyWidthsCommand.Execute(null);
        Equal(144d, session.Document.Settings.OffsetWidth);
        Equal(800d, session.Document.Settings.DataWidth);
        Equal(200d, session.Document.Settings.AsciiWidth);
        Sequence(bits, session.Selection.Bits);
    }
}
