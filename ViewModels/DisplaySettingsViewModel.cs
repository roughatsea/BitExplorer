using System.Collections.ObjectModel;
using System.Globalization;
using BitExplorer.Core;
using WpfApp1.Infrastructure;
using WpfApp1.Services;

namespace WpfApp1.ViewModels;

/// <summary>
/// Presents display preferences to WPF bindings. A binding reads these properties
/// to draw a control and writes them when the user changes it. Settings go through
/// validation before other panels are notified; unfinished width text stays separate.
/// </summary>
public sealed class DisplaySettingsViewModel : ObservableObject, IDisposable
{
    private readonly ExplorerSession _session;
    private readonly IUserInteractionService _dialogs;
    private readonly Action<string> _reportStatus;
    private string _offsetWidthInput = "", _dataWidthInput = "", _asciiWidthInput = "";
    // This tuple is a snapshot: the model values last used to fill the three text boxes.
    private (DocumentModel? Document, double Offset, double Data, double Ascii) _widthSource;

    /// <summary>Creates the view/apply commands and listens for changes from other parts of the app.</summary>
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

    /// <summary>Always resolves settings from the current document, including after an open operation.</summary>
    private DisplaySettings Settings => _session.Document.Settings;
    /// <summary>Chooses byte tokens or groups of eight visible binary digits in the grid.</summary>
    public DataViewMode ViewMode { get => Settings.ViewMode; set => Change(s => s.ViewMode, (s, v) => s.ViewMode = v, value); }
    /// <summary>Chooses hexadecimal or decimal notation for byte values.</summary>
    public NumericBase ByteBase { get => Settings.ByteBase; set => Change(s => s.ByteBase, (s, v) => s.ByteBase = v, value); }
    /// <summary>Chooses hexadecimal or decimal notation independently for offsets.</summary>
    public NumericBase OffsetBase { get => Settings.OffsetBase; set => Change(s => s.OffsetBase, (s, v) => s.OffsetBase = v, value); }
    /// <summary>Changes the ruler's bit numbers without rearranging bit significance or stored data.</summary>
    public BitNumbering BitNumbering { get => Settings.BitNumbering; set => Change(s => s.BitNumbering, (s, v) => s.BitNumbering = v, value); }
    // These three adapters map ComboBox.SelectedIndex (0 or 1) to the enums above.
    // They own no duplicate settings; both getter and setter use the same underlying values.
    public int ByteBaseIndex { get => (int)ByteBase; set { if (value is 0 or 1) ByteBase = (NumericBase)value; } }
    public int OffsetBaseIndex { get => (int)OffsetBase; set { if (value is 0 or 1) OffsetBase = (NumericBase)value; } }
    public int BitNumberingIndex { get => (int)BitNumbering; set { if (value is 0 or 1) BitNumbering = (BitNumbering)value; } }
    /// <summary>The number of source bytes on one grid row and one exported data row.</summary>
    public int BytesPerRow { get => Settings.BytesPerRow; set => Change(s => s.BytesPerRow, (s, v) => s.BytesPerRow = v, value); }
    /// <summary>Common row sizes; ObservableCollection tells WPF when an imported size is added.</summary>
    public ObservableCollection<int> RowSizes { get; } = [4, 8, 16, 24, 32, 64];
    /// <summary>Lets grid layout choose a row size that fits the current data-column width.</summary>
    public bool AutoBytesPerRow { get => Settings.AutoBytesPerRow; set => Change(s => s.AutoBytesPerRow, (s, v) => s.AutoBytesPerRow = v, value); }
    /// <summary>Disables manual row sizing during automatic fitting or a busy file operation.</summary>
    public bool CanSetBytesPerRow => !AutoBytesPerRow && !_session.IsBusy;
    // Visibility switches feed both the screen formatter and whole-file text export:
    // offsets are addresses, ASCII is printable text, the ruler shows positions,
    // and annotations are the names of fields overlapping each row.
    public bool ShowOffsets { get => Settings.ShowOffsets; set => Change(s => s.ShowOffsets, (s, v) => s.ShowOffsets = v, value); }
    public bool ShowAscii { get => Settings.ShowAscii; set => Change(s => s.ShowAscii, (s, v) => s.ShowAscii = v, value); }
    public bool ShowRuler { get => Settings.ShowRuler; set => Change(s => s.ShowRuler, (s, v) => s.ShowRuler = v, value); }
    public bool ShowAnnotations { get => Settings.ShowAnnotations; set => Change(s => s.ShowAnnotations, (s, v) => s.ShowAnnotations = v, value); }
    // Text drafts can temporarily contain incomplete numbers. They become settings
    // only when ApplyWidthsCommand validates all three values together.
    public string OffsetWidthInput { get => _offsetWidthInput; set => SetProperty(ref _offsetWidthInput, value); }
    public string DataWidthInput { get => _dataWidthInput; set => SetProperty(ref _dataWidthInput, value); }
    public string AsciiWidthInput { get => _asciiWidthInput; set => SetProperty(ref _asciiWidthInput, value); }
    // Read-only properties let XAML highlight the active view button and show a summary.
    public bool IsBytesView => ViewMode == DataViewMode.Bytes;
    public bool IsBitsView => ViewMode == DataViewMode.Bits;
    public string FormatStatus => $"{ViewMode} · {BytesPerRow} / row · {(BitNumbering == BitNumbering.LsbZero ? "LSB = bit 0" : "MSB = bit 0")}";
    /// <summary>Validates and applies the offset, data and ASCII width drafts as one operation.</summary>
    public RelayCommand ApplyWidthsCommand { get; }
    /// <summary>Selects the byte view; CanExecute prevents changes while the session is busy.</summary>
    public RelayCommand SetBytesViewCommand { get; }
    /// <summary>Selects the bit view without changing the data or selected addresses.</summary>
    public RelayCommand SetBitsViewCommand { get; }

