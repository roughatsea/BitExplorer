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

// Make names from System.Collections.ObjectModel available here without writing their full prefix each
// time. This does not run that library's code.
using System.Collections.ObjectModel;
// Make names from System.Globalization available here without writing their full prefix each time. This
// does not run that library's code.
using System.Globalization;
// Make names from BitExplorer.Core available here without writing their full prefix each time. This does
// not run that library's code.
using BitExplorer.Core;
// Make names from WpfApp1.Infrastructure available here without writing their full prefix each time. This
// does not run that library's code.
using WpfApp1.Infrastructure;
// Make names from WpfApp1.Services available here without writing their full prefix each time. This does
// not run that library's code.
using WpfApp1.Services;

// Place this file's definitions in the WpfApp1.ViewModels naming group, which prevents clashes with names
// in other groups.
namespace WpfApp1.ViewModels;

/// <summary>
/// Presents display preferences to WPF bindings. A binding reads these properties
/// to draw a control and writes them when the user changes it. Settings go through
/// validation before other panels are notified; unfinished width text stays separate.
/// </summary>
public sealed class DisplaySettingsViewModel : ObservableObject, IDisposable
{
    // Reserve _session to hold an object or value of type ExplorerSession; setup can supply its value,
    // otherwise the type's default is used.
    private readonly ExplorerSession _session;
    // Reserve _dialogs to hold an object or value of type IUserInteractionService; setup can supply its
    // value, otherwise the type's default is used.
    private readonly IUserInteractionService _dialogs;
    // Reserve _reportStatus to hold an object or value of type Action<string>; setup can supply its value,
    // otherwise the type's default is used.
    private readonly Action<string> _reportStatus;
    // Remember empty text as _offsetWidthInput. Remember empty text as _dataWidthInput. Remember empty text
    // as _asciiWidthInput.
    private string _offsetWidthInput = "", _dataWidthInput = "", _asciiWidthInput = "";
    // This tuple is a snapshot: the model values last used to fill the three text boxes.
    private (DocumentModel? Document, double Offset, double Data, double Ascii) _widthSource;

