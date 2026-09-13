using System.IO;
using System.Text.RegularExpressions;
using BitExplorer.Core;
using WpfApp1.Infrastructure;
using WpfApp1.Services;

namespace WpfApp1.ViewModels;

/// <summary>
/// The application's top-level view model. In Model-View-ViewModel (MVVM), the
/// model stores data, XAML describes visible controls, and view models expose
/// values and actions those controls bind to. This shell creates focused panel
/// view models and routes whole-application commands to the document workflow.
/// </summary>
public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IUserInteractionService _dialogs;
    private readonly DocumentWorkflow _documents;
    // Keep commands together so changes to selection, undo history or busy state
    // can ask every button to reevaluate whether its action is currently available.
    private readonly List<RelayCommand> _commands = [];
    private string _status = "Ready · Open a file, drop one here, or explore the example packet.";
    private bool _disposed;

    /// <summary>
    /// Builds the session and panels. Optional document/files arguments make this
    /// shell testable without opening dialogs or touching disk; null arguments
    /// choose the normal demo document and real file implementation.
    /// </summary>
    public MainWindowViewModel(IUserInteractionService dialogs, DocumentModel? document = null,
        string? displayName = null, IWorkspaceFiles? files = null)
    {
        _dialogs = dialogs;
        // The ?? operator uses the right-hand fallback only when the left side is null.
        Session = new ExplorerSession(document ?? DocumentModel.CreateDemo(), displayName ?? "Example packet · demo");
        _documents = new DocumentWorkflow(Session, dialogs, files ?? new WorkspaceFiles(), ReportStatus);
        Inspector = new InspectorViewModel(Session, dialogs, ReportStatus);
        Fields = new FieldsViewModel(Session, dialogs, ReportStatus);
        Display = new DisplaySettingsViewModel(Session, dialogs, ReportStatus);
        // A command combines an action with CanExecute, the rule that enables its button.
        // Async commands await slow file work while keeping the window responsive.
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

    /// <summary>Shared document, bit selection, field context and busy state for the whole window.</summary>
    public ExplorerSession Session { get; }
    /// <summary>Values and editing commands bound by the inspector panel.</summary>
    public InspectorViewModel Inspector { get; }
    /// <summary>Named-field list, selection and label-editing commands.</summary>
    public FieldsViewModel Fields { get; }
    /// <summary>Formatting preferences and validated column-width drafts.</summary>
    public DisplaySettingsViewModel Display { get; }
    /// <summary>The filename or in-memory name shown in the document header.</summary>
    public string DocumentTitle => Session.DisplayName;
    /// <summary>Human-readable file size and source path; N0 adds digit grouping without decimal places.</summary>
    public string DocumentSubtitle => $"{Session.Document.Data.Length:N0} bytes · {Session.Document.Data.LongLength * 8:N0} bits · " +
        (Session.Document.ProjectPath ?? Session.Document.FilePath ?? "Data in memory");
    /// <summary>The operating-system window title, including an unsaved-change marker.</summary>
    public string WindowTitle => $"{(HasUnsavedChanges ? "● " : "")}{DocumentTitle} — Bit Explorer";
    /// <summary>Short save-state text displayed in the header badge.</summary>
    public string DirtyLabel => HasUnsavedChanges ? "UNSAVED CHANGES" : Session.Document.ProjectPath is not null ? "PROJECT SAVED" : "READY";
    /// <summary>Whether edits or newly pasted data still need saving.</summary>
    public bool HasUnsavedChanges => _documents.HasUnsavedChanges;
    /// <summary>Whether a file operation currently prevents interaction.</summary>
    public bool IsBusy => Session.IsBusy;
    /// <summary>True only while this shell is alive and ready for another command.</summary>
    public bool IsNotBusy => !_disposed && !IsBusy;
    /// <summary>The status-bar message; SetProperty notifies WPF when its value actually changes.</summary>
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    /// <summary>Chooses and asynchronously opens a binary or saved project.</summary>
    public AsyncRelayCommand OpenCommand { get; }
    /// <summary>Asynchronously exports the entire document in its current text format.</summary>
    public AsyncRelayCommand ExportTextCommand { get; }
    /// <summary>Saves edited bytes to a binary file.</summary>
    public RelayCommand SaveBinaryCommand { get; }
    /// <summary>Saves data, field definitions and display preferences together.</summary>
    public RelayCommand SaveProjectCommand { get; }
    /// <summary>Reverts the last model edit when undo history exists.</summary>
    public RelayCommand UndoCommand { get; }
    /// <summary>Reapplies a previously undone model edit when redo history exists.</summary>
    public RelayCommand RedoCommand { get; }
    /// <summary>Replaces field definitions from a reusable layout file.</summary>
    public RelayCommand ImportFieldsCommand { get; }
    /// <summary>Saves the current field definitions for reuse with another binary.</summary>
    public RelayCommand ExportFieldsCommand { get; }
    /// <summary>Asks for an offset, selects its byte and returns keyboard focus to the grid.</summary>
    public RelayCommand GoToOffsetCommand { get; }
    /// <summary>Creates a new document from pasted hexadecimal byte text.</summary>
    public RelayCommand NewFromHexCommand { get; }
    /// <summary>Reopens the annotated example after resolving any unsaved work.</summary>
    public RelayCommand ExampleCommand { get; }
    /// <summary>Copies selected source data in the current byte or bit notation.</summary>
    public RelayCommand CopyCommand { get; }
    /// <summary>Opens the keyboard and usage help dialog.</summary>
    public RelayCommand HelpCommand { get; }
    /// <summary>Flips a bit only when the grid has exactly one selected source bit.</summary>
    public RelayCommand FlipSelectionCommand { get; }
    /// <summary>Requests a UI-only focus change without holding a reference to the grid control.</summary>
    public event EventHandler? GridFocusRequested;

    /// <summary>Handles paths already known from startup arguments or drag-and-drop.</summary>
    public Task OpenPathAsync(string path, bool confirm = true) => RunAsync(() => _documents.OpenPathAsync(path, confirm));
    /// <summary>Asks whether the window may close, including the save/discard/cancel workflow.</summary>
    public bool ConfirmClose() => !IsBusy && RunWithResult(_documents.ConfirmClose);
    /// <summary>Runs binary saving with common error handling and reports success to callers.</summary>
    public bool SaveBinary() => RunWithResult(_documents.SaveBinary);
    /// <summary>Runs project saving with common error handling and reports success to callers.</summary>
    public bool SaveProject() => RunWithResult(_documents.SaveProject);
    /// <summary>Publishes settings changed by a grid divider drag so panels and dirty state catch up.</summary>
    public void NotifyGridLayoutChanged() { if (!_disposed) Session.Document.NotifySettingsChanged(); }
    /// <summary>Updates the status bar and dependent shell properties while the window is alive.</summary>
    public void ReportStatus(string message)
    {
        if (_disposed) return;
        Status = message;
        RefreshShell();
    }

    /// <summary>Validates an entered byte offset and translates it into its eight physical bit addresses.</summary>
    private void GoToOffset()
    {
        string? input = _dialogs.RequestText("Go to byte offset", "Decimal offset, or hexadecimal with a 0x prefix", "0x0");
        if (input is null) return;
        var value = NumericText.Parse(input);
        if (value < 0 || value >= Session.Document.Data.Length) throw new ArgumentException("The offset must be inside the current file.");
        // For byte offset 2 this produces physical addresses 16 through 23.
        Session.SelectRawBits(Enumerable.Range(0, 8).Select(bit => (long)value * 8 + bit));
        GridFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Removes accepted separators, requires complete byte pairs, and creates a new unsaved document.</summary>
    private void NewFromHex()
    {
        string? input = _dialogs.RequestText("New from hexadecimal", "Paste bytes such as FF F0 A1, or a continuous hex string", "B3 6C 48 65 6C 6C 6F", true);
        if (input is null) return;
        input = Regex.Replace(input, "0x", "", RegexOptions.IgnoreCase);
        // This regular expression removes whitespace and common separators; it
        // does not accept arbitrary characters that could hide invalid hex input.
        input = Regex.Replace(input, @"[\s,;:_-]", "");
        if (input.Length == 0 || input.Length % 2 != 0 || !Regex.IsMatch(input, "\\A[0-9a-fA-F]+\\z"))
            throw new ArgumentException("Enter complete hexadecimal bytes (two digits each).");
        var bytes = Convert.FromHexString(input);
        if (_documents.ReplaceWithNew(new DocumentModel(bytes), "Pasted data", true))
            ReportStatus($"Created {bytes.Length:N0} bytes from hex. Save binary or project to keep them.");
    }

    /// <summary>Formats selected source bits in physical file order for the clipboard.</summary>
    private void CopySelection()
    {
        var bits = Session.Selection.Bits.Order().ToList();
        if (bits.Count == 0) return;
        var document = Session.Document;
        // In bit view, group selected digits by their source byte. In byte view,
        // Distinct ensures a byte is printed once even if several of its bits are selected.
        string text = document.Settings.ViewMode == DataViewMode.Bits
            ? string.Join(" ", bits.GroupBy(b => b / 8).Select(group => string.Concat(group.Select(bit => (document.Data[(int)(bit / 8)] >> (7 - (int)(bit % 8))) & 1))))
            : string.Join(" ", bits.Select(b => b / 8).Distinct().Select(offset => DisplayFormatter.FormatByte(document.Data[(int)offset], document.Settings.ByteBase)));
        _dialogs.SetClipboardText(text);
        ReportStatus("Copied selected data in the current notation.");
    }

    /// <summary>
    /// Creates a short-running command with shared busy/error handling. Action is
    /// a function with no result; optional Func&lt;bool&gt; supplies its availability rule.
    /// </summary>
    private RelayCommand Command(Action action, Func<bool>? canExecute = null)
    {
        var command = new RelayCommand(() => Run(action), () => IsNotBusy && (canExecute?.Invoke() ?? true));
        _commands.Add(command);
        return command;
    }

    /// <summary>Executes an immediate action, displays errors and refreshes command availability afterward.</summary>
    private void Run(Action operation)
    {
        if (!IsNotBusy) return;
        try { operation(); }
        catch (Exception error) { ReportError(error); }
        finally { RefreshShell(); }
    }

    /// <summary>Like Run, but returns success or cancellation for save/close decisions.</summary>
    private bool RunWithResult(Func<bool> operation)
    {
        if (!IsNotBusy) return false;
        try { return operation(); }
        catch (Exception error) { ReportError(error); return false; }
        finally { RefreshShell(); }
    }

    /// <summary>Awaits an asynchronous operation with the same error reporting and final shell refresh.</summary>
    private async Task RunAsync(Func<Task> operation)
    {
        if (!IsNotBusy) return;
        try { await operation(); }
        catch (Exception error) { ReportError(error); }
        finally { RefreshShell(); }
    }

    /// <summary>Reports an operation failure through both the status bar and the interaction service.</summary>
    private void ReportError(Exception error)
    {
        if (_disposed) return;
        ReportStatus(error.Message);
        _dialogs.ShowError(error.Message);
    }
    /// <summary>Refreshes title, save indicators and enabled commands after shared state changes.</summary>
    private void SessionChanged(object? sender, EventArgs e) => RefreshShell();
    /// <summary>Explains the inspection limit without confusing it with whole-file export.</summary>
    private void SelectionLimitReached(object? sender, EventArgs e) => ReportStatus("Select up to 8 KiB at once. Export text always includes the entire file.");
    /// <summary>
    /// Property notifications make WPF reread values; command notifications make it
    /// reevaluate CanExecute. Neither mechanism requires manually updating controls.
    /// </summary>
    private void RefreshShell()
    {
        if (_disposed) return;
        RaiseProperties(nameof(DocumentTitle), nameof(DocumentSubtitle), nameof(WindowTitle), nameof(DirtyLabel), nameof(HasUnsavedChanges), nameof(IsBusy), nameof(IsNotBusy));
        foreach (var command in _commands) command.RaiseCanExecuteChanged();
        OpenCommand.RaiseCanExecuteChanged(); ExportTextCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Ends ownership of this exploration: detach events, stop panel subscriptions,
    /// and guard against file operations completing after the window has closed.
    /// </summary>
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
