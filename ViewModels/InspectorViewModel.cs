using System.Collections.ObjectModel;
using System.Globalization;
using BitExplorer.Core;
using WpfApp1.Infrastructure;
using WpfApp1.Services;

namespace WpfApp1.ViewModels;

/// <summary>
/// Supplies the inspector's bound values and editing commands. It reads the shared
/// session instead of copying selection state. Text being typed is a draft: it is
/// kept until its source byte/field changes or the user explicitly applies it.
/// This stops an unrelated ruler toggle or edit elsewhere from erasing partial input.
/// </summary>
public sealed class InspectorViewModel : ObservableObject, IDisposable
{
    private readonly ExplorerSession _session;
    private readonly IUserInteractionService _dialogs;
    private readonly Action<string> _reportStatus;
    private string _byteInput = "", _fieldValueInput = "";
    // The byte tuple and field snapshots remember the source last displayed in the
    // text boxes. Comparing these with new model values distinguishes a real target
    // change from an unrelated notification that should preserve the user's draft.
    private (DocumentModel? Document, int Offset, byte Value, NumericBase Base) _byteTarget;
    private DocumentModel? _fieldDocument;
    private Guid? _fieldId;
    private string? _fieldValue;
    private long[] _fieldBits = [];
    private SelectionInterpretation _interpretation = new("—", "—", "—", "", "");

    /// <summary>Creates edit/flip commands and the eight reusable bit-button view models.</summary>
    public InspectorViewModel(ExplorerSession session, IUserInteractionService dialogs, Action<string> reportStatus)
    {
        _session = session;
        _dialogs = dialogs;
        _reportStatus = reportStatus;
        ApplyByteCommand = new RelayCommand(() => Run(ApplyByte), () => HasByte && !session.IsBusy);
        ApplyFieldCommand = new RelayCommand(() => Run(ApplyField), () => HasSelectedField && !session.IsBusy);
        for (int position = 0; position < 8; position++)
        {
            // Give each lambda its own position value. Otherwise capturing a changing
            // loop variable would make buttons refer to the wrong bit after the loop.
            int physical = position;
            Bits.Add(new BitItemViewModel(physical, () => Run(() => FlipBit(physical)), () => HasByte && !session.IsBusy));
        }
        session.Changed += SessionChanged;
        Refresh();
    }

    /// <summary>The byte under the active selection endpoint, resolved from the shared selection.</summary>
    private int Offset => _session.Selection.ActiveByteOffset;
    /// <summary>The active byte value, or a harmless zero placeholder when there is no valid byte.</summary>
    private byte CurrentByte => HasByte ? _session.Document.Data[Offset] : (byte)0;
    /// <summary>Enables byte editing only when the active offset lies inside the current file.</summary>
    public bool HasByte => Offset >= 0 && Offset < _session.Document.Data.Length;
    /// <summary>The number of selected source bits shown in the inspector heading.</summary>
    public string SelectionCount => $"{_session.Selection.Bits.Count:N0} bits";
    /// <summary>Shows the active offset in both hexadecimal and decimal for easy conversion.</summary>
    public string OffsetLabel => HasByte ? $"Offset 0x{Offset:X8} · {Offset}" : "No byte selected";
    /// <summary>Shows how many source bytes are selected and whether the active byte has changed.</summary>
    public string SelectionLabel => HasByte
        ? $"{_session.Selection.Bits.Select(bit => bit / 8).Distinct().Count():N0} source bytes · {(_session.Document.IsByteModified(Offset) ? "modified byte" : "click bits below to edit")}" : "Open a file or select a byte.";
    // These two readouts show the same byte in both number bases. X2 means at least
    // two hexadecimal digits; InvariantCulture keeps formatting stable across locales.
    public string ByteHex => HasByte ? CurrentByte.ToString("X2", CultureInfo.InvariantCulture) : "—";
    public string ByteDecimal => HasByte ? CurrentByte.ToString(CultureInfo.InvariantCulture) : "—";
    /// <summary>Explains which base a byte draft uses when no explicit 0x prefix is supplied.</summary>
    public string ByteEditLabel => "EDIT BYTE · " + (_session.Document.Settings.ByteBase == NumericBase.Hexadecimal ? "HEX" : "DECIMAL");
    /// <summary>Unvalidated byte text from the input box; applying it performs parsing and range checks.</summary>
    public string ByteInput { get => _byteInput; set => SetProperty(ref _byteInput, value); }
    /// <summary>Eight bit buttons, ordered from the physical most significant bit to the least significant bit.</summary>
    public ObservableCollection<BitItemViewModel> Bits { get; } = [];
    /// <summary>Writes a valid byte draft as one undoable model edit.</summary>
    public RelayCommand ApplyByteCommand { get; }
    /// <summary>Writes a valid field draft through its saved map, preserving other bits in the source bytes.</summary>
    public RelayCommand ApplyFieldCommand { get; }

