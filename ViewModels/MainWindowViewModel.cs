using System.IO;
using System.Text.RegularExpressions;
using BitExplorer.Core;
using WpfApp1.Infrastructure;
using WpfApp1.Services;

namespace WpfApp1.ViewModels;

/// <summary>The application shell: composes panels and routes application-level commands.</summary>
public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IUserInteractionService _dialogs;
    private readonly DocumentWorkflow _documents;
    private readonly List<RelayCommand> _commands = [];
    private string _status = "Ready · Open a file, drop one here, or explore the example packet.";
    private bool _disposed;

    public MainWindowViewModel(IUserInteractionService dialogs, DocumentModel? document = null,
        string? displayName = null, IWorkspaceFiles? files = null)
    {
        _dialogs = dialogs;
        Session = new ExplorerSession(document ?? DocumentModel.CreateDemo(), displayName ?? "Example packet · demo");
        _documents = new DocumentWorkflow(Session, dialogs, files ?? new WorkspaceFiles(), ReportStatus);
        Inspector = new InspectorViewModel(Session, dialogs, ReportStatus);
        Fields = new FieldsViewModel(Session, dialogs, ReportStatus);
        Display = new DisplaySettingsViewModel(Session, dialogs, ReportStatus);
        OpenCommand = new AsyncRelayCommand(() => RunAsync(_documents.OpenAsync), () => IsNotBusy);
        ExportTextCommand = new AsyncRelayCommand(() => RunAsync(_documents.ExportTextAsync), () => IsNotBusy);
        SaveBinaryCommand = Command(() => SaveBinary());
        SaveProjectCommand = Command(() => SaveProject());
        UndoCommand = Command(() => { Session.Document.Undo(); ReportStatus("Undid the last edit."); }, () => Session.Document.CanUndo);
        RedoCommand = Command(() => { Session.Document.Redo(); ReportStatus("Redid the last edit."); }, () => Session.Document.CanRedo);
        ImportFieldsCommand = Command(_documents.ImportFields);
        ExportFieldsCommand = Command(_documents.ExportFields);
        GoToOffsetCommand = Command(GoToOffset, () => Session.Document.Data.Length > 0);
        NewFromHexCommand = Command(NewFromHex);
        ExampleCommand = Command(() => _documents.ReplaceWithNew(DocumentModel.CreateDemo(), "Example packet · demo", false));
        CopyCommand = Command(CopySelection, () => Session.Selection.Bits.Count > 0);
        HelpCommand = Command(() => dialogs.ShowHelp(ExplorerHelp.Text));
        FlipSelectionCommand = Command(() => Session.Document.FlipBit(Session.Selection.Bits.Single()), () => Session.Selection.Bits.Count == 1);
        Session.Changed += SessionChanged;
        Session.Selection.SelectionLimitReached += SelectionLimitReached;
    }

    public ExplorerSession Session { get; }
    public InspectorViewModel Inspector { get; }
    public FieldsViewModel Fields { get; }
    public DisplaySettingsViewModel Display { get; }
    public string DocumentTitle => Session.DisplayName;
    public string DocumentSubtitle => $"{Session.Document.Data.Length:N0} bytes · {Session.Document.Data.LongLength * 8:N0} bits · " +
        (Session.Document.ProjectPath ?? Session.Document.FilePath ?? "Data in memory");
    public string WindowTitle => $"{(HasUnsavedChanges ? "● " : "")}{DocumentTitle} — Bit Explorer";
    public string DirtyLabel => HasUnsavedChanges ? "UNSAVED CHANGES" : Session.Document.ProjectPath is not null ? "PROJECT SAVED" : "READY";
    public bool HasUnsavedChanges => _documents.HasUnsavedChanges;
    public bool IsBusy => Session.IsBusy;
    public bool IsNotBusy => !_disposed && !IsBusy;
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public AsyncRelayCommand OpenCommand { get; }
    public AsyncRelayCommand ExportTextCommand { get; }
    public RelayCommand SaveBinaryCommand { get; }
    public RelayCommand SaveProjectCommand { get; }
    public RelayCommand UndoCommand { get; }
    public RelayCommand RedoCommand { get; }
    public RelayCommand ImportFieldsCommand { get; }
    public RelayCommand ExportFieldsCommand { get; }
    public RelayCommand GoToOffsetCommand { get; }
    public RelayCommand NewFromHexCommand { get; }
    public RelayCommand ExampleCommand { get; }
    public RelayCommand CopyCommand { get; }
    public RelayCommand HelpCommand { get; }
    public RelayCommand FlipSelectionCommand { get; }
    public event EventHandler? GridFocusRequested;

    public Task OpenPathAsync(string path, bool confirm = true) => RunAsync(() => _documents.OpenPathAsync(path, confirm));
    public bool ConfirmClose() => !IsBusy && RunWithResult(_documents.ConfirmClose);
    public bool SaveBinary() => RunWithResult(_documents.SaveBinary);
    public bool SaveProject() => RunWithResult(_documents.SaveProject);
    public void NotifyGridLayoutChanged() { if (!_disposed) Session.Document.NotifySettingsChanged(); }
    public void ReportStatus(string message)
    {
        if (_disposed) return;
        Status = message;
        RefreshShell();
    }

    private void GoToOffset()
    {
        string? input = _dialogs.RequestText("Go to byte offset", "Decimal offset, or hexadecimal with a 0x prefix", "0x0");
        if (input is null) return;
        var value = NumericText.Parse(input);
        if (value < 0 || value >= Session.Document.Data.Length) throw new ArgumentException("The offset must be inside the current file.");
        Session.SelectRawBits(Enumerable.Range(0, 8).Select(bit => (long)value * 8 + bit));
        GridFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    private void NewFromHex()
    {
        string? input = _dialogs.RequestText("New from hexadecimal", "Paste bytes such as FF F0 A1, or a continuous hex string", "B3 6C 48 65 6C 6C 6F", true);
        if (input is null) return;
        input = Regex.Replace(input, "0x", "", RegexOptions.IgnoreCase);
        input = Regex.Replace(input, @"[\s,;:_-]", "");
        if (input.Length == 0 || input.Length % 2 != 0 || !Regex.IsMatch(input, "\\A[0-9a-fA-F]+\\z"))
            throw new ArgumentException("Enter complete hexadecimal bytes (two digits each).");
        var bytes = Convert.FromHexString(input);
        if (_documents.ReplaceWithNew(new DocumentModel(bytes), "Pasted data", true))
            ReportStatus($"Created {bytes.Length:N0} bytes from hex. Save binary or project to keep them.");
    }

    private void CopySelection()
    {
        var bits = Session.Selection.Bits.Order().ToList();
        if (bits.Count == 0) return;
        var document = Session.Document;
        string text = document.Settings.ViewMode == DataViewMode.Bits
            ? string.Join(" ", bits.GroupBy(b => b / 8).Select(group => string.Concat(group.Select(bit => (document.Data[(int)(bit / 8)] >> (7 - (int)(bit % 8))) & 1))))
            : string.Join(" ", bits.Select(b => b / 8).Distinct().Select(offset => DisplayFormatter.FormatByte(document.Data[(int)offset], document.Settings.ByteBase)));
        _dialogs.SetClipboardText(text);
        ReportStatus("Copied selected data in the current notation.");
    }

    private RelayCommand Command(Action action, Func<bool>? canExecute = null)
    {
        var command = new RelayCommand(() => Run(action), () => IsNotBusy && (canExecute?.Invoke() ?? true));
        _commands.Add(command);
        return command;
    }

    private void Run(Action operation)
    {
        if (!IsNotBusy) return;
        try { operation(); }
        catch (Exception error) { ReportError(error); }
        finally { RefreshShell(); }
    }

    private bool RunWithResult(Func<bool> operation)
    {
        if (!IsNotBusy) return false;
        try { return operation(); }
        catch (Exception error) { ReportError(error); return false; }
        finally { RefreshShell(); }
    }

    private async Task RunAsync(Func<Task> operation)
    {
        if (!IsNotBusy) return;
        try { await operation(); }
        catch (Exception error) { ReportError(error); }
        finally { RefreshShell(); }
    }

    private void ReportError(Exception error)
    {
        if (_disposed) return;
        ReportStatus(error.Message);
        _dialogs.ShowError(error.Message);
    }
    private void SessionChanged(object? sender, EventArgs e) => RefreshShell();
    private void SelectionLimitReached(object? sender, EventArgs e) => ReportStatus("Select up to 8 KiB at once. Export text always includes the entire file.");
    private void RefreshShell()
    {
        if (_disposed) return;
        RaiseProperties(nameof(DocumentTitle), nameof(DocumentSubtitle), nameof(WindowTitle), nameof(DirtyLabel), nameof(HasUnsavedChanges), nameof(IsBusy), nameof(IsNotBusy));
        foreach (var command in _commands) command.RaiseCanExecuteChanged();
        OpenCommand.RaiseCanExecuteChanged(); ExportTextCommand.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _documents.Dispose();
        Session.Changed -= SessionChanged;
        Session.Selection.SelectionLimitReached -= SelectionLimitReached;
        Inspector.Dispose(); Fields.Dispose(); Display.Dispose(); Session.Dispose();
    }
}
