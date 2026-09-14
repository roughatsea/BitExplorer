# Reading Bit Explorer's code

This guide assumes you have never programmed. It explains how to read the instructions, then connects them to actions you can try in Bit Explorer. The source comments describe individual instructions and the blocks they belong to. You do not need to read every file before understanding one feature.

## Before you start: what you are looking at

The text in a `.cs` file is **source code**: instructions written for people and development tools to read. A tool called a **compiler** checks those instructions and turns them into a form the computer can run. **Building** the project runs that translation and prepares the application. **Running** starts the resulting application. Reading or editing a file does not by itself run its instructions.

This project uses two main kinds of source files. C# (`.cs`) describes behavior, such as changing a byte. XAML (`.xaml`, pronounced “zammel”) describes the window's controls and their connections, such as which action a button invokes. Both contribute to the same application.

A **comment** is an explanation for the reader, ignored when the program runs. `//` starts a C# comment that continues to the end of its line. `///` is also a comment, formatted so an editor can display it as help. Its `<summary>` and `<param>` tags label the explanation and its inputs. In XAML, a comment starts with `<!--` and ends with `-->`.

The comments in this teaching edition follow a few reading rules:

- Read the comment immediately above an instruction before reading the instruction itself. A comment above a method also explains the purpose of its whole body.
- An instruction can occupy several lines. Its explanation covers those continuation lines, including arguments separated by commas.
- Braces group instructions or starting values. A closing brace finishes the group introduced earlier. It does not need a separate explanation that only says “closing brace.”
- A multiline passage inside quotation marks is application text or test data. Its explanation belongs outside the quotation marks; putting a comment inside would change the actual text.
- Repeated simple operations get short reminders. More involved operations also have explanations of their purpose and concrete examples. You can skip reminders once they become familiar.

### Names, values, and places to remember things

Suppose the code says:

```csharp
// Make a place called offset for a whole number, initially 2.
int offset = 2;
// Make a place called firstBit and store the result of 2 multiplied by 8.
long firstBit = offset * 8;
// Replace the number stored in offset with 3.
offset = 3;
```

After these instructions, `offset` is 3 and `firstBit` is still 16. Assigning a calculated result does not create a live mathematical equation. The computer calculates the right side when that instruction runs, then stores the result on the left. This is why `=` means **assignment**, while `==` asks whether two values are equal.

`int` and `long` are **types**: rules about the kinds of values a named place can hold. Both hold whole numbers; `long` has room for much larger ones. A **variable** is a named place whose value can change. A **local variable** belongs to the operation currently running. A **field** in C# is stored with an object and can survive between calls. This use of “field” is different from a named group of bits in the explorer.

An **object** brings related data and operations together. A **class** describes what such objects contain. `new DocumentModel(bytes)` creates a document object. `document.Data` uses the dot to reach the data belonging to that particular object. `document.SetByte(2, 255)` asks that object to change byte offset 2 to the value 255.

There is an important difference between remembering an object and copying it. `var other = document;` gives the same object another name. Editing through either name reaches the same document. A method such as `Clone()` or `ToArray()` can make a separate copy; comments point out where the application needs that independence. `readonly` on a field prevents replacing its stored reference after initialization, but does not automatically prevent editing the object it refers to.

### Instructions, decisions, and repetition

A **method** is a named group of instructions. Writing its definition is like writing a recipe; **calling** it is asking for that recipe to be followed. A definition lists input names called **parameters**. A call supplies actual **arguments** for those inputs. `return` finishes the current call and may send a result back to whoever called it.

Here is a complete teaching example, separate from the application:

```csharp
// Define an operation named IsByteValue. It accepts a whole number named value
// and promises to return bool: either true (yes) or false (no).
bool IsByteValue(int value)
{
    // A byte cannot hold a negative number or a number above 255.
    // || means OR: either problem is enough to enter this branch.
    if (value < 0 || value > 255)
    {
        // Finish this call now, answering no. Later instructions are skipped.
        return false;
    }
    // Reaching here means neither invalid case occurred. Answer yes.
    return true;
}
```

`IsByteValue(179)` returns `true`; `IsByteValue(256)` returns `false`. The braces after `if` contain the instructions that run only when its condition is true. C# also allows a single instruction without braces: `if (value < 0) return false;` has the same decision-and-early-exit pattern.

`else` introduces the alternative when an `if` condition is false. `&&` means AND: both conditions must hold. `!` before a true-or-false expression reverses its answer. `||` and `&&` can stop checking as soon as the answer is known; for example, `item != null && item.Name == "Version"` does not try to read a name from a missing object.

