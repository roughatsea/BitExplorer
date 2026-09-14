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

// Make names from System.Windows.Controls available here without writing their full prefix each time. This
// does not run that library's code.
using System.Windows.Controls;

// Place this file's definitions in the WpfApp1.Views naming group, which prevents clashes with names in
// other groups.
namespace WpfApp1.Views;

/// <summary>
/// Reusable display-options panel. Its XAML bindings send values and commands
/// directly to DisplaySettingsViewModel, so no event-handling code is needed here.
/// UserControl means this is a piece of a window, not a separate desktop window.
/// </summary>
public partial class DisplayOptionsView : UserControl
{
    /// <summary>Creates the controls declared in the matching XAML file.</summary>
    public DisplayOptionsView() => InitializeComponent();
}
