using System.IO;
using System.Windows;
using BitExplorer.Core;
using Microsoft.Win32;
using WpfApp1.Dialogs;

namespace WpfApp1.Services;

/// <summary>The sole adapter from application commands to native dialogs and the clipboard.</summary>
public sealed class WpfUserInteractionService(Window owner) : IUserInteractionService
{
    public string? ChooseOpenFile(string title, string filter)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = filter };
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }

    public string? ChooseSaveFile(string title, string filter, string suggestedName, string? existingPath = null)
    {
        var dialog = new SaveFileDialog
        {
            Title = title, Filter = filter, FileName = suggestedName,
            InitialDirectory = existingPath is null ? "" : Path.GetDirectoryName(existingPath) ?? ""
        };
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }

    public NamedField? EditField(NamedField draft, long fileBitLength)
    {
        var dialog = new FieldEditorWindow(owner, draft.Clone(), fileBitLength);
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public string? RequestText(string title, string label, string initialText = "", bool multiline = false)
    {
        var dialog = new TextInputWindow(owner, title, label, initialText, multiline);
        return dialog.ShowDialog() == true ? dialog.Value : null;
    }

    public SaveDecision ConfirmSaveChanges() => MessageBox.Show(owner,
        "Save the current exploration before continuing? A project preserves edited bytes, field labels and display settings.",
        "Unsaved exploration", MessageBoxButton.YesNoCancel, MessageBoxImage.Question) switch
    {
        MessageBoxResult.Yes => SaveDecision.Save,
        MessageBoxResult.No => SaveDecision.Discard,
        _ => SaveDecision.Cancel
    };

    public void ShowError(string message) => MessageBox.Show(owner, message, "Bit Explorer", MessageBoxButton.OK, MessageBoxImage.Warning);
    public void ShowHelp(string message) => MessageBox.Show(owner, message, "Using Bit Explorer", MessageBoxButton.OK, MessageBoxImage.Information);
    public void SetClipboardText(string text) => Clipboard.SetText(text);
}
