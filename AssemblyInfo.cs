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

// Make names from System.Windows available here without writing their full prefix each time. This does not
// run that library's code.
using System.Windows;
// Make names from System.Runtime.CompilerServices available here without writing their full prefix each
// time. This does not run that library's code.
using System.Runtime.CompilerServices;

// An assembly is a compiled .dll or .exe. Attributes beginning with "assembly:"
// describe this whole compiled project rather than one class. Allow our tests to
// inspect internal helpers (such as layout) without making those helpers public.
[assembly: InternalsVisibleTo("BitExplorer.WpfTests")]

// WPF searches these locations for fallback styles when a resource was not found
// on the control or in App.xaml. We do not ship separate theme-specific dictionaries.
[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,            //where theme specific resource dictionaries are located
                                                //(used if a resource is not found in the page,
                                                // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly   //where the generic resource dictionary is located
                                                //(used if a resource is not found in the page,
                                                // app, or any theme specific resource dictionaries)
)]
