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
// Make names from WpfApp1.Services available here without writing their full prefix each time. This does
// not run that library's code.
using WpfApp1.Services;

/// <summary>
/// A substitute for real dialogs and the clipboard. Tests queue the answers a
/// pretend user would give, then inspect the recorded calls. This is dependency
/// injection: the same view-model code runs with a different interface implementation.
/// </summary>
internal sealed class FakeUserInteractionService : IUserInteractionService
{
    // Queues return prepared answers in first-in, first-out order. null means the
    // user cancelled a chooser/input. Empty queues default to cancellation too.
    public Queue<string?> OpenPaths { get; } = new();
    // Expose SavePaths as an object or value of type Queue<string?>. get allows reading; set, if present,
    // handles writing. Its starting value is a new object of the required type.
    public Queue<string?> SavePaths { get; } = new();
    // Expose TextResponses as an object or value of type Queue<string?>. get allows reading; set, if
    // present, handles writing. Its starting value is a new object of the required type.
    public Queue<string?> TextResponses { get; } = new();
    // Expose SaveDecisions as an object or value of type Queue<SaveDecision>. get allows reading; set, if
    // present, handles writing. Its starting value is a new object of the required type.
    public Queue<SaveDecision> SaveDecisions { get; } = new();
    // These lists record requested output without showing UI or changing the clipboard.
    public List<string> Errors { get; } = [];
    // Expose HelpMessages as a resizable ordered list. get allows reading; set, if present, handles
    // writing. Its starting value is an empty collection.
    public List<string> HelpMessages { get; } = [];
    // Expose ClipboardValues as a resizable ordered list. get allows reading; set, if present, handles
    // writing. Its starting value is an empty collection.
    public List<string> ClipboardValues { get; } = [];
    // Expose FieldDrafts as a resizable ordered list. get allows reading; set, if present, handles writing.
    // Its starting value is an empty collection.
    public List<NamedField> FieldDrafts { get; } = [];
    /// <summary>Optional test function that changes a draft and returns it, or cancels with null.</summary>
    public Func<NamedField, long, NamedField?>? FieldEditor { get; set; }
    // Counters let tests detect unwanted prompts even when a prompt changes no data.
    public int ConfirmSaveCount { get; private set; }
    // Expose SaveDialogCount as a whole number. get allows reading; set, if present, handles writing.
    public int SaveDialogCount { get; private set; }

    /// <summary>Consumes the next planned open-file answer.</summary>
    public string? ChooseOpenFile(string title, string filter) => OpenPaths.TryDequeue(out var path) ? path : null;
    /// <summary>Records a save chooser and consumes its planned answer.</summary>
    public string? ChooseSaveFile(string title, string filter, string suggestedName, string? existingPath = null)
    {
        // Increase SaveDialogCount by one.
        SaveDialogCount++;
        // Return path when SavePaths.TryDequeue(out var path) is true; otherwise null (no value) to the
        // caller and leave this method.
        return SavePaths.TryDequeue(out var path) ? path : null;
    }
    /// <summary>Records the original draft before letting a scenario edit or cancel it.</summary>
    public NamedField? EditField(NamedField draft, long fileBitLength)
    {
        // Store a clone: later changes to the draft must not rewrite our call record.
        FieldDrafts.Add(draft.Clone());
        // Return FieldEditor?.Invoke(draft, fileBitLength) to the caller and leave this method.
        return FieldEditor?.Invoke(draft, fileBitLength);
    }
    /// <summary>Returns the next planned text entry, without opening an input window.</summary>
    public string? RequestText(string title, string label, string initialText = "", bool multiline = false) =>
        TextResponses.TryDequeue(out var value) ? value : null;
    /// <summary>Records a save/discard/cancel question, defaulting to keeping the work open.</summary>
    public SaveDecision ConfirmSaveChanges()
    {
        // Increase ConfirmSaveCount by one.
        ConfirmSaveCount++;
        // Return decision when SaveDecisions.TryDequeue(out var decision) is true; otherwise
        // SaveDecision.Cancel to the caller and leave this method.
        return SaveDecisions.TryDequeue(out var decision) ? decision : SaveDecision.Cancel;
    }
    /// <summary>Captures validation or I/O failures for assertions.</summary>
    public void ShowError(string message) => Errors.Add(message);
    /// <summary>Captures help text without a message box.</summary>
    public void ShowHelp(string message) => HelpMessages.Add(message);
    /// <summary>Captures copied text while leaving the real Windows clipboard untouched.</summary>
    public void SetClipboardText(string text) => ClipboardValues.Add(text);
}
