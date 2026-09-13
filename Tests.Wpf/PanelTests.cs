using System.Numerics;
using BitExplorer.Core;
using WpfApp1.ViewModels;

internal static partial class Program
{
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

    private static ExplorerSession CreateSession()
    {
        var session = new ExplorerSession(DocumentModel.CreateDemo(), "Regression packet");
        session.SelectRawBits(Enumerable.Range(0, 8).Select(bit => (long)bit));
        return session;
    }

    private static void FieldAssemblyAndMembership()
    {
        using var session = CreateSession();
        var dialogs = new FakeUserInteractionService();
        var inspector = new InspectorViewModel(session, dialogs, _ => { });
        var field = new NamedField { Name = "Scattered", OrderedBits = [14, 3, 9, 0] };
        session.Document.AddField(field);
        session.SelectField(field.Id);
        Sequence(new long[] { 0, 3, 9, 14 }, session.Selection.Bits);
        Sequence(field.OrderedBits, session.GetInterpretationBits());
        Equal(InterpretationOrder.SavedField, session.InterpretationOrder);
        Equal("7", inspector.SelectionDecimal);
        session.SelectRawBits([0, 3, 9, 14]);
        Equal<NamedField?>(null, session.SelectedField);
        Equal(InterpretationOrder.FileOrder, session.InterpretationOrder);
        Equal("14", inspector.SelectionDecimal);
        Sequence(new long[] { 0, 3, 9, 14 }, session.GetInterpretationBits());
    }

    private static void FieldHistorySynchronization()
    {
        using var session = CreateSession();
        var dialogs = new FakeUserInteractionService();
        var inspector = new InspectorViewModel(session, dialogs, _ => { });
        var fields = new FieldsViewModel(session, dialogs, _ => { });
        var original = session.Document.Fields.Single(field => field.Name == "Message type").Clone();
        session.SelectField(original.Id);
        Equal("27", inspector.SelectionDecimal);
        var edited = original.Clone();
        edited.OrderedBits = [14, 3, 9, 0];
        session.Document.UpdateField(edited);
        Sequence(edited.OrderedBits.Order(), session.Selection.Bits);
        Equal("7", inspector.SelectionDecimal);
        session.Document.Undo();
        Sequence(original.OrderedBits, session.GetInterpretationBits());
        Equal("27", inspector.SelectionDecimal);
        session.Document.Redo();
        Equal("7", inspector.SelectionDecimal);
        fields.DeleteLabelCommand.Execute(null);
        Equal<NamedField?>(null, session.SelectedField);
        Equal(InterpretationOrder.FileOrder, session.InterpretationOrder);
        Equal(false, inspector.HasSelectedField);
        Equal(3, fields.Items.Count);
        session.Document.Undo();
        Equal(4, fields.Items.Count);
        Equal(true, fields.Items.Any(field => field.Id == original.Id));
    }

    private static void InspectorCommands()
    {
        using var session = CreateSession();
        var dialogs = new FakeUserInteractionService();
        var inspector = new InspectorViewModel(session, dialogs, _ => { });
        var version = session.Document.Fields.Single(field => field.Name == "Version");
        inspector.Bits[2].FlipCommand.Execute(null);
        Equal((byte)0x93, session.Document.Data[0]);
        Equal(new BigInteger(4), session.Document.ReadField(version));
        Equal("93", inspector.ByteHex);
        Equal("147", inspector.ByteDecimal);
        session.Document.Undo();
        Equal("B3", inspector.ByteHex);
        Equal(true, inspector.Bits[2].IsSet);
        inspector.ByteInput = "FF";
        inspector.ApplyByteCommand.Execute(null);
        Equal((byte)255, session.Document.Data[0]);
        inspector.ByteInput = "100";
        inspector.ApplyByteCommand.Execute(null);
        Equal((byte)255, session.Document.Data[0]);
        Equal(1, dialogs.Errors.Count);
    }

    private static void UnfinishedInputs()
    {
        using var session = CreateSession();
        var dialogs = new FakeUserInteractionService();
        var inspector = new InspectorViewModel(session, dialogs, _ => { });
        session.SelectField(session.Document.Fields.Single(field => field.Name == "Message type").Id);
        inspector.ByteInput = "C";
        inspector.FieldValueInput = "0x";
        session.Document.Settings.ShowRuler = !session.Document.Settings.ShowRuler;
        session.Document.Settings.OffsetWidth += 8;
        session.Document.NotifySettingsChanged();
        Equal("C", inspector.ByteInput);
        Equal("0x", inspector.FieldValueInput);
        session.Document.SetByte(100, 0xAB);
        Equal("C", inspector.ByteInput);
        Equal("0x", inspector.FieldValueInput);
        session.Document.SetFieldValue(session.SelectedField!, 42);
        Equal("42", inspector.FieldValueInput);
        Equal("4C", inspector.ByteInput);
    }

