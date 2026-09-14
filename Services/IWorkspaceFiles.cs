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
