using BitExplorer.Core;

namespace WpfApp1.Services;

public interface IWorkspaceFiles
{
    Task<DocumentModel> OpenAsync(string path);
    Task ExportTextAsync(string path, DocumentModel document, DisplaySettings settings);
}
