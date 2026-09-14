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

// Make names from System.IO available here without writing their full prefix each time. This does not run
// that library's code.
using System.IO;
// Make names from System.Text available here without writing their full prefix each time. This does not run
// that library's code.
using System.Text;
// Make names from BitExplorer.Core available here without writing their full prefix each time. This does
// not run that library's code.
using BitExplorer.Core;

// Place this file's definitions in the WpfApp1.Services naming group, which prevents clashes with names in
// other groups.
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
        // Return the result returned by Task.Run(...) to the caller and leave this method.
        return Task.Run(() =>
        {
            // A random identifier prevents simultaneous exports from sharing a temporary name.
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            // Attempt this work. A matching catch below handles a reported exception; a finally block, when
            // present, performs cleanup on the way out.
            try
            {
                // using closes the file even if writing throws. UTF8Encoding(false)
                // avoids adding a byte-order marker before the user's displayed text.
                using (var writer = new StreamWriter(temporary, false, new UTF8Encoding(false)))
                    // Call DisplayFormatter.Export: Writes the entire document using the current row
                    // grouping, notation, columns, ruler, and annotations.
                    DisplayFormatter.Export(writer, document, format);
                // Call File.Move(...); the values in parentheses are the inputs.
                File.Move(temporary, path, true);
            }
            // finally runs on success and failure, cleaning up any unfinished temporary file.
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        });
    }
}
