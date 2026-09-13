using System.IO;
using BitExplorer.Core;
using WpfApp1.Services;
using WpfApp1.ViewModels;

internal static partial class Program
{
    private static async Task OpenCommands()
    {
        var dialogs = new FakeUserInteractionService();
        using var viewModel = new MainWindowViewModel(dialogs);
        var original = viewModel.Session.Document;
        original.SetByte(0, 0xA0);
        string path = Artifact("open.bin");
        File.WriteAllBytes(path, [0x41, 0x42, 0x43]);
        dialogs.SaveDecisions.Enqueue(SaveDecision.Cancel);
        await viewModel.OpenPathAsync(path);
        Equal(true, ReferenceEquals(original, viewModel.Session.Document));
        Equal(true, viewModel.HasUnsavedChanges);
        Equal(false, viewModel.IsBusy);

        bool observedBusy = false;
        viewModel.Session.Changed += (_, _) => observedBusy |= viewModel.IsBusy;
        dialogs.SaveDecisions.Enqueue(SaveDecision.Discard);
        await viewModel.OpenPathAsync(path);
        Sequence(new byte[] { 0x41, 0x42, 0x43 }, viewModel.Session.Document.Data);
        Equal(true, observedBusy);
        Equal(false, viewModel.IsBusy);
        Equal(true, viewModel.IsNotBusy);
        var opened = viewModel.Session.Document;
        await viewModel.OpenPathAsync(Artifact("missing.bin"), confirm: false);
        Equal(true, ReferenceEquals(opened, viewModel.Session.Document));
        Equal(false, viewModel.IsBusy);
        Equal(1, dialogs.Errors.Count);
    }

    private static async Task ExportCommands()
    {
        var dialogs = new FakeUserInteractionService();
        var document = new DocumentModel([0xFF, 0xF0, 0xA1, 0x48, 0x69]);
        document.Settings.BytesPerRow = 3;
        document.Settings.ViewMode = DataViewMode.Bits;
        using var viewModel = new MainWindowViewModel(dialogs, document, "Export fixture");
        dialogs.SavePaths.Enqueue(null);
        await viewModel.ExportTextCommand.ExecuteAsync();
        Equal(false, viewModel.IsBusy);
        Equal(0, dialogs.Errors.Count);
        string path = Artifact("display.txt");
        dialogs.SavePaths.Enqueue(path);
        await viewModel.ExportTextCommand.ExecuteAsync();
        using var expected = new StringWriter();
        DisplayFormatter.Export(expected, document, document.Settings);
        Equal(expected.ToString(), File.ReadAllText(path));
        Equal(false, viewModel.IsBusy);

        dialogs.SavePaths.Enqueue(Path.Combine(Artifact("missing-directory"), "display.txt"));
        await viewModel.ExportTextCommand.ExecuteAsync();
        Equal(false, viewModel.IsBusy);
        Equal(true, viewModel.ExportTextCommand.CanExecute(null));
        Equal(1, dialogs.Errors.Count);
    }

    private static void SaveDecisions()
    {
        var dialogs = new FakeUserInteractionService();
        using var viewModel = new MainWindowViewModel(dialogs);
        viewModel.Session.Document.SetByte(0, 0xAA);
        dialogs.SaveDecisions.Enqueue(SaveDecision.Cancel);
        Equal(false, viewModel.ConfirmClose());
        Equal(true, viewModel.HasUnsavedChanges);
        dialogs.SaveDecisions.Enqueue(SaveDecision.Discard);
        Equal(true, viewModel.ConfirmClose());
        Equal(true, viewModel.HasUnsavedChanges);
        dialogs.SaveDecisions.Enqueue(SaveDecision.Save);
        dialogs.SavePaths.Enqueue(null);
        Equal(false, viewModel.ConfirmClose());
        Equal(true, viewModel.HasUnsavedChanges);
        string path = Artifact("saved.bexplore");
        dialogs.SavePaths.Enqueue(path);
        Equal(true, viewModel.SaveProject());
        Equal(true, File.Exists(path));
        Equal(false, viewModel.HasUnsavedChanges);
        int count = dialogs.ConfirmSaveCount;
        Equal(true, viewModel.ConfirmClose());
        Equal(count, dialogs.ConfirmSaveCount);
    }

    private static async Task BusyCommandGuards()
    {
        var dialogs = new FakeUserInteractionService();
        var files = new ControlledFiles();
        using var viewModel = new MainWindowViewModel(dialogs, files: files);
        Task pending = viewModel.OpenPathAsync("pending.bin", confirm: false);
        Equal(true, viewModel.IsBusy);
        Equal(false, viewModel.Inspector.ApplyByteCommand.CanExecute(null));
        Equal(false, viewModel.Fields.AddLabelCommand.CanExecute(null));
        Equal(false, viewModel.Display.SetBitsViewCommand.CanExecute(null));
        Equal(false, viewModel.SaveProjectCommand.CanExecute(null));
        Equal(false, viewModel.ConfirmClose());
        Equal(0, dialogs.ConfirmSaveCount);
        await viewModel.OpenPathAsync("second.bin", confirm: false);
        Equal(1, files.OpenCount);
        files.OpenCompletion.SetException(new IOException("Injected file-read failure."));
        await pending;
        Equal(false, viewModel.IsBusy);
        Equal(true, viewModel.Inspector.ApplyByteCommand.CanExecute(null));
        Equal(true, viewModel.SaveProjectCommand.CanExecute(null));
        Equal(1, dialogs.Errors.Count);
        Equal("Injected file-read failure.", dialogs.Errors[0]);
    }

    private sealed class ControlledFiles : IWorkspaceFiles
    {
        public TaskCompletionSource<DocumentModel> OpenCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int OpenCount { get; private set; }
        public Task<DocumentModel> OpenAsync(string path) { OpenCount++; return OpenCompletion.Task; }
        public Task ExportTextAsync(string path, DocumentModel document, DisplaySettings settings) => Task.CompletedTask;
    }

    private static async Task DisposedOpenCompletion()
    {
        foreach (bool fail in new[] { false, true })
        {
            var dialogs = new FakeUserInteractionService();
            var files = new ControlledFiles();
            var viewModel = new MainWindowViewModel(dialogs, files: files);
            var original = viewModel.Session.Document;
            int changes = 0;
            viewModel.PropertyChanged += (_, _) => changes++;
            viewModel.Session.Changed += (_, _) => changes++;
            Task pending = viewModel.OpenPathAsync("pending.bin", confirm: false);
            viewModel.Dispose();
            int afterDispose = changes;
            var completed = new DocumentModel([0x41]);
            if (fail) files.OpenCompletion.SetException(new IOException("Ignored failure after disposal."));
            else files.OpenCompletion.SetResult(completed);
            await pending;
            Equal(true, ReferenceEquals(original, viewModel.Session.Document));
            Equal(afterDispose, changes);
            Equal(0, dialogs.Errors.Count);
            original.SetByte(0, 0);
            completed.SetByte(0, 0);
            Equal(afterDispose, changes);
        }
    }
}
