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

// Make names from BitExplorer.Core available here without writing their full prefix each time. This does
// not run that library's code.
using BitExplorer.Core;
// Make names from WpfApp1.Infrastructure available here without writing their full prefix each time. This
// does not run that library's code.
using WpfApp1.Infrastructure;

// Place this file's definitions in the WpfApp1.ViewModels naming group, which prevents clashes with names
// in other groups.
namespace WpfApp1.ViewModels;

/// <summary>
/// Bound values for one inspector bit button. It exposes simple text and booleans;
/// XAML decides colors and layout, so this class does not need WPF control objects.
/// </summary>
public sealed class BitItemViewModel : ObservableObject
{
    // Physical position is always 0 at the byte's high bit and 7 at its low bit.
    // Displayed numbering can change independently, for example from 7→0 to 0→7.
    private readonly int _physicalPosition;
    // Reserve _position to hold a whole number; setup can supply its value, otherwise the type's default is
    // used.
    private int _position;
    // Reserve _isSet to hold a true-or-false answer; setup can supply its value, otherwise the type's
    // default is used.
    private bool _isSet;
    /// <summary>Creates a button for one fixed position with its flip action and availability rule.</summary>
    public BitItemViewModel(int physicalPosition, Action flip, Func<bool> canFlip)
    {
        // Set _physicalPosition to physicalPosition.
        _physicalPosition = physicalPosition;
        // Prepare FlipCommand for later activation: run flip. It can run when the shared command rules
        // permit it. Creating the command here does not perform its action.
        FlipCommand = new RelayCommand(flip, canFlip);
    }
    /// <summary>The ruler number above the button, according to the chosen bit-numbering convention.</summary>
    public string Label => _position.ToString();
    /// <summary>The binary digit printed on the button.</summary>
    public string Value => _isSet ? "1" : "0";
    /// <summary>Lets XAML visually distinguish set bits from clear bits.</summary>
    public bool IsSet => _isSet;
    /// <summary>Explains the bit's number, numeric weight and significance when hovered.</summary>
    public string ToolTip => $"Bit {_position} · weight {1 << (7 - _physicalPosition)} · " +
        (_physicalPosition == 0 ? "most significant bit" : _physicalPosition == 7 ? "least significant bit" : "click to flip");
    /// <summary>A descriptive label for screen readers and UI automation, rather than only a 0 or 1.</summary>
    public string AutomationName => $"Flip bit {_position}, value {Value}";
    /// <summary>The action bound to the button; its caller controls whether a valid byte can be edited.</summary>
    public RelayCommand FlipCommand { get; }

    /// <summary>Refreshes the button from the active byte and asks bindings to reread its text and state.</summary>
    internal void Update(byte value, BitNumbering numbering)
    {
        // Set _position to 7 minus _physicalPosition when numbering equals BitNumbering.LsbZero; otherwise
        // _physicalPosition.
        _position = numbering == BitNumbering.LsbZero ? 7 - _physicalPosition : _physicalPosition;
        // Shift the requested bit into the rightmost position, then mask with 1
        // to discard all other bits. Position 0 therefore tests the byte's 128 bit.
        _isSet = ((value >> (7 - _physicalPosition)) & 1) == 1;
        // Tell the screen that all properties may have changed and should be reread.
        OnPropertyChanged(null);
        // Ask controls using FlipCommand to check again whether the command is allowed to run.
        FlipCommand.RaiseCanExecuteChanged();
    }
}
