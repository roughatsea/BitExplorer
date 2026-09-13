using System.IO;
using BitExplorer.Core;
using WpfApp1.Services;
using WpfApp1.ViewModels;

// These application-shell scenarios use scripted dialogs instead of opening native windows.
// Most checks follow arrange → act → check: create known state, run a command, and inspect its effects.
// Async methods return Task so the runner can wait for completion without guessing how long disk I/O takes.
internal static partial class Program
{
    /// <summary>Opening must respect cancellation, publish busy state, and retain the current document if loading fails.</summary>
    private static async Task OpenCommands()
    {
        // Arrange: make the demo dirty, then write a known three-byte fixture. 41 42 43(hex) are ASCII ABC.
        var dialogs = new FakeUserInteractionService();
        using var viewModel = new MainWindowViewModel(dialogs);
        var original = viewModel.Session.Document;
        original.SetByte(0, 0xA0);
        string path = Artifact("open.bin");
        File.WriteAllBytes(path, [0x41, 0x42, 0x43]);
        // Act/check: Cancel in the unsaved-changes prompt must prevent replacement entirely.
        // ReferenceEquals checks that this is the very same document object, not merely equal bytes.
        dialogs.SaveDecisions.Enqueue(SaveDecision.Cancel);
        await viewModel.OpenPathAsync(path);
        Equal(true, ReferenceEquals(original, viewModel.Session.Document));
        Equal(true, viewModel.HasUnsavedChanges);
        Equal(false, viewModel.IsBusy);

        // Observe notifications while opening; checking only after awaiting would miss the brief busy state.
        // The '|=' remembers whether ANY notification occurred while the operation was busy.
        bool observedBusy = false;
        viewModel.Session.Changed += (_, _) => observedBusy |= viewModel.IsBusy;
        // Discard permits replacement. Check both the new bytes and the transition back to an enabled UI.
        dialogs.SaveDecisions.Enqueue(SaveDecision.Discard);
        await viewModel.OpenPathAsync(path);
        Sequence(new byte[] { 0x41, 0x42, 0x43 }, viewModel.Session.Document.Data);
        Equal(true, observedBusy);
        Equal(false, viewModel.IsBusy);
        Equal(true, viewModel.IsNotBusy);
        var opened = viewModel.Session.Document;
        // A missing file must report an error and release the busy guard, while preserving the
        // successfully opened document. Bypass the save prompt to isolate the loading failure.
        await viewModel.OpenPathAsync(Artifact("missing.bin"), confirm: false);
        Equal(true, ReferenceEquals(opened, viewModel.Session.Document));
        Equal(false, viewModel.IsBusy);
        Equal(1, dialogs.Errors.Count);
    }

    /// <summary>Formatted export must support cancellation, save the complete current format, and recover after write failures.</summary>
    private static async Task ExportCommands()
    {
        // Arrange: five bytes displayed as bits with three bytes per row exercise a full row and a
        // partial final row. 48 69(hex) also provide printable ASCII "Hi" in the text column.
        var dialogs = new FakeUserInteractionService();
        var document = new DocumentModel([0xFF, 0xF0, 0xA1, 0x48, 0x69]);
        document.Settings.BytesPerRow = 3;
        document.Settings.ViewMode = DataViewMode.Bits;
        using var viewModel = new MainWindowViewModel(dialogs, document, "Export fixture");
        // A null save path represents Cancel. Nothing should become busy or produce an error.
        dialogs.SavePaths.Enqueue(null);
        await viewModel.ExportTextCommand.ExecuteAsync();
        Equal(false, viewModel.IsBusy);
        Equal(0, dialogs.Errors.Count);
        // Act: accept a destination. Check exact file contents, including spaces and line endings,
        // against the shared formatter so the workflow cannot accidentally export binary or one viewport.
        string path = Artifact("display.txt");
        dialogs.SavePaths.Enqueue(path);
        await viewModel.ExportTextCommand.ExecuteAsync();
        using var expected = new StringWriter();
        DisplayFormatter.Export(expected, document, document.Settings);
        Equal(expected.ToString(), File.ReadAllText(path));
        Equal(false, viewModel.IsBusy);

        // A nonexistent parent directory causes a real write failure. Afterwards the command must
        // be executable again; a stuck busy flag would leave the UI permanently disabled.
        dialogs.SavePaths.Enqueue(Path.Combine(Artifact("missing-directory"), "display.txt"));
        await viewModel.ExportTextCommand.ExecuteAsync();
        Equal(false, viewModel.IsBusy);
        Equal(true, viewModel.ExportTextCommand.CanExecute(null));
        Equal(1, dialogs.Errors.Count);
    }

    /// <summary>Close confirmation must distinguish Cancel, Discard, cancelled saving, and successful project saving.</summary>
    private static void SaveDecisions()
    {
        // Arrange: changing the first byte to AA(hex) ensures that closing requires a save decision.
        var dialogs = new FakeUserInteractionService();
        using var viewModel = new MainWindowViewModel(dialogs);
        viewModel.Session.Document.SetByte(0, 0xAA);
        // Cancel refuses closure and retains dirty state. Discard permits closure, but this method
        // only answers a question: it does not itself close the window or erase the unsaved changes.
        dialogs.SaveDecisions.Enqueue(SaveDecision.Cancel);
        Equal(false, viewModel.ConfirmClose());
        Equal(true, viewModel.HasUnsavedChanges);
        dialogs.SaveDecisions.Enqueue(SaveDecision.Discard);
        Equal(true, viewModel.ConfirmClose());
        Equal(true, viewModel.HasUnsavedChanges);
        // Choosing Save and then cancelling the destination dialog must still refuse closure.
        dialogs.SaveDecisions.Enqueue(SaveDecision.Save);
        dialogs.SavePaths.Enqueue(null);
        Equal(false, viewModel.ConfirmClose());
        Equal(true, viewModel.HasUnsavedChanges);
        // Save a project successfully. Once the data, field definitions, and display settings are
        // saved, ConfirmClose should return true without asking the same question again.
        string path = Artifact("saved.bexplore");
        dialogs.SavePaths.Enqueue(path);
        Equal(true, viewModel.SaveProject());
        Equal(true, File.Exists(path));
        Equal(false, viewModel.HasUnsavedChanges);
        int count = dialogs.ConfirmSaveCount;
        Equal(true, viewModel.ConfirmClose());
        Equal(count, dialogs.ConfirmSaveCount);
    }

