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

// Make names from BitExplorer.Core available here without writing their full prefix each time. This does
// not run that library's code.
using BitExplorer.Core;

// Place this file's definitions in the WpfApp1.Services naming group, which prevents clashes with names in
// other groups.
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
