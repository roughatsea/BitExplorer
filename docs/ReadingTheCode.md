# Reading Bit Explorer's code

This guide is a starting point for someone who has written little code. The comments in the source files explain individual methods and calculations; this guide connects those pieces into a working application. You do not need to read every file before understanding the main flow.

## 1. The data we are exploring

A **bit** is a value of either zero or one. A **byte** holds eight bits and can represent an unsigned whole number from 0 to 255. “Unsigned” means that negative numbers are not included in that interpretation.

Binary, decimal, and hexadecimal are different ways to write the same number:

| Representation | Example | Meaning |
| --- | --- | --- |
| Binary, base 2 | `10110011` | Eight bit values, with the highest weight at the left |
| Decimal, base 10 | `179` | The familiar everyday notation |
| Hexadecimal, base 16 | `B3` or `0xB3` | Two hexadecimal digits represent one byte |

Hex digits continue after 9 with A, B, C, D, E, and F, meaning 10 through 15. Thus `B3` means `11 × 16 + 3`, which is 179. The `0x` prefix identifies hexadecimal notation; it is not part of the number's value.

The bit weights from left to right are 128, 64, 32, 16, 8, 4, 2, and 1. For `10110011`, add the weights where there is a one: `128 + 32 + 16 + 2 + 1 = 179`. The **most significant bit (MSB)** has the highest weight; the **least significant bit (LSB)** has the lowest.

An **offset** is a position counted from the start of the file, beginning at zero. Byte offset 0 is the first byte. `DocumentModel.Data[1]` reads the second byte: square brackets select an item from an array using its zero-based index.

Bit Explorer needs stable addresses for individual bits. It uses:

```text
physical bit address = byte offset × 8 + position from the left
```

For byte offset 1, the physical bit addresses are 8 through 15. Address 8 is that byte's leftmost, most significant bit. A ruler may label this same bit “7” in LSB-zero mode or “0” in MSB-zero mode. Those labels do not change the physical address, weight, or stored data.

For a concrete packed field, the demo's first two bytes are `B3 6C`:

```text
Byte offset:          0        1
Binary:            10110011 01101100
Physical address:  01234567 8 ... 15

Read addresses 5, 6, 7, 8, 9, 10:
Bits:              0  1  1  0  1  1  = binary 011011 = decimal 27
```

A **field** gives a name and an explicit order to some source bits. The order matters: `[14, 3, 9, 0]` and `[0, 3, 9, 14]` select the same physical locations but assemble different numbers. `BitSelection` records membership and navigation; `NamedField.OrderedBits` records how to assemble a field from most significant to least significant. These are deliberately different responsibilities.

Start reading [Core/Models.cs](../Core/Models.cs), then the `FlipBit`, `ReadField`, and `SetFieldValue` methods in [Core/DocumentModel.cs](../Core/DocumentModel.cs).

## 2. A small C# reading vocabulary

You will meet these forms throughout the project. The inline comments explain the particular reason for using them.

