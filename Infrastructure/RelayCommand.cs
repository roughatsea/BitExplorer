using System.Windows.Input;

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
        if (CanExecute(parameter)) execute();
    }
    /// <summary>Call after selection, undo history, or other relevant state changes.</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
