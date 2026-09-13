using System.IO;
using BitExplorer.Core;
using WpfApp1.ViewModels;

namespace WpfApp1.Services;

/// <summary>
/// Coordinates opening, replacing, saving and exporting documents. It owns the
/// decisions around these operations, while DocumentModel owns the data and the
/// file/interaction interfaces handle external work. Constructor parameters are
/// supplied by the shell; tests can supply fake dialogs and file operations.
/// </summary>
public sealed class DocumentWorkflow(
    ExplorerSession session, IUserInteractionService dialogs, IWorkspaceFiles files, Action<string> reportStatus) : IDisposable
{
    // Pasted bytes still need saving even before an edit changes the model's dirty flag.
    private bool _unsavedNewDocument;
    // Disposed means the owning window has closed; late async completions must stay quiet.
    private bool _disposed;

    /// <summary>Combines tracked edits with the need to save newly pasted data for the first time.</summary>
    public bool HasUnsavedChanges => session.Document.IsDirty || _unsavedNewDocument;

    /// <summary>
    /// Returns true only when it is safe to leave the current document. A canceled
    /// save also returns false, keeping the current exploration open.
    /// </summary>
    public bool ConfirmClose()
    {
        if (_disposed || session.IsBusy) return false;
        if (!HasUnsavedChanges) return true;
        // A switch expression maps each answer to a result; the default includes Cancel.
        return dialogs.ConfirmSaveChanges() switch
        {
            SaveDecision.Discard => true,
            SaveDecision.Save => SaveProject(),
            _ => false
        };
    }

    /// <summary>Collects a filename, then delegates the actual loading and save prompt to OpenPathAsync.</summary>
    public async Task OpenAsync()
    {
        if (_disposed || session.IsBusy) return;
        string? path = dialogs.ChooseOpenFile("Open binary file or project", "All files|*.*|Bit Explorer project|*.bitexplorer");
        if (path is not null) await OpenPathAsync(path);
    }

    /// <summary>
    /// Loads a known path, also used by file drops and startup arguments. Await yields
    /// while reading and resumes to replace the shared document only after a successful read.
    /// </summary>
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

    /// <summary>Installs an in-memory document after resolving any unsaved work in the old one.</summary>
    public bool ReplaceWithNew(DocumentModel document, string name, bool needsSave)
    {
        if (!ConfirmClose()) return false;
        _unsavedNewDocument = needsSave;
        session.ReplaceDocument(document, name);
        return true;
    }

    /// <summary>Saves the actual byte values; field labels and display preferences require a project save.</summary>
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

    /// <summary>Saves bytes, annotations and settings together, then updates the displayed filename.</summary>
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

    /// <summary>
    /// Exports the whole file using a copy of the current display format. The busy
    /// state prevents edits from changing source bytes partway through that export.
    /// </summary>
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

    /// <summary>Replaces the field layout through the model, which makes the replacement undoable.</summary>
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

    /// <summary>Saves only reusable field definitions; the current binary and settings remain in place.</summary>
    public void ExportFields()
    {
        if (_disposed || session.IsBusy) return;
        string? path = dialogs.ChooseSaveFile("Export reusable field layout", "Bit Explorer field layout|*.bitfields", "field-layout.bitfields");
        if (path is not null) Run(() => { session.Document.ExportTemplate(path); reportStatus("Exported reusable field definitions."); });
    }

    /// <summary>Runs an operation on the calling thread and converts exceptions into a message and a false result.</summary>
    private bool Run(Action operation)
    {
        if (_disposed || session.IsBusy) return false;
        try { operation(); return true; }
        catch (Exception error) { ReportError(error); return false; }
    }

    /// <summary>
    /// Holds the shared busy flag during a long operation. All panels observe that
    /// flag to disable changes. finally restores it on success or failure unless
    /// the exploration was disposed while the operation was running.
    /// </summary>
    private async Task RunBusyAsync(string message, Func<Task> operation)
    {
        if (_disposed || session.IsBusy) return;
        session.IsBusy = true;
        reportStatus(message);
        try { await operation(); }
        catch (Exception error) { ReportError(error); }
        finally { if (!_disposed) session.IsBusy = false; }
    }

    /// <summary>Shows recoverable errors only while the owning exploration is still alive.</summary>
    private void ReportError(Exception error)
    {
        if (_disposed) return;
        reportStatus(error.Message);
        dialogs.ShowError(error.Message);
    }

    /// <summary>
    /// Marks the workflow closed. It does not cancel an in-progress disk write;
    /// completion checks prevent that operation from updating a closed window.
    /// </summary>
    public void Dispose() => _disposed = true;
}
