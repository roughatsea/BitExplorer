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

// Place this file's definitions in the WpfApp1.Services naming group, which prevents clashes with names in
// other groups.
namespace WpfApp1.Services;

/// <summary>Keeps the user-facing help text separate from commands and view layout.</summary>
internal static class ExplorerHelp
{
    // A raw string literal preserves the paragraphs without escaped newline characters.
    internal const string Text = """
        Select: click a byte, bit, or ASCII character. Shift-click extends the range; Ctrl-click combines separated bits. Drag to select. Arrow keys move; Shift+arrows extend.

        Edit: double-click a byte or press Enter, then type in the inspector. Click a bit to flip it. Space flips a single selected grid bit. Ctrl+Z / Ctrl+Y undo / redo.

        Fields: Label selection creates a named source-bit map. Physical position 0 is byte 0’s MS bit, independent of the ruler convention. Mappings may contain separated or reversed pieces. Double-click a label or press F2 to edit. Delete removes a label while the list has focus; Ctrl+Z restores it.

        Ctrl+O opens a file. Ctrl+S saves binary. Ctrl+Shift+S saves a project. Ctrl+E exports the entire formatted file. Ctrl+G jumps to an offset. Ctrl+C copies selected data.

        Save project preserves bytes, labels, notes, and display settings. Binary save writes bytes only. Field layouts can be reused with other files.

        The grid is virtualized. Files up to 256 MiB, selections up to 8 KiB, and numeric fields up to 4096 bits are supported.
        """;
}
