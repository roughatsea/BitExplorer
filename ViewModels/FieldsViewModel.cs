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

// Make names from System.Collections.ObjectModel available here without writing their full prefix each
// time. This does not run that library's code.
using System.Collections.ObjectModel;
// Make names from BitExplorer.Core available here without writing their full prefix each time. This does
// not run that library's code.
using BitExplorer.Core;
// Make names from WpfApp1.Infrastructure available here without writing their full prefix each time. This
// does not run that library's code.
using WpfApp1.Infrastructure;
// Make names from WpfApp1.Services available here without writing their full prefix each time. This does
// not run that library's code.
using WpfApp1.Services;

// Place this file's definitions in the WpfApp1.ViewModels naming group, which prevents clashes with names
// in other groups.
namespace WpfApp1.ViewModels;

/// <summary>
/// Supplies the named-fields list and its editing commands. Metadata means the
/// labels, colors, notes and bit maps that explain bytes without changing them.
/// List items keep their identity across refreshes, so editing a byte does not
/// rebuild the list or unexpectedly disturb the selected row.
/// </summary>
public sealed class FieldsViewModel : ObservableObject, IDisposable
{
    // New labels cycle through this palette; the editor can choose another color.
    private static readonly string[] Colors = ["#54BFA6", "#79A9F5", "#D9AF66", "#B696E0", "#E58792", "#77C3D0"];
    // Reserve _session to hold an object or value of type ExplorerSession; setup can supply its value,
    // otherwise the type's default is used.
    private readonly ExplorerSession _session;
    // Reserve _dialogs to hold an object or value of type IUserInteractionService; setup can supply its
    // value, otherwise the type's default is used.
    private readonly IUserInteractionService _dialogs;
    // Reserve _reportStatus to hold an object or value of type Action<string>; setup can supply its value,
    // otherwise the type's default is used.
    private readonly Action<string> _reportStatus;

    /// <summary>Creates bounded label commands and observes the shared document/selection session.</summary>
    public FieldsViewModel(ExplorerSession session, IUserInteractionService dialogs, Action<string> reportStatus)
    {
        // Set _session to session.
        _session = session;
        // Set _dialogs to dialogs.
        _dialogs = dialogs;
        // Set _reportStatus to reportStatus.
        _reportStatus = reportStatus;
        // Prepare AddLabelCommand for later activation: call Run: Runs an operation on the calling thread
        // and converts exceptions into a message and a false result. It can run when both both it is not
        // the case that session.IsBusy is true and session.Selection.Bits.Count matches > 0 and <=
        // DocumentModel.MaxFieldBits and session.Document.Fields.Count is less than
        // DocumentModel.MaxFields. Creating the command here does not perform its action.
        AddLabelCommand = new RelayCommand(() => Run(AddLabel), () => !session.IsBusy && session.Selection.Bits.Count is > 0 and <= DocumentModel.MaxFieldBits && session.Document.Fields.Count < DocumentModel.MaxFields);
        // Prepare EditLabelCommand for later activation: call Run: Runs an operation on the calling thread
        // and converts exceptions into a message and a false result. It can run when both it is not the
        // case that session.IsBusy is true and session.SelectedField matches not null. Creating the command
        // here does not perform its action.
        EditLabelCommand = new RelayCommand(() => Run(EditLabel), () => !session.IsBusy && session.SelectedField is not null);
        // Prepare DeleteLabelCommand for later activation: call Run: Runs an operation on the calling
        // thread and converts exceptions into a message and a false result. It can run when both it is not
        // the case that session.IsBusy is true and session.SelectedField matches not null. Creating the
        // command here does not perform its action.
        DeleteLabelCommand = new RelayCommand(() => Run(DeleteLabel), () => !session.IsBusy && session.SelectedField is not null);
        // Register SessionChanged as a listener for session.Changed; the listener runs when that event is
        // raised.
        session.Changed += SessionChanged;
        // Run Refresh to recalculate the values this part of the application presents.
        Refresh();
    }

