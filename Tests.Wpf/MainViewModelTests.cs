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
// Make names from BitExplorer.Core available here without writing their full prefix each time. This does
// not run that library's code.
using BitExplorer.Core;
// Make names from WpfApp1.Services available here without writing their full prefix each time. This does
// not run that library's code.
using WpfApp1.Services;
// Make names from WpfApp1.ViewModels available here without writing their full prefix each time. This does
// not run that library's code.
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
        // Remember a new MainWindowViewModel object using the inputs in parentheses as viewModel. The using
        // declaration releases it automatically when this scope ends, even after an error.
        using var viewModel = new MainWindowViewModel(dialogs);
        // Remember viewModel.Session.Document as original.
        var original = viewModel.Session.Document;
        // Ask the document to write the supplied value at the supplied byte offset, recording an undoable
        // edit if it changes.
        original.SetByte(0, 0xA0);
        // Remember the result returned by Artifact(...) as path.
        string path = Artifact("open.bin");
        // Call File.WriteAllBytes(...); the values in parentheses are the inputs.
        File.WriteAllBytes(path, [0x41, 0x42, 0x43]);
        // Act/check: Cancel in the unsaved-changes prompt must prevent replacement entirely.
        // ReferenceEquals checks that this is the very same document object, not merely equal bytes.
        dialogs.SaveDecisions.Enqueue(SaveDecision.Cancel);
        // Wait for viewModel.OpenPathAsync to finish before continuing; await lets the thread service other
        // work while the operation is pending.
        await viewModel.OpenPathAsync(path);
        // Check that ReferenceEquals(original, viewModel.Session.Document) equals the expected true; a
        // mismatch fails this test.
        Equal(true, ReferenceEquals(original, viewModel.Session.Document));
        // Check that viewModel.HasUnsavedChanges equals the expected true; a mismatch fails this test.
        Equal(true, viewModel.HasUnsavedChanges);
        // Check that viewModel.IsBusy equals the expected false; a mismatch fails this test.
        Equal(false, viewModel.IsBusy);

        // Observe notifications while opening; checking only after awaiting would miss the brief busy state.
        // The '|=' remembers whether ANY notification occurred while the operation was busy.
        bool observedBusy = false;
        // Register the operation after => as a listener for viewModel.Session.Changed; the listener runs
        // when that event is raised.
        viewModel.Session.Changed += (_, _) => observedBusy |= viewModel.IsBusy;
        // Discard permits replacement. Check both the new bytes and the transition back to an enabled UI.
        dialogs.SaveDecisions.Enqueue(SaveDecision.Discard);
        // Wait for viewModel.OpenPathAsync to finish before continuing; await lets the thread service other
        // work while the operation is pending.
        await viewModel.OpenPathAsync(path);
        // Check that viewModel.Session.Document.Data has the same items in the same order as new byte[] {
        // 0x41, 0x42, 0x43 }.
        Sequence(new byte[] { 0x41, 0x42, 0x43 }, viewModel.Session.Document.Data);
        // Check that observedBusy equals the expected true; a mismatch fails this test.
        Equal(true, observedBusy);
        // Check that viewModel.IsBusy equals the expected false; a mismatch fails this test.
        Equal(false, viewModel.IsBusy);
        // Check that viewModel.IsNotBusy equals the expected true; a mismatch fails this test.
        Equal(true, viewModel.IsNotBusy);
        // Remember viewModel.Session.Document as opened.
        var opened = viewModel.Session.Document;
        // A missing file must report an error and release the busy guard, while preserving the
        // successfully opened document. Bypass the save prompt to isolate the loading failure.
        await viewModel.OpenPathAsync(Artifact("missing.bin"), confirm: false);
        // Check that ReferenceEquals(opened, viewModel.Session.Document) equals the expected true; a
        // mismatch fails this test.
        Equal(true, ReferenceEquals(opened, viewModel.Session.Document));
        // Check that viewModel.IsBusy equals the expected false; a mismatch fails this test.
        Equal(false, viewModel.IsBusy);
        // Check that dialogs.Errors.Count equals the expected 1; a mismatch fails this test.
        Equal(1, dialogs.Errors.Count);
    }

    /// <summary>Formatted export must support cancellation, save the complete current format, and recover after write failures.</summary>
    private static async Task ExportCommands()
    {
        // Arrange: five bytes displayed as bits with three bytes per row exercise a full row and a
        // partial final row. 48 69(hex) also provide printable ASCII "Hi" in the text column.
        var dialogs = new FakeUserInteractionService();
        // Remember a new DocumentModel object using the inputs in parentheses as document.
        var document = new DocumentModel([0xFF, 0xF0, 0xA1, 0x48, 0x69]);
        // Set document.Settings.BytesPerRow to 3.
        document.Settings.BytesPerRow = 3;
        // Set document.Settings.ViewMode to DataViewMode.Bits.
        document.Settings.ViewMode = DataViewMode.Bits;
        // Remember a new MainWindowViewModel object using the inputs in parentheses as viewModel. The using
        // declaration releases it automatically when this scope ends, even after an error.
        using var viewModel = new MainWindowViewModel(dialogs, document, "Export fixture");
        // A null save path represents Cancel. Nothing should become busy or produce an error.
        dialogs.SavePaths.Enqueue(null);
        // Wait for viewModel.ExportTextCommand.ExecuteAsync to finish before continuing; await lets the
        // thread service other work while the operation is pending.
        await viewModel.ExportTextCommand.ExecuteAsync();
        // Check that viewModel.IsBusy equals the expected false; a mismatch fails this test.
        Equal(false, viewModel.IsBusy);
        // Check that dialogs.Errors.Count equals the expected 0; a mismatch fails this test.
        Equal(0, dialogs.Errors.Count);
        // Act: accept a destination. Check exact file contents, including spaces and line endings,
        // against the shared formatter so the workflow cannot accidentally export binary or one viewport.
        string path = Artifact("display.txt");
        // Call dialogs.SavePaths.Enqueue(...); the values in parentheses are the inputs.
        dialogs.SavePaths.Enqueue(path);
        // Wait for viewModel.ExportTextCommand.ExecuteAsync to finish before continuing; await lets the
        // thread service other work while the operation is pending.
        await viewModel.ExportTextCommand.ExecuteAsync();
        // Remember a new StringWriter object as expected. The using declaration releases it automatically
        // when this scope ends, even after an error.
        using var expected = new StringWriter();
        // Call DisplayFormatter.Export: Writes the entire document using the current row grouping,
        // notation, columns, ruler, and annotations.
        DisplayFormatter.Export(expected, document, document.Settings);
        // Check that File.ReadAllText(path) equals the expected expected.ToString(); a mismatch fails this
        // test.
        Equal(expected.ToString(), File.ReadAllText(path));
        // Check that viewModel.IsBusy equals the expected false; a mismatch fails this test.
        Equal(false, viewModel.IsBusy);

        // A nonexistent parent directory causes a real write failure. Afterwards the command must
        // be executable again; a stuck busy flag would leave the UI permanently disabled.
        dialogs.SavePaths.Enqueue(Path.Combine(Artifact("missing-directory"), "display.txt"));
        // Wait for viewModel.ExportTextCommand.ExecuteAsync to finish before continuing; await lets the
        // thread service other work while the operation is pending.
        await viewModel.ExportTextCommand.ExecuteAsync();
        // Check that viewModel.IsBusy equals the expected false; a mismatch fails this test.
        Equal(false, viewModel.IsBusy);
        // Check that viewModel.ExportTextCommand.CanExecute(null) equals the expected true; a mismatch
        // fails this test.
        Equal(true, viewModel.ExportTextCommand.CanExecute(null));
        // Check that dialogs.Errors.Count equals the expected 1; a mismatch fails this test.
        Equal(1, dialogs.Errors.Count);
    }

    /// <summary>Close confirmation must distinguish Cancel, Discard, cancelled saving, and successful project saving.</summary>
    private static void SaveDecisions()
    {
        // Arrange: changing the first byte to AA(hex) ensures that closing requires a save decision.
        var dialogs = new FakeUserInteractionService();
        // Remember a new MainWindowViewModel object using the inputs in parentheses as viewModel. The using
        // declaration releases it automatically when this scope ends, even after an error.
        using var viewModel = new MainWindowViewModel(dialogs);
        // Ask the document to write the supplied value at the supplied byte offset, recording an undoable
        // edit if it changes.
        viewModel.Session.Document.SetByte(0, 0xAA);
        // Cancel refuses closure and retains dirty state. Discard permits closure, but this method
        // only answers a question: it does not itself close the window or erase the unsaved changes.
        dialogs.SaveDecisions.Enqueue(SaveDecision.Cancel);
        // Check that viewModel.ConfirmClose() equals the expected false; a mismatch fails this test.
        Equal(false, viewModel.ConfirmClose());
        // Check that viewModel.HasUnsavedChanges equals the expected true; a mismatch fails this test.
        Equal(true, viewModel.HasUnsavedChanges);
        // Call dialogs.SaveDecisions.Enqueue(...); the values in parentheses are the inputs.
        dialogs.SaveDecisions.Enqueue(SaveDecision.Discard);
        // Check that viewModel.ConfirmClose() equals the expected true; a mismatch fails this test.
        Equal(true, viewModel.ConfirmClose());
        // Check that viewModel.HasUnsavedChanges equals the expected true; a mismatch fails this test.
        Equal(true, viewModel.HasUnsavedChanges);
        // Choosing Save and then cancelling the destination dialog must still refuse closure.
        dialogs.SaveDecisions.Enqueue(SaveDecision.Save);
        // Call dialogs.SavePaths.Enqueue(...); the values in parentheses are the inputs.
        dialogs.SavePaths.Enqueue(null);
        // Check that viewModel.ConfirmClose() equals the expected false; a mismatch fails this test.
        Equal(false, viewModel.ConfirmClose());
        // Check that viewModel.HasUnsavedChanges equals the expected true; a mismatch fails this test.
        Equal(true, viewModel.HasUnsavedChanges);
        // Save a project successfully. Once the data, field definitions, and display settings are
        // saved, ConfirmClose should return true without asking the same question again.
        string path = Artifact("saved.bexplore");
        // Call dialogs.SavePaths.Enqueue(...); the values in parentheses are the inputs.
        dialogs.SavePaths.Enqueue(path);
        // Check that viewModel.SaveProject() equals the expected true; a mismatch fails this test.
        Equal(true, viewModel.SaveProject());
        // Check that File.Exists(path) equals the expected true; a mismatch fails this test.
        Equal(true, File.Exists(path));
        // Check that viewModel.HasUnsavedChanges equals the expected false; a mismatch fails this test.
        Equal(false, viewModel.HasUnsavedChanges);
        // Remember dialogs.ConfirmSaveCount as count.
        int count = dialogs.ConfirmSaveCount;
        // Check that viewModel.ConfirmClose() equals the expected true; a mismatch fails this test.
        Equal(true, viewModel.ConfirmClose());
        // Check that dialogs.ConfirmSaveCount equals the expected count; a mismatch fails this test.
        Equal(count, dialogs.ConfirmSaveCount);
    }

    /// <summary>A pending open must block conflicting commands and duplicate opens, then re-enable them after failure.</summary>
    private static async Task BusyCommandGuards()
    {
        // Arrange: ControlledFiles keeps the open unfinished until this test explicitly completes it.
        // This creates a reliable busy interval without sleeps, slow disks, or other timing assumptions.
        var dialogs = new FakeUserInteractionService();
        // Remember a new ControlledFiles object as files.
        var files = new ControlledFiles();
        // Remember a new MainWindowViewModel object using the inputs in parentheses as viewModel. The using
        // declaration releases it automatically when this scope ends, even after an error.
        using var viewModel = new MainWindowViewModel(dialogs, files: files);
        // Act: start but do not await yet. An async method runs synchronously until it reaches an
        // unfinished await, so these checks inspect the application while file loading is pending.
        Task pending = viewModel.OpenPathAsync("pending.bin", confirm: false);
        // Check that viewModel.IsBusy equals the expected true; a mismatch fails this test.
        Equal(true, viewModel.IsBusy);
        // Check that viewModel.Inspector.ApplyByteCommand.CanExecute(null) equals the expected false; a
        // mismatch fails this test.
        Equal(false, viewModel.Inspector.ApplyByteCommand.CanExecute(null));
        // Check that viewModel.Fields.AddLabelCommand.CanExecute(null) equals the expected false; a
        // mismatch fails this test.
        Equal(false, viewModel.Fields.AddLabelCommand.CanExecute(null));
        // Check that viewModel.Display.SetBitsViewCommand.CanExecute(null) equals the expected false; a
        // mismatch fails this test.
        Equal(false, viewModel.Display.SetBitsViewCommand.CanExecute(null));
        // Check that viewModel.SaveProjectCommand.CanExecute(null) equals the expected false; a mismatch
        // fails this test.
        Equal(false, viewModel.SaveProjectCommand.CanExecute(null));
        // Check that viewModel.ConfirmClose() equals the expected false; a mismatch fails this test.
        Equal(false, viewModel.ConfirmClose());
        // Check that dialogs.ConfirmSaveCount equals the expected 0; a mismatch fails this test.
        Equal(0, dialogs.ConfirmSaveCount);
        // Another open should be ignored rather than starting a racing replacement. Closing must
        // also be refused while busy, without consuming any scripted save-confirmation answer.
        await viewModel.OpenPathAsync("second.bin", confirm: false);
        // Check that files.OpenCount equals the expected 1; a mismatch fails this test.
        Equal(1, files.OpenCount);
        // Complete the fake I/O with a chosen failure and await the original operation. Check that
        // command availability recovers and the error is reported exactly once through the dialog service.
        files.OpenCompletion.SetException(new IOException("Injected file-read failure."));
        // Wait for pending to finish before continuing; await lets the thread service other work while the
        // operation is pending.
        await pending;
        // Check that viewModel.IsBusy equals the expected false; a mismatch fails this test.
        Equal(false, viewModel.IsBusy);
        // Check that viewModel.Inspector.ApplyByteCommand.CanExecute(null) equals the expected true; a
        // mismatch fails this test.
        Equal(true, viewModel.Inspector.ApplyByteCommand.CanExecute(null));
        // Check that viewModel.SaveProjectCommand.CanExecute(null) equals the expected true; a mismatch
        // fails this test.
        Equal(true, viewModel.SaveProjectCommand.CanExecute(null));
        // Check that dialogs.Errors.Count equals the expected 1; a mismatch fails this test.
        Equal(1, dialogs.Errors.Count);
        // Check that dialogs.Errors[0] equals the expected "Injected file-read failure."; a mismatch fails
        // this test.
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
            // Remember a new ControlledFiles object as files.
            var files = new ControlledFiles();
            // Remember a new MainWindowViewModel object using the inputs in parentheses as viewModel.
            var viewModel = new MainWindowViewModel(dialogs, files: files);
            // Remember viewModel.Session.Document as original.
            var original = viewModel.Session.Document;
            // Remember 0 as changes.
            int changes = 0;
            // Register the operation after => as a listener for viewModel.PropertyChanged; the listener
            // runs when that event is raised.
            viewModel.PropertyChanged += (_, _) => changes++;
            // Register the operation after => as a listener for viewModel.Session.Changed; the listener
            // runs when that event is raised.
            viewModel.Session.Changed += (_, _) => changes++;
            // Remember the result returned by viewModel.OpenPathAsync(...) as pending.
            Task pending = viewModel.OpenPathAsync("pending.bin", confirm: false);
            // Act: tear down the view model while loading is still pending, then remember the event count.
            viewModel.Dispose();
            // Remember changes as afterDispose.
            int afterDispose = changes;
            // Remember a new DocumentModel object using the inputs in parentheses as completed.
            var completed = new DocumentModel([0x41]);
            // Complete only AFTER disposal. A one-byte "A" fixture (41 hex) makes replacement easy to detect.
            if (fail) files.OpenCompletion.SetException(new IOException("Ignored failure after disposal."));
            // If the preceding condition was false, run this alternative path.
            else files.OpenCompletion.SetResult(completed);
            // Wait for pending to finish before continuing; await lets the thread service other work while
            // the operation is pending.
            await pending;
            // Check: no replacement, new notifications, or error dialog should survive teardown.
            Equal(true, ReferenceEquals(original, viewModel.Session.Document));
            // Check that changes equals the expected afterDispose; a mismatch fails this test.
            Equal(afterDispose, changes);
            // Check that dialogs.Errors.Count equals the expected 0; a mismatch fails this test.
            Equal(0, dialogs.Errors.Count);
            // Mutate both old and would-be-new documents afterwards. Neither should retain a change
            // subscription capable of notifying the disposed shell/session; this also checks for handler leaks.
            original.SetByte(0, 0);
            // Ask the document to write the supplied value at the supplied byte offset, recording an
            // undoable edit if it changes.
            completed.SetByte(0, 0);
            // Check that changes equals the expected afterDispose; a mismatch fails this test.
            Equal(afterDispose, changes);
        }
    }
}
