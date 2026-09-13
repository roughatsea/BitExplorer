using System.Windows.Input;

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
        if (!CanExecute(parameter)) return;
        _executing = true;
        RaiseCanExecuteChanged();
        // finally runs after success or an exception; a failure must not leave
        // this command permanently disabled.
        try { await execute(); }
        finally { _executing = false; RaiseCanExecuteChanged(); }
    }
    /// <summary>Requests an availability check; this does not execute the command.</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