    /// <summary>A pending open must block conflicting commands and duplicate opens, then re-enable them after failure.</summary>
    private static async Task BusyCommandGuards()
    {
        // Arrange: ControlledFiles keeps the open unfinished until this test explicitly completes it.
        // This creates a reliable busy interval without sleeps, slow disks, or other timing assumptions.
        var dialogs = new FakeUserInteractionService();
        var files = new ControlledFiles();
        using var viewModel = new MainWindowViewModel(dialogs, files: files);
        // Act: start but do not await yet. An async method runs synchronously until it reaches an
        // unfinished await, so these checks inspect the application while file loading is pending.
        Task pending = viewModel.OpenPathAsync("pending.bin", confirm: false);
        Equal(true, viewModel.IsBusy);
        Equal(false, viewModel.Inspector.ApplyByteCommand.CanExecute(null));
        Equal(false, viewModel.Fields.AddLabelCommand.CanExecute(null));
        Equal(false, viewModel.Display.SetBitsViewCommand.CanExecute(null));
        Equal(false, viewModel.SaveProjectCommand.CanExecute(null));
        Equal(false, viewModel.ConfirmClose());
        Equal(0, dialogs.ConfirmSaveCount);
        // Another open should be ignored rather than starting a racing replacement. Closing must
        // also be refused while busy, without consuming any scripted save-confirmation answer.
        await viewModel.OpenPathAsync("second.bin", confirm: false);
        Equal(1, files.OpenCount);
        // Complete the fake I/O with a chosen failure and await the original operation. Check that
        // command availability recovers and the error is reported exactly once through the dialog service.
        files.OpenCompletion.SetException(new IOException("Injected file-read failure."));
        await pending;
        Equal(false, viewModel.IsBusy);
        Equal(true, viewModel.Inspector.ApplyByteCommand.CanExecute(null));
        Equal(true, viewModel.SaveProjectCommand.CanExecute(null));
        Equal(1, dialogs.Errors.Count);
        Equal("Injected file-read failure.", dialogs.Errors[0]);
    }

    /// <summary>
    /// A controllable substitute for disk I/O. TaskCompletionSource provides a Task to await while
    /// letting the test choose exactly when it succeeds (SetResult) or fails (SetException).
    /// </summary>
    private sealed class ControlledFiles : IWorkspaceFiles
    {
        /// <summary>
        /// Completion handle for the simulated open. RunContinuationsAsynchronously schedules code
        /// waiting on this task instead of running that code inside the test's SetResult/SetException call.
        /// </summary>
        public TaskCompletionSource<DocumentModel> OpenCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>Number of actual open requests; detects duplicate work while the first request is pending.</summary>
        public int OpenCount { get; private set; }
        /// <summary>Records the request and returns the still-pending task controlled by the test.</summary>
        public Task<DocumentModel> OpenAsync(string path) { OpenCount++; return OpenCompletion.Task; }
        /// <summary>This open-focused fake completes exports immediately; real export behavior is tested separately above.</summary>
        public Task ExportTextAsync(string path, DocumentModel document, DisplaySettings settings) => Task.CompletedTask;
    }

    /// <summary>Late success or failure from an unfinished open must not revive a disposed exploration or notify its old UI.</summary>
    private static async Task DisposedOpenCompletion()
    {
        // Exercise both completion paths. Disposal is an ownership boundary: background file work may
        // finish, but its result must no longer replace documents, attach handlers, or display errors.
        foreach (bool fail in new[] { false, true })
        {
            // Arrange: count shell-property and session-change notifications so late updates are visible.
            var dialogs = new FakeUserInteractionService();
            var files = new ControlledFiles();
            var viewModel = new MainWindowViewModel(dialogs, files: files);
            var original = viewModel.Session.Document;
            int changes = 0;
            viewModel.PropertyChanged += (_, _) => changes++;
            viewModel.Session.Changed += (_, _) => changes++;
            Task pending = viewModel.OpenPathAsync("pending.bin", confirm: false);
            // Act: tear down the view model while loading is still pending, then remember the event count.
            viewModel.Dispose();
            int afterDispose = changes;
            var completed = new DocumentModel([0x41]);
            // Complete only AFTER disposal. A one-byte "A" fixture (41 hex) makes replacement easy to detect.
            if (fail) files.OpenCompletion.SetException(new IOException("Ignored failure after disposal."));
            else files.OpenCompletion.SetResult(completed);
            await pending;
            // Check: no replacement, new notifications, or error dialog should survive teardown.
            Equal(true, ReferenceEquals(original, viewModel.Session.Document));
            Equal(afterDispose, changes);
            Equal(0, dialogs.Errors.Count);
            // Mutate both old and would-be-new documents afterwards. Neither should retain a change
            // subscription capable of notifying the disposed shell/session; this also checks for handler leaks.
            original.SetByte(0, 0);
            completed.SetByte(0, 0);
            Equal(afterDispose, changes);
        }
    }
}