    /// <summary>Creates the view/apply commands and listens for changes from other parts of the app.</summary>
    public DisplaySettingsViewModel(ExplorerSession session, IUserInteractionService dialogs, Action<string> reportStatus)
    {
        // Set _session to session.
        _session = session;
        // Set _dialogs to dialogs.
        _dialogs = dialogs;
        // Set _reportStatus to reportStatus.
        _reportStatus = reportStatus;
        // Prepare ApplyWidthsCommand for later activation: run ApplyWidths. It can run when it is not the
        // case that session.IsBusy is true. Creating the command here does not perform its action.
        ApplyWidthsCommand = new RelayCommand(ApplyWidths, () => !session.IsBusy);
        // Prepare SetBytesViewCommand for later activation: set ViewMode to DataViewMode.Bytes. It can run
        // when it is not the case that session.IsBusy is true. Creating the command here does not perform
        // its action.
        SetBytesViewCommand = new RelayCommand(() => ViewMode = DataViewMode.Bytes, () => !session.IsBusy);
        // Prepare SetBitsViewCommand for later activation: set ViewMode to DataViewMode.Bits. It can run
        // when it is not the case that session.IsBusy is true. Creating the command here does not perform
        // its action.
        SetBitsViewCommand = new RelayCommand(() => ViewMode = DataViewMode.Bits, () => !session.IsBusy);
        // Register SessionChanged as a listener for session.Changed; the listener runs when that event is
        // raised.
        session.Changed += SessionChanged;
        // Run Refresh to recalculate the values this part of the application presents.
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
    // Expose OffsetBaseIndex as a whole number. get allows reading; set, if present, handles writing.
    public int OffsetBaseIndex { get => (int)OffsetBase; set { if (value is 0 or 1) OffsetBase = (NumericBase)value; } }
    // Expose BitNumberingIndex as a whole number. get allows reading; set, if present, handles writing.
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
    // Expose ShowAscii as a true-or-false answer. get allows reading; set, if present, handles writing.
    public bool ShowAscii { get => Settings.ShowAscii; set => Change(s => s.ShowAscii, (s, v) => s.ShowAscii = v, value); }
    // Expose ShowRuler as a true-or-false answer. get allows reading; set, if present, handles writing.
    public bool ShowRuler { get => Settings.ShowRuler; set => Change(s => s.ShowRuler, (s, v) => s.ShowRuler = v, value); }
    // Expose ShowAnnotations as a true-or-false answer. get allows reading; set, if present, handles
    // writing.
    public bool ShowAnnotations { get => Settings.ShowAnnotations; set => Change(s => s.ShowAnnotations, (s, v) => s.ShowAnnotations = v, value); }
    // Text drafts can temporarily contain incomplete numbers. They become settings
    // only when ApplyWidthsCommand validates all three values together.
    public string OffsetWidthInput { get => _offsetWidthInput; set => SetProperty(ref _offsetWidthInput, value); }
    // Expose DataWidthInput as text. get allows reading; set, if present, handles writing.
    public string DataWidthInput { get => _dataWidthInput; set => SetProperty(ref _dataWidthInput, value); }
    // Expose AsciiWidthInput as text. get allows reading; set, if present, handles writing.
    public string AsciiWidthInput { get => _asciiWidthInput; set => SetProperty(ref _asciiWidthInput, value); }
    // Read-only properties let XAML highlight the active view button and show a summary.
    public bool IsBytesView => ViewMode == DataViewMode.Bytes;
    // Expose IsBitsView as a true-or-false answer. Reading it computes whether ViewMode equals
    // DataViewMode.Bits.
    public bool IsBitsView => ViewMode == DataViewMode.Bits;
    // Expose FormatStatus as text. Reading it computes the displayed text below; each {...} inserts a
    // computed value into the text.
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
        // If _session.IsBusy is true, or EqualityComparer<T>.Default.Equals(read(Settings), value) is true,
        // leave this method immediately.
        if (_session.IsBusy || EqualityComparer<T>.Default.Equals(read(Settings), value)) return;
        // Call Apply: Edits a settings copy and restores the original if parsing or model validation fails.
        Apply(settings => write(settings, value));
    }

    /// <summary>Edits a settings copy and restores the original if parsing or model validation fails.</summary>
    private bool Apply(Action<DisplaySettings> update)
    {
        // Remember Settings as previous.
        var previous = Settings;
        // Remember a copy returned by previous.Clone() as next.
        var next = previous.Clone();
        // Attempt this work. A matching catch below handles a reported exception; a finally block, when
        // present, performs cleanup on the way out.
        try
        {
            // Call update(...); the values in parentheses are the inputs.
            update(next);
            // Set _session.Document.Settings to next.
            _session.Document.Settings = next;
            // Call _session.Document.NotifySettingsChanged: Validates display settings and asks observers
            // to refresh; settings are not undo commands.
            _session.Document.NotifySettingsChanged();
            // Return true (yes) to the caller and leave this method.
            return true;
        }
        // If the preceding try reports Exception, refer to it as error; run this recovery path.
        catch (Exception error)
        {
            // Set _session.Document.Settings to previous.
            _session.Document.Settings = previous;
            // Send the supplied message to the application's status display.
            _reportStatus(error.Message);
            // Show the supplied error message through the interaction service.
            _dialogs.ShowError(error.Message);
            // Run Refresh to recalculate the values this part of the application presents.
            Refresh();
            // Return false (no) to the caller and leave this method.
            return false;
        }
    }

