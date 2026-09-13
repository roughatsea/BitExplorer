using System.Collections.ObjectModel;
using System.Globalization;
using BitExplorer.Core;
using WpfApp1.Infrastructure;
using WpfApp1.Services;

namespace WpfApp1.ViewModels;

/// <summary>Validates display changes and preserves unfinished column-width drafts.</summary>
public sealed class DisplaySettingsViewModel : ObservableObject, IDisposable
{
    private readonly ExplorerSession _session;
    private readonly IUserInteractionService _dialogs;
    private readonly Action<string> _reportStatus;
    private string _offsetWidthInput = "", _dataWidthInput = "", _asciiWidthInput = "";
    private (DocumentModel? Document, double Offset, double Data, double Ascii) _widthSource;

    public DisplaySettingsViewModel(ExplorerSession session, IUserInteractionService dialogs, Action<string> reportStatus)
    {
        _session = session;
        _dialogs = dialogs;
        _reportStatus = reportStatus;
        ApplyWidthsCommand = new RelayCommand(ApplyWidths, () => !session.IsBusy);
        SetBytesViewCommand = new RelayCommand(() => ViewMode = DataViewMode.Bytes, () => !session.IsBusy);
        SetBitsViewCommand = new RelayCommand(() => ViewMode = DataViewMode.Bits, () => !session.IsBusy);
        session.Changed += SessionChanged;
        Refresh();
    }

    private DisplaySettings Settings => _session.Document.Settings;
    public DataViewMode ViewMode { get => Settings.ViewMode; set => Change(s => s.ViewMode, (s, v) => s.ViewMode = v, value); }
    public NumericBase ByteBase { get => Settings.ByteBase; set => Change(s => s.ByteBase, (s, v) => s.ByteBase = v, value); }
    public NumericBase OffsetBase { get => Settings.OffsetBase; set => Change(s => s.OffsetBase, (s, v) => s.OffsetBase = v, value); }
    public BitNumbering BitNumbering { get => Settings.BitNumbering; set => Change(s => s.BitNumbering, (s, v) => s.BitNumbering = v, value); }
    public int ByteBaseIndex { get => (int)ByteBase; set { if (value is 0 or 1) ByteBase = (NumericBase)value; } }
    public int OffsetBaseIndex { get => (int)OffsetBase; set { if (value is 0 or 1) OffsetBase = (NumericBase)value; } }
    public int BitNumberingIndex { get => (int)BitNumbering; set { if (value is 0 or 1) BitNumbering = (BitNumbering)value; } }
    public int BytesPerRow { get => Settings.BytesPerRow; set => Change(s => s.BytesPerRow, (s, v) => s.BytesPerRow = v, value); }
    public ObservableCollection<int> RowSizes { get; } = [4, 8, 16, 24, 32, 64];
    public bool AutoBytesPerRow { get => Settings.AutoBytesPerRow; set => Change(s => s.AutoBytesPerRow, (s, v) => s.AutoBytesPerRow = v, value); }
    public bool CanSetBytesPerRow => !AutoBytesPerRow && !_session.IsBusy;
    public bool ShowOffsets { get => Settings.ShowOffsets; set => Change(s => s.ShowOffsets, (s, v) => s.ShowOffsets = v, value); }
    public bool ShowAscii { get => Settings.ShowAscii; set => Change(s => s.ShowAscii, (s, v) => s.ShowAscii = v, value); }
    public bool ShowRuler { get => Settings.ShowRuler; set => Change(s => s.ShowRuler, (s, v) => s.ShowRuler = v, value); }
    public bool ShowAnnotations { get => Settings.ShowAnnotations; set => Change(s => s.ShowAnnotations, (s, v) => s.ShowAnnotations = v, value); }
    public string OffsetWidthInput { get => _offsetWidthInput; set => SetProperty(ref _offsetWidthInput, value); }
    public string DataWidthInput { get => _dataWidthInput; set => SetProperty(ref _dataWidthInput, value); }
    public string AsciiWidthInput { get => _asciiWidthInput; set => SetProperty(ref _asciiWidthInput, value); }
    public bool IsBytesView => ViewMode == DataViewMode.Bytes;
    public bool IsBitsView => ViewMode == DataViewMode.Bits;
    public string FormatStatus => $"{ViewMode} · {BytesPerRow} / row · {(BitNumbering == BitNumbering.LsbZero ? "LSB = bit 0" : "MSB = bit 0")}";
    public RelayCommand ApplyWidthsCommand { get; }
    public RelayCommand SetBytesViewCommand { get; }
    public RelayCommand SetBitsViewCommand { get; }

    private void Change<T>(Func<DisplaySettings, T> read, Action<DisplaySettings, T> write, T value)
    {
        if (_session.IsBusy || EqualityComparer<T>.Default.Equals(read(Settings), value)) return;
        Apply(settings => write(settings, value));
    }

    private bool Apply(Action<DisplaySettings> update)
    {
        var previous = Settings;
        var next = previous.Clone();
        try
        {
            update(next);
            _session.Document.Settings = next;
            _session.Document.NotifySettingsChanged();
            return true;
        }
        catch (Exception error)
        {
            _session.Document.Settings = previous;
            _reportStatus(error.Message);
            _dialogs.ShowError(error.Message);
            Refresh();
            return false;
        }
    }

    private void ApplyWidths()
    {
        if (Apply(settings =>
        {
            settings.OffsetWidth = ParseWidth(OffsetWidthInput);
            settings.DataWidth = ParseWidth(DataWidthInput);
            settings.AsciiWidth = ParseWidth(AsciiWidthInput);
        }))
        {
            Refresh(forceWidths: true);
            _reportStatus("Updated column widths for the grid and formatted text export.");
        }
    }

    private static double ParseWidth(string text) =>
        double.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var width) &&
        double.IsFinite(width) && width is >= 24 and <= 16384 ? width :
        throw new ArgumentException("Column widths must be between 24 and 16384 pixels.");

    private void SessionChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh(bool forceWidths = false)
    {
        if (!RowSizes.Contains(BytesPerRow))
        {
            int index = 0;
            while (index < RowSizes.Count && RowSizes[index] < BytesPerRow) index++;
            RowSizes.Insert(index, BytesPerRow);
        }
        var source = (_session.Document, Settings.OffsetWidth, Settings.DataWidth, Settings.AsciiWidth);
        bool resetWidths = forceWidths || !ReferenceEquals(_widthSource.Document, _session.Document);
        if (resetWidths || _widthSource.Offset != Settings.OffsetWidth)
            OffsetWidthInput = Settings.OffsetWidth.ToString("0", CultureInfo.InvariantCulture);
        if (resetWidths || _widthSource.Data != Settings.DataWidth)
            DataWidthInput = Settings.DataWidth.ToString("0", CultureInfo.InvariantCulture);
        if (resetWidths || _widthSource.Ascii != Settings.AsciiWidth)
            AsciiWidthInput = Settings.AsciiWidth.ToString("0", CultureInfo.InvariantCulture);
        _widthSource = source;
        OnPropertyChanged(null);
        ApplyWidthsCommand.RaiseCanExecuteChanged();
        SetBytesViewCommand.RaiseCanExecuteChanged();
        SetBitsViewCommand.RaiseCanExecuteChanged();
    }

    public void Dispose() => _session.Changed -= SessionChanged;
}
