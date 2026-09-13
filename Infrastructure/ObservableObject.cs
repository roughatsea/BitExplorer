using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfApp1.Infrastructure;

/// <summary>
/// Base class for objects whose properties appear on screen. WPF reads a property
/// through a binding, but needs a notification to know when to read it again.
/// INotifyPropertyChanged is the standard .NET contract for that notification.
/// "Abstract" means this is shared support code, not an object we create directly.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    /// <summary>Tells subscribers, including WPF, which property needs rereading.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Replaces a stored value only when it differs, then notifies listeners.
    /// Returns true when a change was made, so a caller can perform related work.
    /// </summary>
    /// <typeparam name="T">The value's type; the helper works for strings, numbers, and other types.</typeparam>
    /// <param name="storage">The backing field, passed by reference so this method can replace it.</param>
    /// <param name="value">The proposed new value.</param>
    /// <param name="propertyName">Normally supplied by the compiler from the calling property's name.</param>
    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        // EqualityComparer chooses equality appropriate to T. Avoid needless
        // refreshes when a binding sends the value we already have back to us.
        if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Announces a changed property. Passing null explicitly means "reread all properties."
    /// The ?. invocation does nothing when nobody has subscribed yet.
    /// </summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// Announces several related changes, such as title and save-state text.
    /// "params" lets callers supply names separately instead of constructing an array.
    /// </summary>
    protected void RaiseProperties(params string[] propertyNames)
    {
        foreach (string name in propertyNames) OnPropertyChanged(name);
    }
}
