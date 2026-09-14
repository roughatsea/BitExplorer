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

// Make names from System.Configuration available here without writing their full prefix each time. This
// does not run that library's code.
using System.Configuration;
// Make names from System.Data available here without writing their full prefix each time. This does not run
// that library's code.
using System.Data;
// Make names from System.Windows available here without writing their full prefix each time. This does not
// run that library's code.
using System.Windows;

// Put App in the WpfApp1 naming group. Here the group uses braces rather than
// the semicolon form used in other files; its members are written inside them.
namespace WpfApp1
{
    /// <summary>
    /// The application's lifetime object. WPF generates the startup entry point
    /// from App.xaml, loads its shared resources, and opens the StartupUri window.
    /// This class is empty because no extra startup work is needed here.
    /// "partial" joins this hand-written piece with the code generated from XAML.
    /// </summary>
    public partial class App : Application
    {
    }

}
