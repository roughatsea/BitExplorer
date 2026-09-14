# Bit Explorer

A native WPF explorer and editor for custom binary formats, built with C# and .NET 10. No external packages or services are needed. Open the solution in Visual Studio, or run:

```powershell
dotnet build WpfApp1.csproj
dotnet run --project WpfApp1.csproj
```

The executable is `bin/Debug/net10.0-windows/BitExplorer.exe`. A binary or `.bitexplorer` project path can also be supplied as its first argument. The app starts with an annotated example packet.

## Exploring and editing

- Switch between bytes and bits without losing the selection. Hexadecimal and decimal byte values, offsets, and conversion tooltips are available. The inspector always shows both bases.
- Click data or ASCII to inspect it. Shift-click or drag selects a range; Ctrl-click combines separated ranges. Arrows navigate and Shift+arrows extends the selection.
- Double-click a byte or press Enter to edit it in the inspector. Click an inspector bit to flip it, or press Space on a single selected grid bit. All byte edits and field changes support undo/redo.
- The bit ruler can number the least significant bit as zero or the most significant bit as zero. This changes labels only; the stored bits and their significance do not change.
- Selection interpretation supports file order, reversed byte order, and an explicitly saved field mapping. Byte MSB/LSB indicators are shown for whole-byte values; packed fields show their own most/least significant source bits.

## Named fields

Select bits and choose **Label selection**. Fields have a name, color, notes, and an ordered source-bit map. They can cross byte boundaries or combine separated pieces. Edit a field's numeric value to update only its source bits.

Select an existing label in **Named fields**, then use **Edit label…** or **Delete label** beneath the list. Double-click or press **F2** to edit, or right-click a label for both actions. **Delete** removes the selected label while the list has focus. Deleting a label preserves the bytes; **Ctrl+Z** restores the label and its full definition.

Mapping addresses are absolute physical positions: position `0` is the MS bit of byte 0, and position `8` is the MS bit of byte 1. A mapping such as `5-10, 24-27, 20` assembles those source bits in that order, from the field's MSB to LSB. Descending ranges are allowed. These addresses stay stable across ruler conventions. The field editor previews its endpoints and can reorder by file position, reverse byte groups, or reverse all bits.

**More → Export / Import field template** saves or applies reusable `.bitfields` definitions. Import replaces the current field layout and can be undone; it does not change binary data.

## Layout, saving, and exporting

Drag header dividers or use **Display options** to configure offset, data, and ASCII column widths. Fit column adapts bytes per row to the data column. With a fixed row count, columns retain a minimum width sufficient to display the complete data, with horizontal scrolling as needed. Widths snap to monospaced character positions so export spacing matches the grid.

- **Save binary** writes edited bytes only.
- **Save project** creates a versioned `.bitexplorer` JSON file containing edited bytes, fields, notes, and display settings.
- **Export text** writes the entire file in the current main-grid format: notation, visible columns, ruler, row grouping, spacing, and field annotation rows. It excludes inspector controls, colors, and selection highlights. Annotation summaries use the same truncation on screen and in text; full definitions remain available in the inspector and project.

Projects embed their binary data and can be reopened independently of the original file. Text export is UTF-8 without a byte-order mark. Files are saved through a temporary file to avoid leaving partial output.

## Shortcuts

| Action | Shortcut |
| --- | --- |
| Open | Ctrl+O |
| Save binary | Ctrl+S |
| Save project | Ctrl+Shift+S |
| Export entire formatted file | Ctrl+E |
| Go to byte offset | Ctrl+G |
| Copy selected data | Ctrl+C |
| Undo / redo | Ctrl+Z / Ctrl+Y |

## Limits and verification

The grid renders only visible rows. Binary documents are held in memory and support files up to 256 MiB; interactive selection supports up to 8 KiB at once. Numeric fields support up to 4096 bits, with arbitrary-precision integer interpretation. Editing overwrites bytes and bits; insertion/deletion and automatic schema discovery are not implemented.

Run the dependency-free regression harness with:

```powershell
dotnet run --project Tests/BitExplorer.Tests.csproj
dotnet run --project Tests.Wpf/BitExplorer.WpfTests.csproj -c Release
```

The core checks cover field interpretation, preservation of neighboring bits, undo/redo, persistence validation, exact byte/bit export formatting, and bounded selection/navigation. The WPF harness exercises panel commands, drafts, field mapping changes, layout/hit testing, asynchronous workflows, and compiled bindings without opening desktop windows.

## Code organization

Never programmed before? Start with [Reading the code](docs/ReadingTheCode.md). It begins with names, values, decisions, and the difference between defining an operation and running it, then follows selection, editing, undo, rendering, and file operations through the application. The C#, XAML, project files, and tests contain instruction-level explanations. Comments cover each meaningful instruction or its clearly introduced block; punctuation and continuation lines belong to that same explanation.

The desktop UI uses composed MVVM: each panel has its own view and view model, and all panels share one exploration session.

- `Core/` contains binary editing, persistence, display formatting, and `BitSelection`. It has no WPF dependency. Selection membership and navigation anchors are separate from the ordered bits used to interpret a named field.
- `Controls/ByteGrid.cs` adapts WPF input, scrolling, and lifecycle. `ByteGridLayout` owns geometry and hit testing; `ByteGridRenderer` draws visible rows; `ByteGridTooltip` formats hover details. Drawing and hit testing use the same layout, with column sizing shared with text export.
- `ViewModels/ExplorerSession.cs` owns the current document, selection, selected field, and interpretation order. `MainWindowViewModel` composes the inspector, fields, and display settings view models and exposes application commands.
- `Views/` contains the fields, inspector, and display options panels. Bindings update their contents; small code-behind adapters handle focus and pointer/key gestures. `MainWindow.xaml.cs` only connects the shared grid session and handles window lifecycle, focus, shortcuts, and file drops.
- `Services/DocumentWorkflow.cs` coordinates opening, saving, exporting, and save decisions. `IUserInteractionService` isolates native dialogs and the clipboard; `IWorkspaceFiles` isolates background file operations. Tests provide fake implementations.
- `Infrastructure/` provides small observable-object and command helpers, without an external MVVM framework.

Keep new interpretation and editing behavior in the core or focused view models. Keep WPF control manipulation in views, and keep platform operations behind services. A file split alone is not a new responsibility boundary.