    /// <summary>Parses all three drafts before accepting their shared settings copy, avoiding a partial update.</summary>
    private void ApplyWidths()
    {
        // If Apply(settings => { settings.OffsetWidth = ParseWidth(OffsetWidthInput); settings.DataWidth =
        // ParseWidth(DataWidthInput); settings.AsciiWidth = ParseWidth(AsciiWidthInput); }) is true, run
        // the following grouped instructions.
        if (Apply(settings =>
        {
            // Set settings.OffsetWidth to the result returned by ParseWidth(...).
            settings.OffsetWidth = ParseWidth(OffsetWidthInput);
            // Set settings.DataWidth to the result returned by ParseWidth(...).
            settings.DataWidth = ParseWidth(DataWidthInput);
            // Set settings.AsciiWidth to the result returned by ParseWidth(...).
            settings.AsciiWidth = ParseWidth(AsciiWidthInput);
        }))
        {
            // Run Refresh to recalculate the values this part of the application presents, using the
            // options in parentheses.
            Refresh(forceWidths: true);
            // Send the supplied message to the application's status display.
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
            // Remember 0 as index.
            int index = 0;
            // Keep repeating the following instructions while index < RowSizes.Count && RowSizes[index] <
            // BytesPerRow is true; check again before each pass.
            while (index < RowSizes.Count && RowSizes[index] < BytesPerRow) index++;
            // Call RowSizes.Insert(...); the values in parentheses are the inputs.
            RowSizes.Insert(index, BytesPerRow);
        }
        // Remember the values in parentheses grouped into one package (a tuple) as source.
        var source = (_session.Document, Settings.OffsetWidth, Settings.DataWidth, Settings.AsciiWidth);
        // Remember at least one condition being true: forceWidths or
        // !ReferenceEquals(_widthSource.Document, _session.Document) as resetWidths.
        bool resetWidths = forceWidths || !ReferenceEquals(_widthSource.Document, _session.Document);
        // If resetWidths is true, or _widthSource.Offset differs from Settings.OffsetWidth, set
        // OffsetWidthInput to Settings.OffsetWidth converted to text; the arguments select its formatting.
        if (resetWidths || _widthSource.Offset != Settings.OffsetWidth)
            // Set OffsetWidthInput to Settings.OffsetWidth converted to text; the arguments select its
            // formatting.
            OffsetWidthInput = Settings.OffsetWidth.ToString("0", CultureInfo.InvariantCulture);
        // If resetWidths is true, or _widthSource.Data differs from Settings.DataWidth, set DataWidthInput
        // to Settings.DataWidth converted to text; the arguments select its formatting.
        if (resetWidths || _widthSource.Data != Settings.DataWidth)
            // Set DataWidthInput to Settings.DataWidth converted to text; the arguments select its
            // formatting.
            DataWidthInput = Settings.DataWidth.ToString("0", CultureInfo.InvariantCulture);
        // If resetWidths is true, or _widthSource.Ascii differs from Settings.AsciiWidth, set
        // AsciiWidthInput to Settings.AsciiWidth converted to text; the arguments select its formatting.
        if (resetWidths || _widthSource.Ascii != Settings.AsciiWidth)
            // Set AsciiWidthInput to Settings.AsciiWidth converted to text; the arguments select its
            // formatting.
            AsciiWidthInput = Settings.AsciiWidth.ToString("0", CultureInfo.InvariantCulture);
        // Set _widthSource to source.
        _widthSource = source;
        // Notify values and command availability separately: bindings read properties,
        // while buttons call CanExecute when their command raises this second event.
        OnPropertyChanged(null);
        // Ask controls using ApplyWidthsCommand to check again whether the command is allowed to run.
        ApplyWidthsCommand.RaiseCanExecuteChanged();
        // Ask controls using SetBytesViewCommand to check again whether the command is allowed to run.
        SetBytesViewCommand.RaiseCanExecuteChanged();
        // Ask controls using SetBitsViewCommand to check again whether the command is allowed to run.
        SetBitsViewCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Stops observing the shared session when this panel's owner is disposed.</summary>
    public void Dispose() => _session.Changed -= SessionChanged;
}
