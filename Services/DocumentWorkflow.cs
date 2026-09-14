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
// Make names from WpfApp1.ViewModels available here without writing their full prefix each time. This does
// not run that library's code.
using WpfApp1.ViewModels;

// Place this file's definitions in the WpfApp1.Services naming group, which prevents clashes with names in
// other groups.
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
        // If _disposed is true, or session.IsBusy is true, return false (no) to the caller and leave this
        // method.
        if (_disposed || session.IsBusy) return false;
        // If it is not the case that HasUnsavedChanges is true, return true (yes) to the caller and leave
        // this method.
        if (!HasUnsavedChanges) return true;
        // A switch expression maps each answer to a result; the default includes Cancel.
        return dialogs.ConfirmSaveChanges() switch
        {
            // For SaveDecision.Discard, use true (yes).
            SaveDecision.Discard => true,
            // For SaveDecision.Save, use the result returned by SaveProject().
            SaveDecision.Save => SaveProject(),
            // For any remaining case, use false (no).
            _ => false
        };
    }

    /// <summary>Collects a filename, then delegates the actual loading and save prompt to OpenPathAsync.</summary>
    public async Task OpenAsync()
    {
        // If _disposed is true, or session.IsBusy is true, leave this method immediately.
        if (_disposed || session.IsBusy) return;
        // Remember the result returned by dialogs.ChooseOpenFile(...) as path.
        string? path = dialogs.ChooseOpenFile("Open binary file or project", "All files|*.*|Bit Explorer project|*.bitexplorer");
        // If path matches not null, wait for OpenPathAsync to finish before continuing; await lets the
        // thread service other work while the operation is pending.
        if (path is not null) await OpenPathAsync(path);
    }

    /// <summary>
    /// Loads a known path, also used by file drops and startup arguments. Await yields
    /// while reading and resumes to replace the shared document only after a successful read.
    /// </summary>
    public async Task OpenPathAsync(string path, bool confirm = true)
    {
        // If _disposed is true, or session.IsBusy is true, or (both confirm is true and it is not the case
        // that ConfirmClose() is true), leave this method immediately.
        if (_disposed || session.IsBusy || (confirm && !ConfirmClose())) return;
        // Wait for RunBusyAsync to finish before continuing; await lets the thread service other work while
        // the operation is pending.
        await RunBusyAsync("Opening file…", async () =>
        {
            // Remember the result of files.OpenAsync(path) after that operation finishes; await allows the
            // caller's thread to do other work while it waits as document.
            var document = await files.OpenAsync(path);
            // The window may have been disposed while the file was being read.
            if (_disposed) return;
            // Set _unsavedNewDocument to false (no).
            _unsavedNewDocument = false;
            // Call session.ReplaceDocument: Detaches the old model, installs the new one, and resets
            // selection.
            session.ReplaceDocument(document, Path.GetFileName(path));
            // Send the supplied message to the application's status display.
            reportStatus($"Opened {Path.GetFileName(path)} · {document.Data.Length:N0} bytes.");
        });
    }

    /// <summary>Installs an in-memory document after resolving any unsaved work in the old one.</summary>
    public bool ReplaceWithNew(DocumentModel document, string name, bool needsSave)
    {
        // If it is not the case that ConfirmClose() is true, return false (no) to the caller and leave this
        // method.
        if (!ConfirmClose()) return false;
        // Set _unsavedNewDocument to needsSave.
        _unsavedNewDocument = needsSave;
        // Call session.ReplaceDocument: Detaches the old model, installs the new one, and resets selection.
        session.ReplaceDocument(document, name);
        // Return true (yes) to the caller and leave this method.
        return true;
    }

    /// <summary>Saves the actual byte values; field labels and display preferences require a project save.</summary>
    public bool SaveBinary()
    {
        // If _disposed is true, or session.IsBusy is true, return false (no) to the caller and leave this
        // method.
        if (_disposed || session.IsBusy) return false;
        // Remember session.Document as document.
        var document = session.Document;
        // Remember the result returned by dialogs.ChooseSaveFile(...) as path.
        string? path = dialogs.ChooseSaveFile("Save edited binary", "Binary file|*.bin|All files|*.*",
            Path.GetFileName(document.FilePath ?? "data.bin"), document.FilePath);
        // If path matches null, return false (no) to the caller and leave this method.
        if (path is null) return false;
        // Return the result returned by Run(...) to the caller and leave this method.
        return Run(() =>
        {
            // Call document.SaveBinary: Writes only raw bytes, then updates the binary baseline if the
            // write succeeds.
            document.SaveBinary(path);
            // Set _unsavedNewDocument to false (no).
            _unsavedNewDocument = false;
            // Set session.DisplayName to the filename portion of path, without its folder.
            session.DisplayName = Path.GetFileName(path);
            // Send the supplied message to the application's status display.
            reportStatus("Binary saved. Use Save project to preserve field definitions and display settings.");
        });
    }

    /// <summary>Saves bytes, annotations and settings together, then updates the displayed filename.</summary>
    public bool SaveProject()
    {
        // If _disposed is true, or session.IsBusy is true, return false (no) to the caller and leave this
        // method.
        if (_disposed || session.IsBusy) return false;
        // Remember session.Document as document.
        var document = session.Document;
        // Remember the filename from document.FilePath ?? "exploration", without its folder or extension +
        // the text ".bitexplorer" (addition, or joining text) when document.ProjectPath matches null;
        // otherwise the filename portion of document.ProjectPath, without its folder as suggested.
        string suggested = document.ProjectPath is null
            ? Path.GetFileNameWithoutExtension(document.FilePath ?? "exploration") + ".bitexplorer"
            : Path.GetFileName(document.ProjectPath);
        // Remember the result returned by dialogs.ChooseSaveFile(...) as path.
        string? path = dialogs.ChooseSaveFile("Save binary, fields and view as a project", "Bit Explorer project|*.bitexplorer", suggested, document.ProjectPath);
        // If path matches null, return false (no) to the caller and leave this method.
        if (path is null) return false;
        // Return the result returned by Run(...) to the caller and leave this method.
        return Run(() =>
        {
            // Call document.SaveProject: Saves edited bytes, display settings, and field definitions
            // together in versioned JSON.
            document.SaveProject(path);
            // Set _unsavedNewDocument to false (no).
            _unsavedNewDocument = false;
            // Set session.DisplayName to the filename portion of path, without its folder.
            session.DisplayName = Path.GetFileName(path);
            // Send the supplied message to the application's status display.
            reportStatus("Project saved with edited data, field mappings and display settings.");
        });
    }

    /// <summary>
    /// Exports the whole file using a copy of the current display format. The busy
    /// state prevents edits from changing source bytes partway through that export.
    /// </summary>
    public async Task ExportTextAsync()
    {
        // If _disposed is true, or session.IsBusy is true, leave this method immediately.
        if (_disposed || session.IsBusy) return;
        // Remember session.Document as document.
        var document = session.Document;
        // Remember the result returned by dialogs.ChooseSaveFile(...) as path.
        string? path = dialogs.ChooseSaveFile("Export the entire file in the current display format", "Formatted text|*.txt",
            Path.GetFileNameWithoutExtension(document.FilePath ?? "exploration") + ".txt");
        // If path matches null, leave this method immediately.
        if (path is null) return;
        // Wait for RunBusyAsync to finish before continuing; await lets the thread service other work while
        // the operation is pending.
        await RunBusyAsync("Exporting entire file…", async () =>
        {
            // Wait for files.ExportTextAsync to finish before continuing; await lets the thread service
            // other work while the operation is pending.
            await files.ExportTextAsync(path, document, document.Settings.Clone());
            // If it is not the case that _disposed is true, send the supplied message to the application's
            // status display.
            if (!_disposed) reportStatus($"Exported all {document.Data.Length:N0} bytes as formatted text.");
        });
    }

    /// <summary>Replaces the field layout through the model, which makes the replacement undoable.</summary>
    public void ImportFields()
    {
        // If _disposed is true, or session.IsBusy is true, leave this method immediately.
        if (_disposed || session.IsBusy) return;
        // Remember the result returned by dialogs.ChooseOpenFile(...) as path.
        string? path = dialogs.ChooseOpenFile("Import field layout (replaces current labels; undoable)", "Field layout|*.bitfields;*.json|All files|*.*");
        // If path matches not null, call Run: Runs an operation on the calling thread and converts
        // exceptions into a message and a false result.
        if (path is not null) Run(() =>
        {
            // Call session.Document.ImportTemplate: Replaces the current field layout in one undoable
            // operation; the binary is untouched.
            session.Document.ImportTemplate(path);
            // Send the supplied message to the application's status display.
            reportStatus("Imported field layout. Ctrl+Z restores the previous layout.");
        });
    }

    /// <summary>Saves only reusable field definitions; the current binary and settings remain in place.</summary>
    public void ExportFields()
    {
        // If _disposed is true, or session.IsBusy is true, leave this method immediately.
        if (_disposed || session.IsBusy) return;
        // Remember the result returned by dialogs.ChooseSaveFile(...) as path.
        string? path = dialogs.ChooseSaveFile("Export reusable field layout", "Bit Explorer field layout|*.bitfields", "field-layout.bitfields");
        // If path matches not null, call Run: Runs an operation on the calling thread and converts
        // exceptions into a message and a false result.
        if (path is not null) Run(() => { session.Document.ExportTemplate(path); reportStatus("Exported reusable field definitions."); });
    }

    /// <summary>Runs an operation on the calling thread and converts exceptions into a message and a false result.</summary>
    private bool Run(Action operation)
    {
        // If _disposed is true, or session.IsBusy is true, return false (no) to the caller and leave this
        // method.
        if (_disposed || session.IsBusy) return false;
        // Attempt this work. A matching catch below handles a reported exception; a finally block, when
        // present, performs cleanup on the way out.
        try { operation(); return true; }
        // If the preceding try reports Exception, refer to it as error; run this recovery path.
        catch (Exception error) { ReportError(error); return false; }
    }

    /// <summary>
    /// Holds the shared busy flag during a long operation. All panels observe that
    /// flag to disable changes. finally restores it on success or failure unless
    /// the exploration was disposed while the operation was running.
    /// </summary>
    private async Task RunBusyAsync(string message, Func<Task> operation)
    {
        // If _disposed is true, or session.IsBusy is true, leave this method immediately.
        if (_disposed || session.IsBusy) return;
        // Set session.IsBusy to true (yes).
        session.IsBusy = true;
        // Send the supplied message to the application's status display.
        reportStatus(message);
        // Attempt this work. A matching catch below handles a reported exception; a finally block, when
        // present, performs cleanup on the way out.
        try { await operation(); }
        // If the preceding try reports Exception, refer to it as error; run this recovery path.
        catch (Exception error) { ReportError(error); }
        // Run this cleanup when leaving the try, whether the work succeeded or reported an exception.
        finally { if (!_disposed) session.IsBusy = false; }
    }

    /// <summary>Shows recoverable errors only while the owning exploration is still alive.</summary>
    private void ReportError(Exception error)
    {
        // If _disposed is true, leave this method immediately.
        if (_disposed) return;
        // Send the supplied message to the application's status display.
        reportStatus(error.Message);
        // Show the supplied error message through the interaction service.
        dialogs.ShowError(error.Message);
    }

    /// <summary>
    /// Marks the workflow closed. It does not cancel an in-progress disk write;
    /// completion checks prevent that operation from updating a closed window.
    /// </summary>
    public void Dispose() => _disposed = true;
}
