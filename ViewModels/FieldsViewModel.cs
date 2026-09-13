using System.Collections.ObjectModel;
using BitExplorer.Core;
using WpfApp1.Infrastructure;
using WpfApp1.Services;

namespace WpfApp1.ViewModels;

/// <summary>Edits named field metadata while preserving stable list items and shared selection.</summary>
public sealed class FieldsViewModel : ObservableObject, IDisposable
{
    private static readonly string[] Colors = ["#54BFA6", "#79A9F5", "#D9AF66", "#B696E0", "#E58792", "#77C3D0"];
    private readonly ExplorerSession _session;
    private readonly IUserInteractionService _dialogs;
    private readonly Action<string> _reportStatus;

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

    public ObservableCollection<FieldItemViewModel> Items { get; } = [];
    public int Count => Items.Count;
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
    public RelayCommand AddLabelCommand { get; }
    public RelayCommand EditLabelCommand { get; }
    public RelayCommand DeleteLabelCommand { get; }

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

    private void EditLabel()
    {
        if (_session.SelectedField is not { } field) return;
        var result = _dialogs.EditField(field.Clone(), _session.Document.Data.LongLength * 8);
        if (result is null) return;
        _session.Document.UpdateField(result);
        _session.SelectField(result.Id);
        ReportField();
    }

    private void DeleteLabel()
    {
        if (_session.SelectedField is not { } field) return;
        _session.Document.RemoveField(field.Id);
        _reportStatus($"Removed the {field.Name} label. Ctrl+Z restores it; bytes are unchanged.");
    }

    private void ReportField()
    {
        if (_session.SelectedField is { } field)
            _reportStatus($"{field.Name} · {field.OrderedBits.Count} bits · {NumericText.DescribeSource(field.OrderedBits)}");
    }

    private void SessionChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        var fields = _session.Document.Fields;
        var ids = fields.Select(field => field.Id).ToHashSet();
        for (int index = Items.Count - 1; index >= 0; index--)
            if (!ids.Contains(Items[index].Id)) Items.RemoveAt(index);
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
        RaiseProperties(nameof(Count), nameof(SelectedItem));
        AddLabelCommand.RaiseCanExecuteChanged();
        EditLabelCommand.RaiseCanExecuteChanged();
        DeleteLabelCommand.RaiseCanExecuteChanged();
    }

    private void Run(Action action)
    {
        try { action(); }
        catch (Exception error) { _reportStatus(error.Message); _dialogs.ShowError(error.Message); }
    }

    public void Dispose() => _session.Changed -= SessionChanged;
}
