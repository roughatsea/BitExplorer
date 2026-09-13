using BitExplorer.Core;
using WpfApp1.Services;

internal sealed class FakeUserInteractionService : IUserInteractionService
{
    public Queue<string?> OpenPaths { get; } = new();
    public Queue<string?> SavePaths { get; } = new();
    public Queue<string?> TextResponses { get; } = new();
    public Queue<SaveDecision> SaveDecisions { get; } = new();
    public List<string> Errors { get; } = [];
    public List<string> HelpMessages { get; } = [];
    public List<string> ClipboardValues { get; } = [];
    public List<NamedField> FieldDrafts { get; } = [];
    public Func<NamedField, long, NamedField?>? FieldEditor { get; set; }
    public int ConfirmSaveCount { get; private set; }
    public int SaveDialogCount { get; private set; }

    public string? ChooseOpenFile(string title, string filter) => OpenPaths.TryDequeue(out var path) ? path : null;
    public string? ChooseSaveFile(string title, string filter, string suggestedName, string? existingPath = null)
    {
        SaveDialogCount++;
        return SavePaths.TryDequeue(out var path) ? path : null;
    }
    public NamedField? EditField(NamedField draft, long fileBitLength)
    {
        FieldDrafts.Add(draft.Clone());
        return FieldEditor?.Invoke(draft, fileBitLength);
    }
    public string? RequestText(string title, string label, string initialText = "", bool multiline = false) =>
        TextResponses.TryDequeue(out var value) ? value : null;
    public SaveDecision ConfirmSaveChanges()
    {
        ConfirmSaveCount++;
        return SaveDecisions.TryDequeue(out var decision) ? decision : SaveDecision.Cancel;
    }
    public void ShowError(string message) => Errors.Add(message);
    public void ShowHelp(string message) => HelpMessages.Add(message);
    public void SetClipboardText(string text) => ClipboardValues.Add(text);
}
