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

// Make names from System.Windows.Input available here without writing their full prefix each time. This
// does not run that library's code.
using System.Windows.Input;

// Place this file's definitions in the WpfApp1.Infrastructure naming group, which prevents clashes with
// names in other groups.
namespace WpfApp1.Infrastructure;

/// <summary>
/// Connects a WPF button or shortcut to an ordinary view-model method.
/// ICommand asks two questions: "is this action allowed?" and "please perform it."
/// </summary>
/// <param name="execute">The work to run; Action is a callable function with no result.</param>
/// <param name="canExecute">An optional function returning true when the action is available.</param>
/// <remarks>
/// Arguments beside the class name use C#'s primary-constructor syntax. Our commands
/// do not need WPF's optional parameter: their supplied functions already know
/// the session and document on which they should operate.
/// </remarks>
public sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    /// <summary>Asks WPF to check again whether bound buttons should be enabled.</summary>
    public event EventHandler? CanExecuteChanged;
    /// <summary>Runs the check, or permits the action if no check was supplied.</summary>
    public bool CanExecute(object? parameter = null) => canExecute?.Invoke() ?? true;
    /// <summary>Checks permission again before running, including direct calls from tests.</summary>
    public void Execute(object? parameter = null)
    {
        // If CanExecute(parameter) is true, call execute().
        if (CanExecute(parameter)) execute();
    }
    /// <summary>Call after selection, undo history, or other relevant state changes.</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
