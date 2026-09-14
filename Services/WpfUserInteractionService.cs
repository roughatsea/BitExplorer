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
// Make names from System.Windows available here without writing their full prefix each time. This does not
// run that library's code.
using System.Windows;
// Make names from BitExplorer.Core available here without writing their full prefix each time. This does
// not run that library's code.
using BitExplorer.Core;
// Make names from Microsoft.Win32 available here without writing their full prefix each time. This does not
// run that library's code.
using Microsoft.Win32;
// Make names from WpfApp1.Dialogs available here without writing their full prefix each time. This does not
// run that library's code.
using WpfApp1.Dialogs;

// Place this file's definitions in the WpfApp1.Services naming group, which prevents clashes with names in
// other groups.
namespace WpfApp1.Services;

/// <summary>
/// Connects the application-facing interaction interface to real WPF and Windows
/// dialogs. The owner constructor parameter is the main window, keeping dialogs
/// attached to it. View models depend on the interface, so they need no Window objects.
/// </summary>
public sealed class WpfUserInteractionService(Window owner) : IUserInteractionService
{
    /// <summary>Shows the Windows open dialog; accepting returns a path and cancellation returns null.</summary>
    public string? ChooseOpenFile(string title, string filter)
    {
        // Remember a new OpenFileDialog object; the entries in braces set its initial contents or
        // properties as dialog.
        var dialog = new OpenFileDialog { Title = title, Filter = filter };
        // ShowDialog returns a nullable bool. Only an explicit true means the user accepted.
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }

    /// <summary>Shows the save dialog, suggesting the previous folder when a prior path exists.</summary>
    public string? ChooseSaveFile(string title, string filter, string suggestedName, string? existingPath = null)
    {
        // Remember a new SaveFileDialog object; the entries in braces set its initial contents or
        // properties as dialog.
        var dialog = new SaveFileDialog
        {
            // Set this new object's Title entry to title.
            Title = title, Filter = filter, FileName = suggestedName,
            // Set this new object's InitialDirectory entry to empty text when existingPath matches null;
            // otherwise the folder portion of existingPath, falling back to empty text if it is null.
            InitialDirectory = existingPath is null ? "" : Path.GetDirectoryName(existingPath) ?? ""
        };
        // Return dialog.FileName when dialog.ShowDialog(owner) equals true; otherwise null (no value) to
        // the caller and leave this method.
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }

    /// <summary>
    /// Gives the editor its own copy. Typing into the dialog cannot mutate the live
    /// field; its accepted result is later applied through the document's undoable API.
    /// </summary>
    public NamedField? EditField(NamedField draft, long fileBitLength)
    {
        // Remember a new FieldEditorWindow object using the inputs in parentheses as dialog.
        var dialog = new FieldEditorWindow(owner, draft.Clone(), fileBitLength);
        // Return dialog.Result when dialog.ShowDialog() equals true; otherwise null (no value) to the
        // caller and leave this method.
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    /// <summary>Shows the reusable text-entry dialog for offsets and pasted hexadecimal data.</summary>
    public string? RequestText(string title, string label, string initialText = "", bool multiline = false)
    {
        // Remember a new TextInputWindow object using the inputs in parentheses as dialog.
        var dialog = new TextInputWindow(owner, title, label, initialText, multiline);
        // Return dialog.Value when dialog.ShowDialog() equals true; otherwise null (no value) to the caller
        // and leave this method.
        return dialog.ShowDialog() == true ? dialog.Value : null;
    }

    /// <summary>Translates Windows-specific button results into the application's save decision.</summary>
    public SaveDecision ConfirmSaveChanges() => MessageBox.Show(owner,
        "Save the current exploration before continuing? A project preserves edited bytes, field labels and display settings.",
        "Unsaved exploration", MessageBoxButton.YesNoCancel, MessageBoxImage.Question) switch
    {
        // For MessageBoxResult.Yes, use SaveDecision.Save.
        MessageBoxResult.Yes => SaveDecision.Save,
        // For MessageBoxResult.No, use SaveDecision.Discard.
        MessageBoxResult.No => SaveDecision.Discard,
        // For any remaining case, use SaveDecision.Cancel.
        _ => SaveDecision.Cancel
    };

    /// <summary>Shows an operation error without requiring callers to know MessageBox options.</summary>
    public void ShowError(string message) => MessageBox.Show(owner, message, "Bit Explorer", MessageBoxButton.OK, MessageBoxImage.Warning);
    /// <summary>Shows help with the informational dialog appearance.</summary>
    public void ShowHelp(string message) => MessageBox.Show(owner, message, "Using Bit Explorer", MessageBoxButton.OK, MessageBoxImage.Information);
    /// <summary>Writes to the Windows clipboard on the calling UI thread.</summary>
    public void SetClipboardText(string text) => Clipboard.SetText(text);
}
