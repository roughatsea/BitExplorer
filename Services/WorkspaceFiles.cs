using System.IO;
using System.Text;
using BitExplorer.Core;

namespace WpfApp1.Services;

/// <summary>
/// Performs file work on a worker thread so Windows can keep drawing and processing
/// its UI. The document workflow disables editing during export: the settings are
/// copied here, but the binary data is read from the existing document.
/// </summary>
public sealed class WorkspaceFiles : IWorkspaceFiles
{
    /// <summary>
    /// Task.Run schedules the read away from the UI thread. The extension determines
    /// whether to restore a complete exploration or load a plain sequence of bytes.
    /// </summary>
    public Task<DocumentModel> OpenAsync(string path) => Task.Run(() =>
        Path.GetExtension(path).Equals(".bitexplorer", StringComparison.OrdinalIgnoreCase)
            ? DocumentModel.OpenProject(path) : DocumentModel.OpenBinary(path));

    /// <summary>
    /// Writes all rows, regardless of scrolling. A temporary file is completed before
    /// replacing the destination, so a formatting failure does not leave a half-written export.
    /// </summary>
    public Task ExportTextAsync(string path, DocumentModel document, DisplaySettings settings)
    {
        // A stable layout snapshot ensures the whole file uses one format.
        var format = settings.Clone();
        return Task.Run(() =>
        {
            // A random identifier prevents simultaneous exports from sharing a temporary name.
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                // using closes the file even if writing throws. UTF8Encoding(false)
                // avoids adding a byte-order marker before the user's displayed text.
                using (var writer = new StreamWriter(temporary, false, new UTF8Encoding(false)))
                    DisplayFormatter.Export(writer, document, format);
                File.Move(temporary, path, true);
            }
            // finally runs on success and failure, cleaning up any unfinished temporary file.
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        });
    }
}