| Form | How to read it |
| --- | --- |
| `class` | A definition describing an object's data and operations. `new` creates an instance. |
| `namespace` / `using` | Organize names and make names from other namespaces convenient to reference. |
| Field, such as `_document` | A value stored inside an object. An underscore is our naming convention, not a C# requirement. |
| Property, such as `Document` | A named value exposed through a getter and sometimes a setter. It may be stored or computed. |
| Method, such as `FlipBit(...)` | A named operation. Its arguments supply inputs; its return type describes the result. `void` means no result. |
| Constructor | A method-like initialization step with the same name as its class. Arguments can also be declared beside the class name. |
| `public`, `private`, `internal` | Accessible from outside, only from the containing type, or from the same compiled assembly, respectively. |
| `readonly` | The field's reference/value can be assigned only in allowed initialization locations. The referenced object may still contain editable data. |
| `var` | The compiler infers the variable's type from its starting value. The variable still has a definite type. |
| `byte`, `int`, `long`, `double`, `bool`, `string` | An eight-bit unsigned number, 32-bit integer, 64-bit integer, floating-point number, true/false value, and text. |
| `BigInteger` | A whole number that can grow beyond 64 bits, used for wide custom fields. |
| `enum` | A named set of choices, such as Bytes/Bits or Hexadecimal/Decimal. |
| `List<T>`, `Dictionary<K,V>`, `Stack<T>` | A sequence, a lookup by key, and a last-in-first-out collection. `T`, `K`, and `V` stand for supplied types. |
| `=>` | Either a short expression-bodied member or an unnamed function (lambda), depending on its position. |
| `() => SaveProject()` | A function we can call later. Constructing this function does not save a project immediately. |
| `is { } value` | Checks that something is not null and gives it a local name. Other `is` patterns can check type and properties. |
| `string?` / `object?` | The reference is allowed to be absent (`null`). |
| `a?.Method()` | Call only if `a` is not null. |
| `a ?? fallback` | Use `a` if it is not null; otherwise use `fallback`. |
| `value!` | Tells the compiler the programmer believes this value is not null. It does not perform a runtime check. |
| `condition ? a : b` | Choose `a` if the condition is true, otherwise `b`. |
| `$"Offset {offset:X8}"` | Insert a value into text; here format it in hex with at least eight digits. |
| `nameof(Property)` | Produce the name as text while letting the compiler check the identifier. |
| `using var item = ...` | Dispose the item when the current scope ends, useful for files, listeners, and subscriptions. |

An **interface** describes what operations an object must offer. `IUserInteractionService` describes prompts and messages. The real implementation shows Windows dialogs; the test implementation returns prepared answers. A view model can use either because both satisfy the same interface.

`Select`, `Where`, `Order`, `GroupBy`, and `SelectMany` are LINQ operations for transforming sequences. Read a chain from left to right: filter, group, sort, or convert its items. Many chains describe work that runs when the sequence is enumerated; `ToList()` and `ToArray()` collect results immediately. `Select` in this sense transforms items and does not mean “highlight in the UI.”

Bit operations work on individual binary digits. `<<` and `>>` shift left or right; `&` keeps bits set in both operands; `|` combines set bits; `^` flips where the other operand has a one; `~` inverts bits. A **mask** is a number chosen to identify the bit positions we want to change or inspect. Comments beside these operations show how neighboring bits are preserved.

## 3. What each part of the application owns

WPF (Windows Presentation Foundation) supplies the window, controls, layout, input events, and drawing APIs. C# implements behavior. XAML is a markup language that describes the control tree and its properties.

The project uses **Model–View–ViewModel (MVVM)**. The model owns data rules. The view describes controls. The view model exposes values and actions suitable for the view. This lets us test most behavior without showing a desktop window.

| Read this | Responsibility |
| --- | --- |
| [App.xaml](../App.xaml) and [App.xaml.cs](../App.xaml.cs) | Application startup and shared styles/colors. |
| [MainWindow.xaml](../MainWindow.xaml) and [MainWindow.xaml.cs](../MainWindow.xaml.cs) | Assemble the window, connect the shared grid objects, route focus/shortcuts, and handle window lifetime. |
| [ViewModels/MainWindowViewModel.cs](../ViewModels/MainWindowViewModel.cs) | Compose the panels and expose application commands such as Open, Undo, and Copy. |
| [ViewModels/ExplorerSession.cs](../ViewModels/ExplorerSession.cs) | Own the current document, selected addresses, selected field, interpretation order, and busy state. |
| [ViewModels/InspectorViewModel.cs](../ViewModels/InspectorViewModel.cs) | Convert selected data into readouts and validate byte/field input. |
| [ViewModels/FieldsViewModel.cs](../ViewModels/FieldsViewModel.cs) | Create, edit, and delete labels; maintain their list items. |
| [ViewModels/DisplaySettingsViewModel.cs](../ViewModels/DisplaySettingsViewModel.cs) | Validate display preferences and column-width drafts. |
| [Views/](../Views) | XAML and small control-specific adapters for those panels. |
| [Core/](../Core) | Binary/field edits, undo/redo, persistence, physical selection, and shared text formatting. This project does not depend on WPF. |
| [Controls/](../Controls) | Draw the custom byte grid and translate mouse positions into data addresses. |
| [Services/](../Services) | Coordinate file workflows and adapt application requests to file I/O, dialogs, and the clipboard. |
| [Infrastructure/](../Infrastructure) | Small reusable helpers for property notifications and commands. |
| [Dialogs/](../Dialogs) | Construct text and field-editing windows and validate their input. |

