# BitExplorer data model

This package-free .NET 10 library owns binary edits, named bit fields, history,
project/template persistence, and the text representation shared with the WPF grid.

## Bit addresses and values

A physical bit address is `byteOffset * 8 + bitFromLeft`. Physical bit zero is
the most significant bit of byte zero. The visible bit-numbering convention
changes labels only. `NamedField.OrderedBits` explicitly lists source bits in
the interpreted value's **most significant to least significant** order.
Lists may cross byte boundaries or contain separated ranges. For example,
bytes `B3 6C` at physical bits `5,6,7,8,9,10` produce `011011`, decimal 27.
A little-endian word at byte offsets 4–5 uses physical bits 40–47 followed by
32–39. Numbering, physical position, and numeric significance are independent.

Use `SetByte`, `FlipBit`, and `SetFieldValue` to edit data. Use `AddField`,
`UpdateField`, and `RemoveField` to edit annotations. Each operation is one undo
step. Clone an existing field before updating it; do not mutate the field object
in `Fields` directly. A temporary field need not be registered to read or edit a
selection. All value writes preserve bits outside the supplied field.

`BitSelection` holds source-bit membership and its active/anchor positions. Its
read-only live `Bits` collection is sorted by physical address; it is never used
as a substitute for a field's assembly order. `SelectAt`, `SelectRange`, and
`Navigate` handle byte/bit selection, extension, toggling, and navigation. A change
that would exceed 65,536 selected bits raises `SelectionLimitReached` and leaves
the entire previous selection, including its anchors, intact. Document length
changes trim out-of-range bits. The desktop session shares this instance with
the grid and panels.

## Persistence

Binary saves write only edited bytes. Version 1 project JSON embeds the bytes,
the display settings, and the field layout; it never executes or automatically
reads its stored source path. Template JSON contains only fields. Importing a
template replaces the current field layout in one undoable operation.
Writes use a temporary file in the destination directory followed by a replace
or move. Failed validation or writes leave the existing destination intact.

`IsDirty` follows the active project, when present. Without a project, it reports
unsaved binary or metadata changes. `IsBinaryDirty` reports edits since the last
binary save; saving a project can clear `IsDirty` while retaining that flag.
`IsByteModified` compares against the bytes present on binary open/last binary
save. Assign settings through `Settings` and call `NotifySettingsChanged` after
changes to refresh listeners. Display settings are saved with the project.

Limits: 256 MiB binary data, 4,096 fields, 4,096 bits per field, and 1,048,576
total field bit references. Field addresses must be unique within each field
and lie within the document. Different fields may overlap.

## Formatted export

`DisplayFormatter.Export` streams every row using the active notation, byte
grouping, ruler, column visibility, and field annotation settings. Decimal byte
values have three digits, hexadecimal values have two. Binary byte groups keep
their physical order under either bit numbering convention. Nonprintable ASCII
bytes use a period. Incomplete final rows receive data-column padding when the
ASCII column is shown. The caller chooses the text encoding and newline style.

The grid records its measured monospace glyph advance in `CharacterWidth`.
`GetColumnCharacterWidths` converts resizable pixel columns into the same whole
character-cell widths for both rendering and export, with two-character gutters
at each edge. Fixed row sizes impose minimum column widths so bytes are never
silently clipped. Export preserves the two-character leading gutter and spacing
between columns; unnecessary trailing spaces after the final column are omitted.
When annotations are displayed and the document has fields, every data row has
a second annotation line, including an empty line when no field touches that row.
Summaries longer than the data column are abbreviated with an ellipsis in both
the grid and the export. Full field names remain available in the inspector and
project/template files. Character-width calibration alone does not mark a file
dirty.
