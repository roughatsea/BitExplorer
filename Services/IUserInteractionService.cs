using BitExplorer.Core;

namespace WpfApp1.Services;

/// <summary>
/// The three possible answers when leaving an unsaved document: save first,
/// continue without saving, or stay in the current document.
/// </summary>
public enum SaveDecision { Save, Discard, Cancel }

/// <summary>
/// Describes the user interactions that application commands need. An interface is
/// a contract: the real implementation opens Windows dialogs, while tests supply
/// predetermined answers without opening windows or touching the clipboard.
/// </summary>
public interface IUserInteractionService
{
    /// <summary>Returns the chosen input path, or null when the user cancels.</summary>
    string? ChooseOpenFile(string title, string filter);
    /// <summary>Asks where to save, using the suggested name and optionally the previous folder.</summary>
    string? ChooseSaveFile(string title, string filter, string suggestedName, string? existingPath = null);
    /// <summary>Edits a temporary field definition; null means discard the draft without changing the document.</summary>
    NamedField? EditField(NamedField draft, long fileBitLength);
    /// <summary>Collects one line or several lines of text; a null result means cancellation.</summary>
    string? RequestText(string title, string label, string initialText = "", bool multiline = false);
    /// <summary>Asks how to handle unsaved work before closing or replacing the current document.</summary>
    SaveDecision ConfirmSaveChanges();
    /// <summary>Displays a recoverable operation error in a user-visible dialog.</summary>
    void ShowError(string message);
    /// <summary>Displays the application's usage instructions.</summary>
    void ShowHelp(string message);
    /// <summary>Copies prepared plain text; the caller chooses its data and formatting.</summary>
    void SetClipboardText(string text);
}
