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
/// A command for work that may finish later, such as opening or exporting a file.
/// A Task represents that work. Awaiting it lets the UI thread process messages
/// while the underlying operation is pending, rather than blocking the window.
/// </summary>
/// <param name="execute">The asynchronous operation, which returns its completion Task.</param>
/// <param name="canExecute">An optional application-state check, such as "the session is not busy."</param>
/// <remarks>
/// Prevents a second execution of this command until the first ends. It does not
/// start a background thread itself: WorkspaceFiles moves file I/O off the UI thread.
/// Application-level wrappers handle errors from the supplied operation.
/// </remarks>
public sealed class AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null) : ICommand
{
    // This flag guards this one command; session.IsBusy coordinates different commands.
    private bool _executing;
    /// <summary>Tells buttons to refresh their enabled state when execution begins or ends.</summary>
    public event EventHandler? CanExecuteChanged;
    /// <summary>Requires an idle command and permission from its optional state check.</summary>
    public bool CanExecute(object? parameter = null) => !_executing && (canExecute?.Invoke() ?? true);
    /// <summary>
    /// WPF requires a void method. This async-void bridge delegates to the Task-returning
    /// version that application code and tests can await to observe completion.
    /// </summary>
    public async void Execute(object? parameter = null) => await ExecuteAsync(parameter);
    /// <summary>Runs one operation and always releases the guard, even on failure.</summary>
    public async Task ExecuteAsync(object? parameter = null)
    {
        // If it is not the case that CanExecute(parameter) is true, leave this method immediately.
        if (!CanExecute(parameter)) return;
        // Set _executing to true (yes).
        _executing = true;
        // Ask controls using to check again whether the command is allowed to run.
        RaiseCanExecuteChanged();
        // finally runs after success or an exception; a failure must not leave
        // this command permanently disabled.
        try { await execute(); }
        // Run this cleanup when leaving the try, whether the work succeeded or reported an exception.
        finally { _executing = false; RaiseCanExecuteChanged(); }
    }
    /// <summary>Requests an availability check; this does not execute the command.</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
