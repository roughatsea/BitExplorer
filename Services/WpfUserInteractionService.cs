using System.IO;
using System.Windows;
using BitExplorer.Core;
using Microsoft.Win32;
using WpfApp1.Dialogs;

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
        var dialog = new OpenFileDialog { Title = title, Filter = filter };
        // ShowDialog returns a nullable bool. Only an explicit true means the user accepted.
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }

    /// <summary>Shows the save dialog, suggesting the previous folder when a prior path exists.</summary>
    public string? ChooseSaveFile(string title, string filter, string suggestedName, string? existingPath = null)
    {
        var dialog = new SaveFileDialog
        {
            Title = title, Filter = filter, FileName = suggestedName,
            InitialDirectory = existingPath is null ? "" : Path.GetDirectoryName(existingPath) ?? ""
        };
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }

    /// <summary>
    /// Gives the editor its own copy. Typing into the dialog cannot mutate the live
    /// field; its accepted result is later applied through the document's undoable API.
    /// </summary>
    public NamedField? EditField(NamedField draft, long fileBitLength)
    {
        var dialog = new FieldEditorWindow(owner, draft.Clone(), fileBitLength);
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    /// <summary>Shows the reusable text-entry dialog for offsets and pasted hexadecimal data.</summary>
    public string? RequestText(string title, string label, string initialText = "", bool multiline = false)
    {
        var dialog = new TextInputWindow(owner, title, label, initialText, multiline);
        return dialog.ShowDialog() == true ? dialog.Value : null;
    }

    /// <summary>Translates Windows-specific button results into the application's save decision.</summary>
    public SaveDecision ConfirmSaveChanges() => MessageBox.Show(owner,
        "Save the current exploration before continuing? A project preserves edited bytes, field labels and display settings.",
        "Unsaved exploration", MessageBoxButton.YesNoCancel, MessageBoxImage.Question) switch
    {
        MessageBoxResult.Yes => SaveDecision.Save,
        MessageBoxResult.No => SaveDecision.Discard,
        _ => SaveDecision.Cancel
    };

    /// <summary>Shows an operation error without requiring callers to know MessageBox options.</summary>
    public void ShowError(string message) => MessageBox.Show(owner, message, "Bit Explorer", MessageBoxButton.OK, MessageBoxImage.Warning);
    /// <summary>Shows help with the informational dialog appearance.</summary>
    public void ShowHelp(string message) => MessageBox.Show(owner, message, "Using Bit Explorer", MessageBoxButton.OK, MessageBoxImage.Information);
    /// <summary>Writes to the Windows clipboard on the calling UI thread.</summary>
    public void SetClipboardText(string text) => Clipboard.SetText(text);
}
