namespace WpfApp1.Services;

internal static class ExplorerHelp
{
    internal const string Text = """
        Select: click a byte, bit, or ASCII character. Shift-click extends the range; Ctrl-click combines separated bits. Drag to select. Arrow keys move; Shift+arrows extend.

        Edit: double-click a byte or press Enter, then type in the inspector. Click a bit to flip it. Space flips a single selected grid bit. Ctrl+Z / Ctrl+Y undo / redo.

        Fields: Label selection creates a named source-bit map. Physical position 0 is byte 0’s MS bit, independent of the ruler convention. Mappings may contain separated or reversed pieces. Double-click a label or press F2 to edit. Delete removes a label while the list has focus; Ctrl+Z restores it.

        Ctrl+O opens a file. Ctrl+S saves binary. Ctrl+Shift+S saves a project. Ctrl+E exports the entire formatted file. Ctrl+G jumps to an offset. Ctrl+C copies selected data.

        Save project preserves bytes, labels, notes, and display settings. Binary save writes bytes only. Field layouts can be reused with other files.

        The grid is virtualized. Files up to 256 MiB, selections up to 8 KiB, and numeric fields up to 4096 bits are supported.
        """;
}