    /// <summary>
    /// Common setter logic. The type parameter T lets booleans, numbers and enums
    /// share validation; read/write lambdas specify which setting is being changed.
    /// </summary>
    private void Change<T>(Func<DisplaySettings, T> read, Action<DisplaySettings, T> write, T value)
    {
        if (_session.IsBusy || EqualityComparer<T>.Default.Equals(read(Settings), value)) return;
        Apply(settings => write(settings, value));
    }

    /// <summary>Edits a settings copy and restores the original if parsing or model validation fails.</summary>
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

    /// <summary>Parses all three drafts before accepting their shared settings copy, avoiding a partial update.</summary>
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

    /// <summary>Accepts a finite width in WPF screen units, in the supported range and independent of the machine's locale.</summary>
    private static double ParseWidth(string text) =>
        double.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var width) &&
        double.IsFinite(width) && width is >= 24 and <= 16384 ? width :
        throw new ArgumentException("Column widths must be between 24 and 16384 pixels.");

    /// <summary>Receives shared changes, including grid divider drags and document replacements.</summary>
    private void SessionChanged(object? sender, EventArgs e) => Refresh();

    /// <summary>
    /// Refreshes bindings while preserving drafts whose underlying width has not
    /// changed. For example, toggling the ruler should not erase a half-typed data width.
    /// </summary>
    private void Refresh(bool forceWidths = false)
    {
        // A saved project may use a valid size not present in the common-size list.
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
        // Notify values and command availability separately: bindings read properties,
        // while buttons call CanExecute when their command raises this second event.
        OnPropertyChanged(null);
        ApplyWidthsCommand.RaiseCanExecuteChanged();
        SetBytesViewCommand.RaiseCanExecuteChanged();
        SetBitsViewCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Stops observing the shared session when this panel's owner is disposed.</summary>
    public void Dispose() => _session.Changed -= SessionChanged;
}