    private static void LabelCommands()
    {
        using var session = CreateSession();
        var dialogs = new FakeUserInteractionService();
        var fields = new FieldsViewModel(session, dialogs, _ => { });
        var target = session.Document.Fields.Single(field => field.Name == "Enabled");
        fields.SelectedItem = fields.Items.Single(field => field.Id == target.Id);
        dialogs.FieldEditor = (draft, _) => { draft.Name = "Canceled edit"; return null; };
        fields.EditLabelCommand.Execute(null);
        Equal(target.Id, dialogs.FieldDrafts.Single().Id);
        Equal("Enabled", session.Document.Fields.Single(field => field.Id == target.Id).Name);
        Equal(false, session.Document.CanUndo);
        dialogs.FieldEditor = (draft, _) => { draft.Name = "Renamed enable"; return draft; };
        fields.EditLabelCommand.Execute(null);
        Equal("Renamed enable", session.Document.Fields.Single(field => field.Id == target.Id).Name);
        session.Document.Undo();
        Equal("Enabled", session.Document.Fields.Single(field => field.Id == target.Id).Name);

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

    private static void OrderedInterpretation()
    {
        using var session = CreateSession();
        var inspector = new InspectorViewModel(session, new FakeUserInteractionService(), _ => { });
        session.SelectRawBits(Enumerable.Range(0, 16).Select(bit => (long)bit));
        Equal("45932", inspector.SelectionDecimal);
        Equal("-19604", inspector.SelectionSigned);
        Equal("0xB36C", inspector.SelectionHex);
        inspector.InterpretationOrderIndex = 1;
        Equal("27827", inspector.SelectionDecimal);
        Equal("27827", inspector.SelectionSigned);
        Equal("0x6CB3", inspector.SelectionHex);
        Sequence(Enumerable.Range(0, 16).Select(bit => (long)bit), session.Selection.Bits);
        session.Document.Settings.BitNumbering = BitNumbering.MsbZero;
        session.Document.NotifySettingsChanged();
        Equal("27827", inspector.SelectionDecimal);
        Equal("Field MSB → byte 1, bit 0\nField LSB → byte 0, bit 7", inspector.FieldEnds);
    }

    private static void DisplayCommands()
    {
        using var session = CreateSession();
        var dialogs = new FakeUserInteractionService();
        var display = new DisplaySettingsViewModel(session, dialogs, _ => { });
        session.SelectRawBits([5, 6, 7, 8, 9, 10]);
        var bits = session.Selection.Bits.ToArray();
        long active = session.Selection.ActiveBit;
        display.BytesPerRow = 16;
        display.SetBitsViewCommand.Execute(null);
        Equal(DataViewMode.Bits, session.Document.Settings.ViewMode);
        Equal(16, session.Document.Settings.BytesPerRow);
        display.SetBytesViewCommand.Execute(null);
        Sequence(bits, session.Selection.Bits);
        Equal(active, session.Selection.ActiveBit);
        Equal(16, session.Document.Settings.BytesPerRow);
        display.DataWidthInput = "1";
        display.ShowRuler = !display.ShowRuler;
        Equal("1", display.DataWidthInput);
        double previousOffset = session.Document.Settings.OffsetWidth;
        double previousData = session.Document.Settings.DataWidth;
        display.OffsetWidthInput = "144";
        display.DataWidthInput = "not a width";
        display.AsciiWidthInput = "200";
        display.ApplyWidthsCommand.Execute(null);
        Equal(previousOffset, session.Document.Settings.OffsetWidth);
        Equal(previousData, session.Document.Settings.DataWidth);
        Equal(1, dialogs.Errors.Count);
        display.DataWidthInput = "800";
        display.ApplyWidthsCommand.Execute(null);
        Equal(144d, session.Document.Settings.OffsetWidth);
        Equal(800d, session.Document.Settings.DataWidth);
        Equal(200d, session.Document.Settings.AsciiWidth);
        Sequence(bits, session.Selection.Bits);
    }
}
