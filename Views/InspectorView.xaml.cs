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
/// Displays interpreted byte/field values using InspectorViewModel bindings.
/// These methods only manipulate visible controls; the view model performs edits.
/// </summary>
public partial class InspectorView : UserControl
{
    /// <summary>Builds the panel from InspectorView.xaml.</summary>
    public InspectorView() => InitializeComponent();

    /// <summary>Reveals the byte input and selects its text, ready for replacement typing.</summary>
    public void FocusByteEditor()
    {
        // The inspector scrolls, so focus alone might land on an offscreen control.
        ByteInput.BringIntoView();
        // Ask WPF to give this control keyboard focus, so keyboard input is directed here.
        ByteInput.Focus();
        // Call ByteInput.SelectAll().
        ByteInput.SelectAll();
    }

    /// <summary>Toggles the collapsible settings section and scrolls it into view on expansion.</summary>
    public void ToggleDisplayOptions()
    {
        // Set DisplayExpander.IsExpanded to the opposite true-or-false answer to
        // DisplayExpander.IsExpanded.
        DisplayExpander.IsExpanded = !DisplayExpander.IsExpanded;
        // If DisplayExpander.IsExpanded is true, call DisplayExpander.BringIntoView().
        if (DisplayExpander.IsExpanded) DisplayExpander.BringIntoView();
    }
}
