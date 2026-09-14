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

// Make names from System.ComponentModel available here without writing their full prefix each time. This
// does not run that library's code.
using System.ComponentModel;
// Make names from System.Runtime.CompilerServices available here without writing their full prefix each
// time. This does not run that library's code.
using System.Runtime.CompilerServices;

// Place this file's definitions in the WpfApp1.Infrastructure naming group, which prevents clashes with
// names in other groups.
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
        // Replace the caller's stored value. The ref keyword in the parameter list
        // makes storage refer to the caller's actual location, not a separate copy.
        storage = value;
        // Announce which named property changed. For example, "ByteInput" tells
        // a text-box binding to read ByteInput again and show the new text.
        OnPropertyChanged(propertyName);
        // Answer yes: a replacement happened. A caller can use this answer to
        // perform related work without doing it for an unchanged value.
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
        // Take each item from propertyNames in turn, call the current item name, and tell the screen that
        // name should be reread.
        foreach (string name in propertyNames) OnPropertyChanged(name);
    }
}