    /// <summary>Rows bound by the list; collection notifications tell WPF about insertions, removals and moves.</summary>
    public ObservableCollection<FieldItemViewModel> Items { get; } = [];
    /// <summary>The current number of labels, displayed next to the fields heading.</summary>
    public int Count => Items.Count;
    /// <summary>
    /// Adapts the selected list row to the session's field ID. The getter looks up
    /// shared state instead of storing a second, potentially stale selected field.
    /// </summary>
    public FieldItemViewModel? SelectedItem
    {
        // When this property is read, return the first matching item, or the type's default (usually null
        // here) when none matches.
        get => Items.FirstOrDefault(item => item.Id == _session.SelectedField?.Id);
        // When this property is written, the proposed new content is called value; run this setter to
        // decide how to store it.
        set
        {
            // Collection refreshes may temporarily clear a ListBox selection. The session
            // remains authoritative; raw grid selection is what leaves field context.
            if (value is null || value.Id == _session.SelectedField?.Id || _session.IsBusy) return;
            // Choose the named field with the supplied identity, synchronizing its highlighted bits and
            // interpretation.
            _session.SelectField(value.Id);
            // Call ReportField: Describes the selected label and its source-byte span in the status bar.
            ReportField();
        }
    }
    /// <summary>Creates a label when the selection fits the field and document limits.</summary>
    public RelayCommand AddLabelCommand { get; }
    /// <summary>Edits the active label; disabled when no field is selected or file work is busy.</summary>
    public RelayCommand EditLabelCommand { get; }
    /// <summary>Removes only the active label, leaving binary data unchanged and allowing undo.</summary>
    public RelayCommand DeleteLabelCommand { get; }

    /// <summary>
    /// Builds an editable draft in the current interpretation order. In particular,
    /// a reverse-byte selection must keep that order when it becomes a saved field.
    /// </summary>
    private void AddLabel()
    {
        // Remember a new NamedField object; the entries in braces set its initial contents or properties as
        // draft.
        var draft = new NamedField
        {
            // Set this new object's Name entry to the displayed text below; each {...} inserts a computed
            // value into the text.
            Name = $"Field {Count + 1}",
            // Set this new object's OrderedBits entry to a separate list containing
            // _session.GetInterpretationBits()'s items.
            OrderedBits = _session.GetInterpretationBits().ToList(),
            // Set this new object's Color entry to Colors[Count % Colors.Length].
            Color = Colors[Count % Colors.Length]
        };
        // Remember the result returned by _dialogs.EditField(...) as result.
        var result = _dialogs.EditField(draft, _session.Document.Data.LongLength * 8);
        // If result matches null, leave this method immediately.
        if (result is null) return;
        // Call _session.Document.AddField: Adds a validated, independently copied field in one undo step;
        // IDs must be unique.
        _session.Document.AddField(result);
        // Choose the named field with the supplied identity, synchronizing its highlighted bits and
        // interpretation.
        _session.SelectField(result.Id);
        // Call ReportField: Describes the selected label and its source-byte span in the status bar.
        ReportField();
    }

    /// <summary>Edits a cloned field and commits only an accepted result, keeping cancellation harmless.</summary>
    private void EditLabel()
    {
        // If _session.SelectedField matches not { } field, leave this method immediately.
        if (_session.SelectedField is not { } field) return;
        // Remember the result returned by _dialogs.EditField(...) as result.
        var result = _dialogs.EditField(field.Clone(), _session.Document.Data.LongLength * 8);
        // If result matches null, leave this method immediately.
        if (result is null) return;
        // Call _session.Document.UpdateField: Replaces the matching field from an edited draft while
        // preserving its original state for Undo.
        _session.Document.UpdateField(result);
        // Choose the named field with the supplied identity, synchronizing its highlighted bits and
        // interpretation.
        _session.SelectField(result.Id);
        // Call ReportField: Describes the selected label and its source-byte span in the status bar.
        ReportField();
    }

