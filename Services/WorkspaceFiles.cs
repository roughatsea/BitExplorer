using System.IO;
using System.Text;
using BitExplorer.Core;

namespace WpfApp1.Services;

/// <summary>File operations that run off the UI thread. The caller owns the editing lock while exporting.</summary>
public sealed class WorkspaceFiles : IWorkspaceFiles
{
    public Task<DocumentModel> OpenAsync(string path) => Task.Run(() =>
        Path.GetExtension(path).Equals(".bitexplorer", StringComparison.OrdinalIgnoreCase)
            ? DocumentModel.OpenProject(path) : DocumentModel.OpenBinary(path));

    public Task ExportTextAsync(string path, DocumentModel document, DisplaySettings settings)
    {
        // A stable layout snapshot ensures the whole file uses one format.
        var format = settings.Clone();
        return Task.Run(() =>
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var writer = new StreamWriter(temporary, false, new UTF8Encoding(false)))
                    DisplayFormatter.Export(writer, document, format);
                File.Move(temporary, path, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        });
    }
}
