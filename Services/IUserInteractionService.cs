using BitExplorer.Core;

namespace WpfApp1.Services;

public enum SaveDecision { Save, Discard, Cancel }

/// <summary>Desktop interactions needed by application commands, independent of any particular window.</summary>
public interface IUserInteractionService
{
    string? ChooseOpenFile(string title, string filter);
    string? ChooseSaveFile(string title, string filter, string suggestedName, string? existingPath = null);
    NamedField? EditField(NamedField draft, long fileBitLength);
    string? RequestText(string title, string label, string initialText = "", bool multiline = false);
    SaveDecision ConfirmSaveChanges();
    void ShowError(string message);
    void ShowHelp(string message);
    void SetClipboardText(string text);
}