    /// <summary>Records removal through the model's undoable field operation.</summary>
    private void DeleteLabel()
    {
        // If _session.SelectedField matches not { } field, leave this method immediately.
        if (_session.SelectedField is not { } field) return;
        // Call _session.Document.RemoveField: Removes a label without changing any bytes; an unknown ID is
        // a no-op.
        _session.Document.RemoveField(field.Id);
        // Send the supplied message to the application's status display.
        _reportStatus($"Removed the {field.Name} label. Ctrl+Z restores it; bytes are unchanged.");
    }

    /// <summary>Describes the selected label and its source-byte span in the status bar.</summary>
    private void ReportField()
    {
        // If _session.SelectedField matches { } field, send the supplied message to the application's
        // status display.
        if (_session.SelectedField is { } field)
            // Send the supplied message to the application's status display.
            _reportStatus($"{field.Name} · {field.OrderedBits.Count} bits · {NumericText.DescribeSource(field.OrderedBits)}");
    }

    /// <summary>Observes edits, selection changes and undo/redo through one shared event.</summary>
    private void SessionChanged(object? sender, EventArgs e) => Refresh();

    /// <summary>
    /// Reconciles rows by field ID: remove missing rows, insert new ones, move
    /// existing ones if needed, then update their text without replacing objects.
    /// </summary>
    private void Refresh()
    {
        // Remember _session.Document.Fields as fields.
        var fields = _session.Document.Fields;
        // Remember the result returned by fields.Select(field => field.Id).ToHashSet() as ids.
        var ids = fields.Select(field => field.Id).ToHashSet();
        // Remove backwards so deleting an item does not shift an unvisited index.
        for (int index = Items.Count - 1; index >= 0; index--)
            // If it is not the case that ids.Contains(Items[index].Id) is true, call Items.RemoveAt(...);
            // the values in parentheses are the inputs.
            if (!ids.Contains(Items[index].Id)) Items.RemoveAt(index);
        // The dictionary finds an existing row by stable ID without repeatedly searching the list.
        var existing = Items.ToDictionary(item => item.Id);
        // Repeat with int index = 0 as the starting state, while index is less than fields.Count; after
        // each pass, increase index by one. The braces contain one pass.
        for (int index = 0; index < fields.Count; index++)
        {
            // Remember fields[index] as field.
            var field = fields[index];
            // If it is not the case that existing.TryGetValue(field.Id, out var item) is true, run the
            // following grouped instructions.
            if (!existing.TryGetValue(field.Id, out var item))
            {
                // Set item to a new FieldItemViewModel object using the inputs in parentheses.
                item = new FieldItemViewModel(field.Id);
                // Call Items.Insert(...); the values in parentheses are the inputs.
                Items.Insert(index, item);
            }
            // Otherwise, try this next condition: !ReferenceEquals(Items[index], item).
            else if (!ReferenceEquals(Items[index], item)) Items.Move(Items.IndexOf(item), index);
            // Refresh this existing field-list item from the current field and document, preserving the
            // item object used by selection bindings.
            item.Update(field, _session.Document);
        }
        // Row changes and selected-field changes can also alter button availability.
        RaiseProperties(nameof(Count), nameof(SelectedItem));
        // Ask controls using AddLabelCommand to check again whether the command is allowed to run.
        AddLabelCommand.RaiseCanExecuteChanged();
        // Ask controls using EditLabelCommand to check again whether the command is allowed to run.
        EditLabelCommand.RaiseCanExecuteChanged();
        // Ask controls using DeleteLabelCommand to check again whether the command is allowed to run.
        DeleteLabelCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Reports dialog/model errors consistently while leaving the application usable.</summary>
    private void Run(Action action)
    {
        // Attempt this work. A matching catch below handles a reported exception; a finally block, when
        // present, performs cleanup on the way out.
        try { action(); }
        // If the preceding try reports Exception, refer to it as error; run this recovery path.
        catch (Exception error) { _reportStatus(error.Message); _dialogs.ShowError(error.Message); }
    }

    /// <summary>Releases the shared event subscription when the panel is no longer needed.</summary>
    public void Dispose() => _session.Changed -= SessionChanged;
}