A **loop** repeats instructions. `for (int bit = 0; bit < 8; bit++)` starts at zero, runs a pass while the number is below eight, and adds one after each pass. Its passes use 0, 1, 2, 3, 4, 5, 6, and 7. `foreach (var bit in bits)` instead visits the items already in `bits`. `break` leaves the loop; `continue` skips the remainder of the current pass. Neither means “close the application.”

A **switch** chooses from several alternatives. Each case names a matching value and the work or result to use. `_` in a switch expression is the catch-all case. A **lambda**, such as `() => SaveProject()`, packages an operation for another part of the program to invoke. Creating a button's command does not immediately save a file; the command retains that operation for later activation.

### Reading a small piece of the real application

In [ObservableObject.cs](../Infrastructure/ObservableObject.cs), the central part of `SetProperty` is:

```csharp
// If the old and proposed values are equal, answer "nothing changed" now.
if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
// Store the proposed value in the original caller's storage.
storage = value;
// Tell listeners which displayed property should be read again.
OnPropertyChanged(propertyName);
// Answer "a change happened" to the caller.
return true;
```

`T` is a placeholder for a type, allowing this same helper to compare text, numbers, and other values. `ref` in the method's parameter list means `storage` refers to the caller's actual storage location, so the assignment updates that location. `propertyName` is text identifying the property that changed. The notification is necessary because storing a new value in memory does not by itself tell a text box to redraw.

### Reading a control description

Here is a shortened version of a control in [DisplayOptionsView.xaml](../Views/DisplayOptionsView.xaml):

```xml
<!-- Make a checkbox labelled ASCII column. Connect its checked state to
     ShowAscii on the panel's view model. The control's normal two-way binding
     also sends a user's check/uncheck back to that property. -->
<CheckBox Content="ASCII column" IsChecked="{Binding ShowAscii}" />
```

`CheckBox` names the kind of control. `Content` and `IsChecked` are **attributes**, written as `name="value"`. `Content` supplies a label. The braces in `{Binding ShowAscii}` ask WPF to connect to a property rather than use those words as literal text. The connection's starting object is the **DataContext**, supplied by the containing view. `/>` finishes an element with no children. A container instead has an opening tag, child elements, and a closing tag such as `</Grid>`.

`{StaticResource MutedBrush}` looks up a reusable named object from the application's resources. `{TemplateBinding Background}` takes a property from the control whose appearance a template describes. A template is a recipe for drawing a control; it is different from the data displayed inside it.

XAML uses screen measurements called **device-independent units**. A width of 100 does not always mean 100 physical monitor pixels, because Windows can scale the interface. A margin is empty space outside a control; padding is empty space inside its border. Row and column numbers start at zero. `Auto` fits the content, while `*` receives a share of the remaining space.

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
| `static` | Belongs to the type itself, rather than to one particular object. `Math.Max(...)` is called this way. |
| `const` | A named value fixed when the code is compiled. |
| `sealed` | Other classes cannot extend this class by inheriting from it. |
| `abstract` | An incomplete/shared definition that cannot itself be created with `new`. |
| `partial` | Several source files contribute to the same type; WPF uses this for your code and its generated code. |
| `override` / `base` | Replace an inherited operation's behavior / reach the inherited implementation. |
| `protected` | Available inside this class and classes derived from it. |
| `get` / `set` / `init` | Read a property / write it / supply it during initialization. `private set` restricts who may write it. |
| `record` | A type that groups related values and provides comparisons based on those values. Its members are not automatically deeply immutable. |
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
| `items[0]`, `items[^1]` | Read the first item or the last item. `^1` counts one place back from the end. |
| `text[2..]`, `text[..3]` | Take text from position two to the end, or from the start up to (but excluding) position three. |
| `(byte)value` | Convert a value to the byte type. This syntax is called a cast; validation must establish that a value fits when required. |
| `checked(...)` | Report overflow rather than silently wrapping a calculation or conversion that is too large for its numeric type. |
| `out var result` | Let the called operation supply an additional result through this named place. |
| `[]`, `new[] { ... }` | An empty collection in context, or a new array initialized from the listed values. Brackets after an existing name instead select an item. |
| `(Offset: 2, Value: 255)` | A tuple: a small package of values, optionally given names. |
| `[STAThread]`, `[CallerMemberName]` | Attributes: instructions to development/runtime tools, rather than ordinary method calls. The former selects the thread model WPF needs; the latter supplies a caller's member name. |
| `null`, `default` | No object/value present; or the default for a type (for example 0 for integers, false for bool, and null for object references). |

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