## 4. Follow a click from the screen to the data

Start with [MainWindow.xaml.cs](../MainWindow.xaml.cs). Its constructor calls `InitializeComponent()`, which WPF generates from XAML. It then assigns a view model as the window's **DataContext**, the source object for bindings such as `{Binding DocumentTitle}`.

The window gives `ByteGrid` references to the session's actual document and selection. These references point to the same objects used by the panels, rather than separate copies that must be manually synchronized.

When you click a byte:

1. [ByteGrid.OnMouseLeftButtonDown](../Controls/ByteGrid.cs) receives WPF's mouse event.
2. [ByteGridLayout.HitData](../Controls/ByteGridLayout.cs) converts the click's screen location, current scroll offsets, and row/column geometry into a physical bit address. In byte mode, the gesture selects the whole byte.
3. [BitSelection.SelectAt](../Core/BitSelection.cs) applies normal, Shift, or Ctrl selection rules and raises `Changed`.
4. [ExplorerSession.SelectionChanged](../ViewModels/ExplorerSession.cs) leaves any previously selected label's context and announces the updated session state.
5. [InspectorViewModel.Refresh](../ViewModels/InspectorViewModel.cs) rereads the active byte and interpretation. Other panel view models refresh their relevant values too.
6. Property-change notifications tell WPF bindings to reread those values. The grid separately requests a redraw and the window keeps the active byte visible.

An **event** is a notification to subscribed functions. `publisher.Changed += Handler` subscribes; `-= Handler` removes the subscription. `Changed?.Invoke(...)` announces the event if it has listeners. Notifications do not automatically mean background threads: ordinary subscribers run as part of the call unless code explicitly schedules them elsewhere.

Selecting a named field follows a slightly different route. `ExplorerSession.SelectField` both updates highlighted membership and preserves the field's saved assembly order. Its `_updatingSelection` guard prevents that internal update from being mistaken for a fresh raw grid selection, which would clear the field context. The `finally` block releases the guard even if an exception occurs.

## 5. Follow an edit and its undo

Typing into the byte editor changes `InspectorViewModel.ByteInput`, a **draft string**. It does not immediately change the file. The Apply button and Enter key are bound to the same command. A **command** packages an action with an availability check: WPF uses `CanExecute` to enable the control and `Execute` to perform the action.

`ApplyByte` parses the draft, checks that it fits in a byte, then calls `DocumentModel.SetByte`. The core records the old and new values as one undoable edit and raises its change event. The inspector, labels, grid, and save indicator then refresh through their subscriptions.

Clicking an inspector bit instead calls `FlipBit`. For example, the demo byte `B3` is `10110011`. Flipping its third bit from the left uses mask `00100000` (32). XOR produces `10010011`, or `93` hex / 147 decimal. See this example in [InspectorCommands](../Tests.Wpf/PanelTests.cs).

Undo uses a stack: the most recent edit is reversed first. Redo reapplies the most recently undone edit. A new edit clears the redo path, because there is now a different history after that point. Label edits also use history; deleting a label removes metadata without deleting its source bytes.

The **dirty state** means that the current history/settings differ from a saved baseline. Binary and project saving have different baselines because a binary file cannot store field names or display settings. Modified-byte highlighting compares actual byte values with a byte snapshot. These mechanisms are related but not identical; manually typing old values can create another history revision even when those bytes now match the snapshot.

Draft preservation is another reason to keep state separate. A ruler toggle should refresh labels without replacing half-typed text such as `0x`. The inspector compares its current edit target with a previous snapshot before resetting a draft. A changed target, byte value, relevant notation, or field mapping may require a fresh draft; an unrelated notification does not.

## 6. Why the grid has several classes

The file can contain far more rows than fit on the screen. `ByteGridRenderer` paints the visible rows instead of constructing a control for every byte. This is **virtualization**. The visible rectangle is the **viewport**; the full scrollable size is the **extent**.

