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

// Make names from System.IO available here without writing their full prefix each time. This does not run
// that library's code.
using System.IO;
// Make names from System.Text.RegularExpressions available here without writing their full prefix each
// time. This does not run that library's code.
using System.Text.RegularExpressions;
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
/// The application's top-level view model. In Model-View-ViewModel (MVVM), the
/// model stores data, XAML describes visible controls, and view models expose
/// values and actions those controls bind to. This shell creates focused panel
/// view models and routes whole-application commands to the document workflow.
/// </summary>
public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    // Reserve _dialogs to hold an object or value of type IUserInteractionService; setup can supply its
    // value, otherwise the type's default is used.
    private readonly IUserInteractionService _dialogs;
    // Reserve _documents to hold an object or value of type DocumentWorkflow; setup can supply its value,
    // otherwise the type's default is used.
    private readonly DocumentWorkflow _documents;
    // Keep commands together so changes to selection, undo history or busy state
    // can ask every button to reevaluate whether its action is currently available.
    private readonly List<RelayCommand> _commands = [];
    // Remember the text written below as _status.
    private string _status = "Ready · Open a file, drop one here, or explore the example packet.";
    // Reserve _disposed to hold a true-or-false answer; setup can supply its value, otherwise the type's
    // default is used.
    private bool _disposed;

    /// <summary>
    /// Builds the session and panels. Optional document/files arguments make this
    /// shell testable without opening dialogs or touching disk; null arguments
    /// choose the normal demo document and real file implementation.
    /// </summary>
    public MainWindowViewModel(IUserInteractionService dialogs, DocumentModel? document = null,
        string? displayName = null, IWorkspaceFiles? files = null)
    {
        // Set _dialogs to dialogs.
        _dialogs = dialogs;
        // The ?? operator uses the right-hand fallback only when the left side is null.
        Session = new ExplorerSession(document ?? DocumentModel.CreateDemo(), displayName ?? "Example packet · demo");
        // Set _documents to a new DocumentWorkflow object using the inputs in parentheses.
        _documents = new DocumentWorkflow(Session, dialogs, files ?? new WorkspaceFiles(), ReportStatus);
        // Set Inspector to a new InspectorViewModel object using the inputs in parentheses.
        Inspector = new InspectorViewModel(Session, dialogs, ReportStatus);
        // Set Fields to a new FieldsViewModel object using the inputs in parentheses.
        Fields = new FieldsViewModel(Session, dialogs, ReportStatus);
        // Set Display to a new DisplaySettingsViewModel object using the inputs in parentheses.
        Display = new DisplaySettingsViewModel(Session, dialogs, ReportStatus);
        // A command combines an action with CanExecute, the rule that enables its button.
        // Async commands await slow file work while keeping the window responsive.
        OpenCommand = new AsyncRelayCommand(() => RunAsync(_documents.OpenAsync), () => IsNotBusy);
        // Prepare ExportTextCommand for later activation: call RunAsync: Awaits an asynchronous operation
        // with the same error reporting and final shell refresh. It can run when IsNotBusy is true.
        // Creating the command here does not perform its action.
        ExportTextCommand = new AsyncRelayCommand(() => RunAsync(_documents.ExportTextAsync), () => IsNotBusy);
        // Prepare SaveBinaryCommand for later activation: call SaveBinary: Writes only raw bytes, then
        // updates the binary baseline if the write succeeds. It can run when the shared command rules
        // permit it. Creating the command here does not perform its action.
        SaveBinaryCommand = Command(() => SaveBinary());
        // Prepare SaveProjectCommand for later activation: call SaveProject: Saves edited bytes, display
        // settings, and field definitions together in versioned JSON. It can run when the shared command
        // rules permit it. Creating the command here does not perform its action.
        SaveProjectCommand = Command(() => SaveProject());
        // Prepare UndoCommand for later activation: restore the state before the most recent undoable edit;
        // then call ReportStatus: Updates the status bar and dependent shell properties while the window is
        // alive. It can run when Session.Document.CanUndo is true. Creating the command here does not
        // perform its action.
        UndoCommand = Command(() => { Session.Document.Undo(); ReportStatus("Undid the last edit."); }, () => Session.Document.CanUndo);
        // Prepare RedoCommand for later activation: reapply the most recently undone edit; then call
        // ReportStatus: Updates the status bar and dependent shell properties while the window is alive. It
        // can run when Session.Document.CanRedo is true. Creating the command here does not perform its
        // action.
        RedoCommand = Command(() => { Session.Document.Redo(); ReportStatus("Redid the last edit."); }, () => Session.Document.CanRedo);
        // Prepare ImportFieldsCommand for later activation: run _documents.ImportFields. It can run when
        // the shared command rules permit it. Creating the command here does not perform its action.
        ImportFieldsCommand = Command(_documents.ImportFields);
        // Prepare ExportFieldsCommand for later activation: run _documents.ExportFields. It can run when
        // the shared command rules permit it. Creating the command here does not perform its action.
        ExportFieldsCommand = Command(_documents.ExportFields);
        // Prepare GoToOffsetCommand for later activation: run GoToOffset. It can run when
        // Session.Document.Data.Length is greater than 0. Creating the command here does not perform its
        // action.
        GoToOffsetCommand = Command(GoToOffset, () => Session.Document.Data.Length > 0);
        // Prepare NewFromHexCommand for later activation: run NewFromHex. It can run when the shared
        // command rules permit it. Creating the command here does not perform its action.
        NewFromHexCommand = Command(NewFromHex);
        // Prepare ExampleCommand for later activation: call _documents.ReplaceWithNew: Installs an
        // in-memory document after resolving any unsaved work in the old one. It can run when the shared
        // command rules permit it. Creating the command here does not perform its action.
        ExampleCommand = Command(() => _documents.ReplaceWithNew(DocumentModel.CreateDemo(), "Example packet · demo", false));
        // Prepare CopyCommand for later activation: run CopySelection. It can run when
        // Session.Selection.Bits.Count is greater than 0. Creating the command here does not perform its
        // action.
        CopyCommand = Command(CopySelection, () => Session.Selection.Bits.Count > 0);
        // Prepare HelpCommand for later activation: call dialogs.ShowHelp: Displays the application's usage
        // instructions. It can run when the shared command rules permit it. Creating the command here does
        // not perform its action.
        HelpCommand = Command(() => dialogs.ShowHelp(ExplorerHelp.Text));
        // Prepare FlipSelectionCommand for later activation: ask the document to invert the bit at the
        // supplied physical file address as an undoable edit. It can run when Session.Selection.Bits.Count
        // equals 1. Creating the command here does not perform its action.
        FlipSelectionCommand = Command(() => Session.Document.FlipBit(Session.Selection.Bits.Single()), () => Session.Selection.Bits.Count == 1);
        // Register SessionChanged as a listener for Session.Changed; the listener runs when that event is
        // raised.
        Session.Changed += SessionChanged;
        // Register SelectionLimitReached as a listener for Session.Selection.SelectionLimitReached; the
        // listener runs when that event is raised.
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
        // If _disposed is true, leave this method immediately.
        if (_disposed) return;
        // Set Status to message.
        Status = message;
        // Call RefreshShell: Property notifications make WPF reread values; command notifications make it
        // reevaluate CanExecute.
        RefreshShell();
    }

    /// <summary>Validates an entered byte offset and translates it into its eight physical bit addresses.</summary>
    private void GoToOffset()
    {
        // Remember the result returned by _dialogs.RequestText(...) as input.
        string? input = _dialogs.RequestText("Go to byte offset", "Decimal offset, or hexadecimal with a 0x prefix", "0x0");
        // If input matches null, leave this method immediately.
        if (input is null) return;
        // Remember the result returned by NumericText.Parse(...) as value.
        var value = NumericText.Parse(input);
        // If value is less than 0, or value is at least Session.Document.Data.Length, stop this normal path
        // by reporting ArgumentException; the arguments carry the error details.
        if (value < 0 || value >= Session.Document.Data.Length) throw new ArgumentException("The offset must be inside the current file.");
        // For byte offset 2 this produces physical addresses 16 through 23.
        Session.SelectRawBits(Enumerable.Range(0, 8).Select(bit => (long)value * 8 + bit));
        // If GridFocusRequested is present, perform .Invoke(this, EventArgs.Empty); ?. skips this call when
        // there is no recipient.
        GridFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Removes accepted separators, requires complete byte pairs, and creates a new unsaved document.</summary>
    private void NewFromHex()
    {
        // Remember the result returned by _dialogs.RequestText(...) as input.
        string? input = _dialogs.RequestText("New from hexadecimal", "Paste bytes such as FF F0 A1, or a continuous hex string", "B3 6C 48 65 6C 6C 6F", true);
        // If input matches null, leave this method immediately.
        if (input is null) return;
        // Set input to text with each occurrence of the first argument replaced by the second.
        input = Regex.Replace(input, "0x", "", RegexOptions.IgnoreCase);
        // This regular expression removes whitespace and common separators; it
        // does not accept arbitrary characters that could hide invalid hex input.
        input = Regex.Replace(input, @"[\s,;:_-]", "");
        // If input.Length equals 0, or input.Length % 2 differs from 0, or it is not the case that
        // Regex.IsMatch(input, "\\A[0-9a-fA-F]+\\z") is true, stop this normal path by reporting
        // ArgumentException; the arguments carry the error details.
        if (input.Length == 0 || input.Length % 2 != 0 || !Regex.IsMatch(input, "\\A[0-9a-fA-F]+\\z"))
            // Stop this normal path by reporting ArgumentException; the arguments carry the error details.
            throw new ArgumentException("Enter complete hexadecimal bytes (two digits each).");
        // Remember the result returned by Convert.FromHexString(...) as bytes.
        var bytes = Convert.FromHexString(input);
        // If _documents.ReplaceWithNew(new DocumentModel(bytes), "Pasted data", true) is true, call
        // ReportStatus: Updates the status bar and dependent shell properties while the window is alive.
        if (_documents.ReplaceWithNew(new DocumentModel(bytes), "Pasted data", true))
            // Call ReportStatus: Updates the status bar and dependent shell properties while the window is
            // alive.
            ReportStatus($"Created {bytes.Length:N0} bytes from hex. Save binary or project to keep them.");
    }

    /// <summary>Formats selected source bits in physical file order for the clipboard.</summary>
    private void CopySelection()
    {
        // Remember a separate list containing Session.Selection.Bits.Order()'s items as bits.
        var bits = Session.Selection.Bits.Order().ToList();
        // If bits.Count equals 0, leave this method immediately.
        if (bits.Count == 0) return;
        // Remember Session.Document as document.
        var document = Session.Document;
        // In bit view, group selected digits by their source byte. In byte view,
        // Distinct ensures a byte is printed once even if several of its bits are selected.
        string text = document.Settings.ViewMode == DataViewMode.Bits
            ? string.Join(" ", bits.GroupBy(b => b / 8).Select(group => string.Concat(group.Select(bit => (document.Data[(int)(bit / 8)] >> (7 - (int)(bit % 8))) & 1))))
            : string.Join(" ", bits.Select(b => b / 8).Distinct().Select(offset => DisplayFormatter.FormatByte(document.Data[(int)offset], document.Settings.ByteBase)));
        // Call _dialogs.SetClipboardText: Copies prepared plain text; the caller chooses its data and
        // formatting.
        _dialogs.SetClipboardText(text);
        // Call ReportStatus: Updates the status bar and dependent shell properties while the window is
        // alive.
        ReportStatus("Copied selected data in the current notation.");
    }

    /// <summary>
    /// Creates a short-running command with shared busy/error handling. Action is
    /// a function with no result; optional Func&lt;bool&gt; supplies its availability rule.
    /// </summary>
    private RelayCommand Command(Action action, Func<bool>? canExecute = null)
    {
        // Remember a new RelayCommand object using the inputs in parentheses as command.
        var command = new RelayCommand(() => Run(action), () => IsNotBusy && (canExecute?.Invoke() ?? true));
        // Add command to _commands.
        _commands.Add(command);
        // Return command to the caller and leave this method.
        return command;
    }

    /// <summary>Executes an immediate action, displays errors and refreshes command availability afterward.</summary>
    private void Run(Action operation)
    {
        // If it is not the case that IsNotBusy is true, leave this method immediately.
        if (!IsNotBusy) return;
        // Attempt this work. A matching catch below handles a reported exception; a finally block, when
        // present, performs cleanup on the way out.
        try { operation(); }
        // If the preceding try reports Exception, refer to it as error; run this recovery path.
        catch (Exception error) { ReportError(error); }
        // Run this cleanup when leaving the try, whether the work succeeded or reported an exception.
        finally { RefreshShell(); }
    }

    /// <summary>Like Run, but returns success or cancellation for save/close decisions.</summary>
    private bool RunWithResult(Func<bool> operation)
    {
        // If it is not the case that IsNotBusy is true, return false (no) to the caller and leave this
        // method.
        if (!IsNotBusy) return false;
        // Attempt this work. A matching catch below handles a reported exception; a finally block, when
        // present, performs cleanup on the way out.
        try { return operation(); }
        // If the preceding try reports Exception, refer to it as error; run this recovery path.
        catch (Exception error) { ReportError(error); return false; }
        // Run this cleanup when leaving the try, whether the work succeeded or reported an exception.
        finally { RefreshShell(); }
    }

    /// <summary>Awaits an asynchronous operation with the same error reporting and final shell refresh.</summary>
    private async Task RunAsync(Func<Task> operation)
    {
        // If it is not the case that IsNotBusy is true, leave this method immediately.
        if (!IsNotBusy) return;
        // Attempt this work. A matching catch below handles a reported exception; a finally block, when
        // present, performs cleanup on the way out.
        try { await operation(); }
        // If the preceding try reports Exception, refer to it as error; run this recovery path.
        catch (Exception error) { ReportError(error); }
        // Run this cleanup when leaving the try, whether the work succeeded or reported an exception.
        finally { RefreshShell(); }
    }

    /// <summary>Reports an operation failure through both the status bar and the interaction service.</summary>
    private void ReportError(Exception error)
    {
        // If _disposed is true, leave this method immediately.
        if (_disposed) return;
        // Call ReportStatus: Updates the status bar and dependent shell properties while the window is
        // alive.
        ReportStatus(error.Message);
        // Show the supplied error message through the interaction service.
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
        // If _disposed is true, leave this method immediately.
        if (_disposed) return;
        // Tell WPF to reread each named property below, because their displayed values may have changed.
        RaiseProperties(nameof(DocumentTitle), nameof(DocumentSubtitle), nameof(WindowTitle), nameof(DirtyLabel), nameof(HasUnsavedChanges), nameof(IsBusy), nameof(IsNotBusy));
        // Take each item from _commands in turn, call the current item command, and ask controls using
        // command to check again whether the command is allowed to run.
        foreach (var command in _commands) command.RaiseCanExecuteChanged();
        // Ask controls using OpenCommand to check again whether the command is allowed to run.
        OpenCommand.RaiseCanExecuteChanged(); ExportTextCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Ends ownership of this exploration: detach events, stop panel subscriptions,
    /// and guard against file operations completing after the window has closed.
    /// </summary>
    public void Dispose()
    {
        // If _disposed is true, leave this method immediately.
        if (_disposed) return;
        // Set _disposed to true (yes).
        _disposed = true;
        // Release _documents's resources or event subscriptions now that it is no longer needed.
        _documents.Dispose();
        // Stop sending Session.Changed notifications to SessionChanged.
        Session.Changed -= SessionChanged;
        // Stop sending Session.Selection.SelectionLimitReached notifications to SelectionLimitReached.
        Session.Selection.SelectionLimitReached -= SelectionLimitReached;
        // Release Inspector's resources or event subscriptions now that it is no longer needed.
        Inspector.Dispose(); Fields.Dispose(); Display.Dispose(); Session.Dispose();
    }
}
