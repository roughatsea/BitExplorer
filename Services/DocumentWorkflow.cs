using System.IO;
using BitExplorer.Core;
using WpfApp1.ViewModels;

namespace WpfApp1.Services;

/// <summary>Coordinates document replacement, save decisions, persistence and operation lifetime.</summary>
public sealed class DocumentWorkflow(
    ExplorerSession session, IUserInteractionService dialogs, IWorkspaceFiles files, Action<string> reportStatus) : IDisposable
{
    private bool _unsavedNewDocument;
    private bool _disposed;

    public bool HasUnsavedChanges => session.Document.IsDirty || _unsavedNewDocument;

    public bool ConfirmClose()
    {
        if (_disposed || session.IsBusy) return false;
        if (!HasUnsavedChanges) return true;
        return dialogs.ConfirmSaveChanges() switch
        {
            SaveDecision.Discard => true,
            SaveDecision.Save => SaveProject(),
            _ => false
        };
    }

    public async Task OpenAsync()
    {
        if (_disposed || session.IsBusy) return;
        string? path = dialogs.ChooseOpenFile("Open binary file or project", "All files|*.*|Bit Explorer project|*.bitexplorer");
        if (path is not null) await OpenPathAsync(path);
    }

    public async Task OpenPathAsync(string path, bool confirm = true)
    {
        if (_disposed || session.IsBusy || (confirm && !ConfirmClose())) return;
        await RunBusyAsync("Opening file…", async () =>
        {
            var document = await files.OpenAsync(path);
            // The window may have been disposed while the file was being read.
            if (_disposed) return;
            _unsavedNewDocument = false;
            session.ReplaceDocument(document, Path.GetFileName(path));
            reportStatus($"Opened {Path.GetFileName(path)} · {document.Data.Length:N0} bytes.");
        });
    }

    public bool ReplaceWithNew(DocumentModel document, string name, bool needsSave)
    {
        if (!ConfirmClose()) return false;
        _unsavedNewDocument = needsSave;
        session.ReplaceDocument(document, name);
        return true;
    }

    public bool SaveBinary()
    {
        if (_disposed || session.IsBusy) return false;
        var document = session.Document;
        string? path = dialogs.ChooseSaveFile("Save edited binary", "Binary file|*.bin|All files|*.*",
            Path.GetFileName(document.FilePath ?? "data.bin"), document.FilePath);
        if (path is null) return false;
        return Run(() =>
        {
            document.SaveBinary(path);
            _unsavedNewDocument = false;
            session.DisplayName = Path.GetFileName(path);
            reportStatus("Binary saved. Use Save project to preserve field definitions and display settings.");
        });
    }

    public bool SaveProject()
    {
        if (_disposed || session.IsBusy) return false;
        var document = session.Document;
        string suggested = document.ProjectPath is null
            ? Path.GetFileNameWithoutExtension(document.FilePath ?? "exploration") + ".bitexplorer"
            : Path.GetFileName(document.ProjectPath);
        string? path = dialogs.ChooseSaveFile("Save binary, fields and view as a project", "Bit Explorer project|*.bitexplorer", suggested, document.ProjectPath);
        if (path is null) return false;
        return Run(() =>
        {
            document.SaveProject(path);
            _unsavedNewDocument = false;
            session.DisplayName = Path.GetFileName(path);
            reportStatus("Project saved with edited data, field mappings and display settings.");
        });
    }

    public async Task ExportTextAsync()
    {
        if (_disposed || session.IsBusy) return;
        var document = session.Document;
        string? path = dialogs.ChooseSaveFile("Export the entire file in the current display format", "Formatted text|*.txt",
            Path.GetFileNameWithoutExtension(document.FilePath ?? "exploration") + ".txt");
        if (path is null) return;
        await RunBusyAsync("Exporting entire file…", async () =>
        {
            await files.ExportTextAsync(path, document, document.Settings.Clone());
            if (!_disposed) reportStatus($"Exported all {document.Data.Length:N0} bytes as formatted text.");
        });
    }

    public void ImportFields()
    {
        if (_disposed || session.IsBusy) return;
        string? path = dialogs.ChooseOpenFile("Import field layout (replaces current labels; undoable)", "Field layout|*.bitfields;*.json|All files|*.*");
        if (path is not null) Run(() =>
        {
            session.Document.ImportTemplate(path);
            reportStatus("Imported field layout. Ctrl+Z restores the previous layout.");
        });
    }

    public void ExportFields()
    {
        if (_disposed || session.IsBusy) return;
        string? path = dialogs.ChooseSaveFile("Export reusable field layout", "Bit Explorer field layout|*.bitfields", "field-layout.bitfields");
        if (path is not null) Run(() => { session.Document.ExportTemplate(path); reportStatus("Exported reusable field definitions."); });
    }

    private bool Run(Action operation)
    {
        if (_disposed || session.IsBusy) return false;
        try { operation(); return true; }
        catch (Exception error) { ReportError(error); return false; }
    }

    private async Task RunBusyAsync(string message, Func<Task> operation)
    {
        if (_disposed || session.IsBusy) return;
        session.IsBusy = true;
        reportStatus(message);
        try { await operation(); }
        catch (Exception error) { ReportError(error); }
        finally { if (!_disposed) session.IsBusy = false; }
    }

    private void ReportError(Exception error)
    {
        if (_disposed) return;
        reportStatus(error.Message);
        dialogs.ShowError(error.Message);
    }

    // File I/O can finish, but its completion must not mutate or notify a closed exploration.
    public void Dispose() => _disposed = true;
}