    // A single computed interpretation supplies unsigned decimal, signed decimal,
    // hexadecimal, byte significance and the source addresses of the field endpoints.
    // Reusing it avoids reading the same selected bits separately for every binding.
    public string SelectionDecimal => _interpretation.Decimal;
    public string SelectionSigned => _interpretation.Signed;
    public string SelectionHex => _interpretation.Hex;
    public string SignificanceLabel => _interpretation.Significance;
    public string FieldEnds => _interpretation.Ends;
    /// <summary>Adapts the order ComboBox's selected index to the session's interpretation enum.</summary>
    public int InterpretationOrderIndex
    {
        get => (int)_session.InterpretationOrder;
        set { if (!_session.IsBusy && Enum.IsDefined(typeof(InterpretationOrder), value)) _session.InterpretationOrder = (InterpretationOrder)value; }
    }
    /// <summary>Disables the saved-map order choice when no named field is selected.</summary>
    public bool CanUseSavedFieldMapping => HasSelectedField;
    /// <summary>Controls whether the selected-field section is visible.</summary>
    public bool HasSelectedField => _session.SelectedField is not null;
    /// <summary>The current field's heading, resolved after edits and undo/redo.</summary>
    public string SelectedFieldName => _session.SelectedField?.Name ?? "";
    /// <summary>The field's notes, with a descriptive fallback for an empty note.</summary>
    public string FieldNotes => _session.SelectedField is { } item
        ? string.IsNullOrWhiteSpace(item.Notes) ? "An explicitly ordered map of source bits." : item.Notes : "";
    /// <summary>Unvalidated field-value draft, accepting decimal or explicitly prefixed hexadecimal.</summary>
    public string FieldValueInput { get => _fieldValueInput; set => SetProperty(ref _fieldValueInput, value); }

    /// <summary>Parses a draft, checks the 0–255 byte range, applies it and normalizes the displayed text.</summary>
    private void ApplyByte()
    {
        int offset = Offset;
        var value = NumericText.Parse(ByteInput, _session.Document.Settings.ByteBase == NumericBase.Hexadecimal);
        if (value < 0 || value > 255) throw new ArgumentException("A byte must be between 0 and 255 (00–FF hex).");
        _session.Document.SetByte(offset, (byte)value);
        Refresh(resetByte: true);
        _reportStatus($"Updated byte {offset} to 0x{(byte)value:X2} · {value} decimal.");
    }

    /// <summary>Writes the selected field's numeric value; the model checks that it fits the field's bit count.</summary>
    private void ApplyField()
    {
        if (_session.SelectedField is not { } field) return;
        _session.Document.SetFieldValue(field, NumericText.Parse(FieldValueInput));
        Refresh(resetField: true);
        _reportStatus($"Updated {field.Name}; other source bits were preserved.");
    }

    /// <summary>Converts a button's within-byte position to a file address and makes one undoable bit flip.</summary>
    private void FlipBit(int physical)
    {
        int offset = Offset;
        _session.Document.FlipBit((long)offset * 8 + physical);
        int position = _session.Document.Settings.BitNumbering == BitNumbering.LsbZero ? 7 - physical : physical;
        _reportStatus($"Flipped byte {offset}, bit {position}. Ctrl+Z to undo.");
    }

    /// <summary>Refreshes inspector values after model, selection, order or display settings change.</summary>
    private void SessionChanged(object? sender, EventArgs e) => Refresh();

    /// <summary>
    /// Recomputes readouts and compares edit-target snapshots before replacing drafts.
    /// Changing the field map matters even when its numeric value happens to stay the
    /// same: applying a retained draft would otherwise write to a different set of bits.
    /// </summary>
    private void Refresh(bool resetByte = false, bool resetField = false)
    {
        var target = (_session.Document, Offset, CurrentByte, _session.Document.Settings.ByteBase);
        // Tuple comparison checks the document, offset, value and byte notation together.
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
        // Ask WPF to reread all inspector values, then update command enablement.
        OnPropertyChanged(null);
        ApplyByteCommand.RaiseCanExecuteChanged();
        ApplyFieldCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Turns invalid input and model errors into status/dialog feedback without crashing the UI.</summary>
    private void Run(Action action)
    {
        try { action(); }
        catch (Exception error) { _reportStatus(error.Message); _dialogs.ShowError(error.Message); }
    }

    /// <summary>Detaches session notifications when the inspector's owning exploration closes.</summary>
    public void Dispose() => _session.Changed -= SessionChanged;
}
