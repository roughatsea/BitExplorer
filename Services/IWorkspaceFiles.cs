using BitExplorer.Core;

namespace WpfApp1.Services;

/// <summary>
/// Provides the potentially slow file operations. Returning Task lets a caller
/// await completion without blocking the UI; test implementations can simulate
/// a delayed read, an export failure, or a successful operation without real files.
/// </summary>
public interface IWorkspaceFiles
{
    /// <summary>Loads either a binary file or a saved project into a new document.</summary>
    Task<DocumentModel> OpenAsync(string path);
    /// <summary>Writes the entire document as formatted text using the supplied display settings.</summary>
    Task ExportTextAsync(string path, DocumentModel document, DisplaySettings settings);
}
