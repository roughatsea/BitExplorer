using BitExplorer.Core;
using WpfApp1.Infrastructure;

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
    private int _position;
    private bool _isSet;
    /// <summary>Creates a button for one fixed position with its flip action and availability rule.</summary>
    public BitItemViewModel(int physicalPosition, Action flip, Func<bool> canFlip)
    {
        _physicalPosition = physicalPosition;
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
        _position = numbering == BitNumbering.LsbZero ? 7 - _physicalPosition : _physicalPosition;
        // Shift the requested bit into the rightmost position, then mask with 1
        // to discard all other bits. Position 0 therefore tests the byte's 128 bit.
        _isSet = ((value >> (7 - _physicalPosition)) & 1) == 1;
        OnPropertyChanged(null);
        FlipCommand.RaiseCanExecuteChanged();
    }
}
