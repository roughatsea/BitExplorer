using BitExplorer.Core;
using WpfApp1.Infrastructure;

namespace WpfApp1.ViewModels;

public sealed class BitItemViewModel : ObservableObject
{
    private readonly int _physicalPosition;
    private int _position;
    private bool _isSet;
    public BitItemViewModel(int physicalPosition, Action flip, Func<bool> canFlip)
    {
        _physicalPosition = physicalPosition;
        FlipCommand = new RelayCommand(flip, canFlip);
    }
    public string Label => _position.ToString();
    public string Value => _isSet ? "1" : "0";
    public bool IsSet => _isSet;
    public string ToolTip => $"Bit {_position} · weight {1 << (7 - _physicalPosition)} · " +
        (_physicalPosition == 0 ? "most significant bit" : _physicalPosition == 7 ? "least significant bit" : "click to flip");
    public string AutomationName => $"Flip bit {_position}, value {Value}";
    public RelayCommand FlipCommand { get; }

    internal void Update(byte value, BitNumbering numbering)
    {
        _position = numbering == BitNumbering.LsbZero ? 7 - _physicalPosition : _physicalPosition;
        _isSet = ((value >> (7 - _physicalPosition)) & 1) == 1;
        OnPropertyChanged(null);
        FlipCommand.RaiseCanExecuteChanged();
    }
}
