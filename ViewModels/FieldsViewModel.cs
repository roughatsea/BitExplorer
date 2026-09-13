using System.Collections.ObjectModel;
using BitExplorer.Core;
using WpfApp1.Infrastructure;
using WpfApp1.Services;

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
    private readonly ExplorerSession _session;
    private readonly IUserInteractionService _dialogs;
    private readonly Action<string> _reportStatus;

    /// <summary>Creates bounded label commands and observes the shared document/selection session.</summary>
    public FieldsViewModel(ExplorerSession session, IUserInteractionService dialogs, Action<string> reportStatus)
    {
        _session = session;
        _dialogs = dialogs;
        _reportStatus = reportStatus;
        AddLabelCommand = new RelayCommand(() => Run(AddLabel), () => !session.IsBusy && session.Selection.Bits.Count is > 0 and <= DocumentModel.MaxFieldBits && session.Document.Fields.Count < DocumentModel.MaxFields);
        EditLabelCommand = new RelayCommand(() => Run(EditLabel), () => !session.IsBusy && session.SelectedField is not null);
        DeleteLabelCommand = new RelayCommand(() => Run(DeleteLabel), () => !session.IsBusy && session.SelectedField is not null);
        session.Changed += SessionChanged;
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
        get => Items.FirstOrDefault(item => item.Id == _session.SelectedField?.Id);
        set
        {
            // Collection refreshes may temporarily clear a ListBox selection. The session
            // remains authoritative; raw grid selection is what leaves field context.
            if (value is null || value.Id == _session.SelectedField?.Id || _session.IsBusy) return;
            _session.SelectField(value.Id);
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
        var draft = new NamedField
        {
            Name = $"Field {Count + 1}",
            OrderedBits = _session.GetInterpretationBits().ToList(),
            Color = Colors[Count % Colors.Length]
        };
        var result = _dialogs.EditField(draft, _session.Document.Data.LongLength * 8);
        if (result is null) return;
        _session.Document.AddField(result);
        _session.SelectField(result.Id);
        ReportField();
    }

    /// <summary>Edits a cloned field and commits only an accepted result, keeping cancellation harmless.</summary>
    private void EditLabel()
    {
        if (_session.SelectedField is not { } field) return;
        var result = _dialogs.EditField(field.Clone(), _session.Document.Data.LongLength * 8);
        if (result is null) return;
        _session.Document.UpdateField(result);
        _session.SelectField(result.Id);
        ReportField();
    }

    /// <summary>Records removal through the model's undoable field operation.</summary>
    private void DeleteLabel()
    {
        if (_session.SelectedField is not { } field) return;
        _session.Document.RemoveField(field.Id);
        _reportStatus($"Removed the {field.Name} label. Ctrl+Z restores it; bytes are unchanged.");
    }

    /// <summary>Describes the selected label and its source-byte span in the status bar.</summary>
    private void ReportField()
    {
        if (_session.SelectedField is { } field)
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
        var fields = _session.Document.Fields;
        var ids = fields.Select(field => field.Id).ToHashSet();
        // Remove backwards so deleting an item does not shift an unvisited index.
        for (int index = Items.Count - 1; index >= 0; index--)
            if (!ids.Contains(Items[index].Id)) Items.RemoveAt(index);
        // The dictionary finds an existing row by stable ID without repeatedly searching the list.
        var existing = Items.ToDictionary(item => item.Id);
        for (int index = 0; index < fields.Count; index++)
        {
            var field = fields[index];
            if (!existing.TryGetValue(field.Id, out var item))
            {
                item = new FieldItemViewModel(field.Id);
                Items.Insert(index, item);
            }
            else if (!ReferenceEquals(Items[index], item)) Items.Move(Items.IndexOf(item), index);
            item.Update(field, _session.Document);
        }
        // Row changes and selected-field changes can also alter button availability.
        RaiseProperties(nameof(Count), nameof(SelectedItem));
        AddLabelCommand.RaiseCanExecuteChanged();
        EditLabelCommand.RaiseCanExecuteChanged();
        DeleteLabelCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Reports dialog/model errors consistently while leaving the application usable.</summary>
    private void Run(Action action)
    {
        try { action(); }
        catch (Exception error) { _reportStatus(error.Message); _dialogs.ShowError(error.Message); }
    }

    /// <summary>Releases the shared event subscription when the panel is no longer needed.</summary>
    public void Dispose() => _session.Changed -= SessionChanged;
}