`ByteGridLayout` provides both drawing coordinates and hit testing. If drawing and click handling used different geometry, the byte highlighted by a click could differ from the one under the pointer. Content coordinates include the offscreen area; viewport coordinates describe what you currently see. Horizontal scrolling subtracts an offset when drawing and adds it when interpreting a click.

Column widths are converted from WPF screen units into whole monospaced character positions. [DisplayFormatter](../Core/DisplayFormatter.cs) supplies common column-sizing and text rules so export spacing matches the grid. Export loops through the entire file; the renderer paints only the current viewport. Colors, selection highlights, and inspector controls are not part of the exported text.

`ByteGrid` itself handles WPF lifecycle and gestures. `ByteGridTooltip` supplies hover descriptions. Keeping these responsibilities separate lets you study one task without first understanding every drawing and input detail.

## 7. What happens while opening or saving

[DocumentWorkflow](../Services/DocumentWorkflow.cs) coordinates the steps. A dialog service asks for a path or a save decision. `WorkspaceFiles.OpenAsync` reads on a worker thread; `await` lets the UI continue handling messages while the task is pending. Await does not itself create a background thread.

The workflow sets the shared busy flag so another command cannot edit/export the changing document at the same time. A failed open leaves the existing document in place. A successful open replaces it only after reading and validation finish. `finally` resets the normal busy state after success or failure. Disposal guards prevent late file-operation completions from updating a session that has already been closed.

A **snapshot** is a copy taken at a particular moment. Export snapshots display settings so the whole output uses one format; application commands prevent document edits during export. A **temporary file** allows the complete result to be written before it replaces the destination. This avoids exposing half-written output from the writing phase; it is not a promise against every possible disk or power failure.

`try` encloses work that may fail. `catch` handles an exception, such as invalid input or a missing file. `finally` performs cleanup regardless of the outcome. `Dispose` releases resources or subscriptions once their owner is finished with them; it is different from deleting the user's file.

## 8. Use the tests as worked examples

[Tests/Program.cs](../Tests/Program.cs) and [Tests/BitSelectionTests.cs](../Tests/BitSelectionTests.cs) exercise the core without WPF. [Tests.Wpf/](../Tests.Wpf) tests view models, geometry, file workflows, and real compiled bindings.

Each scenario generally **arranges** starting data, **acts** by calling a command or method, and **checks** the outcome. `Equal(expected, actual)` checks one value. `Sequence(expected, actual)` checks a sequence in order. A failed check throws an exception, which the harness reports as a FAIL line. The process returns a nonzero exit code if any scenario fails.

The WPF tests inject [FakeUserInteractionService](../Tests.Wpf/FakeUserInteractionService.cs), so they can model cancelling a dialog or entering text without showing real dialogs. `ControlledFiles` deliberately leaves an open operation pending until the test supplies its result or error. That makes busy-state and disposal scenarios repeatable rather than dependent on disk speed.

The compiled-binding test creates and lays out an unshown window. It verifies binding paths, shared references, text input, selection, and command connections while collecting WPF binding errors. This is useful coverage, but it does not replace checking the look and feel of a visible window.

## 9. A manageable reading route

Read the high-level summary at the top of a class, then choose one method tied to a behavior you recognize. Follow the method calls only as far as you need for that behavior. The source comments explain calculations and constraints beside the relevant code.

For a first pass, use this order: `Models.cs` → `DocumentModel.FlipBit` → `BitSelection` → `ExplorerSession` → `InspectorViewModel` → `InspectorView.xaml` → `MainWindow.xaml.cs`. Read `ObservableObject` and `RelayCommand` when you encounter notifications and commands. Move on to drawing, persistence, and tests once those relationships make sense.

The project files (`.csproj`) explain which code is compiled together. The solution (`.slnx`) groups projects for development. WPF generates some code under `obj/`, and builds executables under `bin/`; those generated folders are not source files to edit. Comments beginning with `///` describe C# members and may appear in editor tooltips; `//` comments explain nearby steps. XAML uses `<!-- ... -->` comments.
