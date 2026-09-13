using BitExplorer.Core;
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
    public Queue<string?> SavePaths { get; } = new();
    public Queue<string?> TextResponses { get; } = new();
    public Queue<SaveDecision> SaveDecisions { get; } = new();
    // These lists record requested output without showing UI or changing the clipboard.
    public List<string> Errors { get; } = [];
    public List<string> HelpMessages { get; } = [];
    public List<string> ClipboardValues { get; } = [];
    public List<NamedField> FieldDrafts { get; } = [];
    /// <summary>Optional test function that changes a draft and returns it, or cancels with null.</summary>
    public Func<NamedField, long, NamedField?>? FieldEditor { get; set; }
    // Counters let tests detect unwanted prompts even when a prompt changes no data.
    public int ConfirmSaveCount { get; private set; }
    public int SaveDialogCount { get; private set; }

    /// <summary>Consumes the next planned open-file answer.</summary>
    public string? ChooseOpenFile(string title, string filter) => OpenPaths.TryDequeue(out var path) ? path : null;
    /// <summary>Records a save chooser and consumes its planned answer.</summary>
    public string? ChooseSaveFile(string title, string filter, string suggestedName, string? existingPath = null)
    {
        SaveDialogCount++;
        return SavePaths.TryDequeue(out var path) ? path : null;
    }
    /// <summary>Records the original draft before letting a scenario edit or cancel it.</summary>
    public NamedField? EditField(NamedField draft, long fileBitLength)
    {
        // Store a clone: later changes to the draft must not rewrite our call record.
        FieldDrafts.Add(draft.Clone());
        return FieldEditor?.Invoke(draft, fileBitLength);
    }
    /// <summary>Returns the next planned text entry, without opening an input window.</summary>
    public string? RequestText(string title, string label, string initialText = "", bool multiline = false) =>
        TextResponses.TryDequeue(out var value) ? value : null;
    /// <summary>Records a save/discard/cancel question, defaulting to keeping the work open.</summary>
    public SaveDecision ConfirmSaveChanges()
    {
        ConfirmSaveCount++;
        return SaveDecisions.TryDequeue(out var decision) ? decision : SaveDecision.Cancel;
    }
    /// <summary>Captures validation or I/O failures for assertions.</summary>
    public void ShowError(string message) => Errors.Add(message);
    /// <summary>Captures help text without a message box.</summary>
    public void ShowHelp(string message) => HelpMessages.Add(message);
    /// <summary>Captures copied text while leaving the real Windows clipboard untouched.</summary>
    public void SetClipboardText(string text) => ClipboardValues.Add(text);
}
