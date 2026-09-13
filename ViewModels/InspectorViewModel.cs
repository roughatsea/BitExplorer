using System.Collections.ObjectModel;
using System.Globalization;
using BitExplorer.Core;
using WpfApp1.Infrastructure;
using WpfApp1.Services;

namespace WpfApp1.ViewModels;

/// <summary>Interprets the shared selection and retains drafts until their edit target changes.</summary>
public sealed class InspectorViewModel : ObservableObject, IDisposable
{
    private readonly ExplorerSession _session;
    private readonly IUserInteractionService _dialogs;
    private readonly Action<string> _reportStatus;
    private string _byteInput = "", _fieldValueInput = "";
    private (DocumentModel? Document, int Offset, byte Value, NumericBase Base) _byteTarget;
    private DocumentModel? _fieldDocument;
    private Guid? _fieldId;
    private string? _fieldValue;
    private long[] _fieldBits = [];
    private SelectionInterpretation _interpretation = new("—", "—", "—", "", "");

    public InspectorViewModel(ExplorerSession session, IUserInteractionService dialogs, Action<string> reportStatus)
    {
        _session = session;
        _dialogs = dialogs;
        _reportStatus = reportStatus;
        ApplyByteCommand = new RelayCommand(() => Run(ApplyByte), () => HasByte && !session.IsBusy);
        ApplyFieldCommand = new RelayCommand(() => Run(ApplyField), () => HasSelectedField && !session.IsBusy);
        for (int position = 0; position < 8; position++)
        {
            int physical = position;
            Bits.Add(new BitItemViewModel(physical, () => Run(() => FlipBit(physical)), () => HasByte && !session.IsBusy));
        }
        session.Changed += SessionChanged;
        Refresh();
    }

    private int Offset => _session.Selection.ActiveByteOffset;
    private byte CurrentByte => HasByte ? _session.Document.Data[Offset] : (byte)0;
    public bool HasByte => Offset >= 0 && Offset < _session.Document.Data.Length;
    public string SelectionCount => $"{_session.Selection.Bits.Count:N0} bits";
    public string OffsetLabel => HasByte ? $"Offset 0x{Offset:X8} · {Offset}" : "No byte selected";
    public string SelectionLabel => HasByte
        ? $"{_session.Selection.Bits.Select(bit => bit / 8).Distinct().Count():N0} source bytes · {(_session.Document.IsByteModified(Offset) ? "modified byte" : "click bits below to edit")}" : "Open a file or select a byte.";
    public string ByteHex => HasByte ? CurrentByte.ToString("X2", CultureInfo.InvariantCulture) : "—";
    public string ByteDecimal => HasByte ? CurrentByte.ToString(CultureInfo.InvariantCulture) : "—";
    public string ByteEditLabel => "EDIT BYTE · " + (_session.Document.Settings.ByteBase == NumericBase.Hexadecimal ? "HEX" : "DECIMAL");
    public string ByteInput { get => _byteInput; set => SetProperty(ref _byteInput, value); }
    public ObservableCollection<BitItemViewModel> Bits { get; } = [];
    public RelayCommand ApplyByteCommand { get; }
    public RelayCommand ApplyFieldCommand { get; }

    public string SelectionDecimal => _interpretation.Decimal;
    public string SelectionSigned => _interpretation.Signed;
    public string SelectionHex => _interpretation.Hex;
    public string SignificanceLabel => _interpretation.Significance;
    public string FieldEnds => _interpretation.Ends;
    public int InterpretationOrderIndex
    {
        get => (int)_session.InterpretationOrder;
        set { if (!_session.IsBusy && Enum.IsDefined(typeof(InterpretationOrder), value)) _session.InterpretationOrder = (InterpretationOrder)value; }
    }
    public bool CanUseSavedFieldMapping => HasSelectedField;
    public bool HasSelectedField => _session.SelectedField is not null;
    public string SelectedFieldName => _session.SelectedField?.Name ?? "";
    public string FieldNotes => _session.SelectedField is { } item
        ? string.IsNullOrWhiteSpace(item.Notes) ? "An explicitly ordered map of source bits." : item.Notes : "";
    public string FieldValueInput { get => _fieldValueInput; set => SetProperty(ref _fieldValueInput, value); }

    private void ApplyByte()
    {
        int offset = Offset;
        var value = NumericText.Parse(ByteInput, _session.Document.Settings.ByteBase == NumericBase.Hexadecimal);
        if (value < 0 || value > 255) throw new ArgumentException("A byte must be between 0 and 255 (00–FF hex).");
        _session.Document.SetByte(offset, (byte)value);
        Refresh(resetByte: true);
        _reportStatus($"Updated byte {offset} to 0x{(byte)value:X2} · {value} decimal.");
    }

    private void ApplyField()
    {
        if (_session.SelectedField is not { } field) return;
        _session.Document.SetFieldValue(field, NumericText.Parse(FieldValueInput));
        Refresh(resetField: true);
        _reportStatus($"Updated {field.Name}; other source bits were preserved.");
    }

    private void FlipBit(int physical)
    {
        int offset = Offset;
        _session.Document.FlipBit((long)offset * 8 + physical);
        int position = _session.Document.Settings.BitNumbering == BitNumbering.LsbZero ? 7 - physical : physical;
        _reportStatus($"Flipped byte {offset}, bit {position}. Ctrl+Z to undo.");
    }

    private void SessionChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh(bool resetByte = false, bool resetField = false)
    {
        var target = (_session.Document, Offset, CurrentByte, _session.Document.Settings.ByteBase);
        if (resetByte || _byteTarget != target)
        {
            ByteInput = HasByte ? DisplayFormatter.FormatByte(CurrentByte, _session.Document.Settings.ByteBase) : "";
            _byteTarget = target;
        }
        var field = _session.SelectedField;
        string? value = field is null ? null : _session.Document.ReadField(field).ToString(CultureInfo.InvariantCulture);
        if (resetField || !ReferenceEquals(_fieldDocument, _session.Document) || _fieldId != field?.Id || _fieldValue != value ||
            !_fieldBits.SequenceEqual(field?.OrderedBits ?? []))
        {
            FieldValueInput = value ?? "";
            _fieldDocument = _session.Document;
            _fieldId = field?.Id;
            _fieldValue = value;
            _fieldBits = field?.OrderedBits.ToArray() ?? [];
        }
        _interpretation = SelectionInterpretation.From(_session);
        foreach (var bit in Bits) bit.Update(CurrentByte, _session.Document.Settings.BitNumbering);
        OnPropertyChanged(null);
        ApplyByteCommand.RaiseCanExecuteChanged();
        ApplyFieldCommand.RaiseCanExecuteChanged();
    }

    private void Run(Action action)
    {
        try { action(); }
        catch (Exception error) { _reportStatus(error.Message); _dialogs.ShowError(error.Message); }
    }

    public void Dispose() => _session.Changed -= SessionChanged;
}
