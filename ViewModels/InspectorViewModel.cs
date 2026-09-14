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
/// Supplies the inspector's bound values and editing commands. It reads the shared
/// session instead of copying selection state. Text being typed is a draft: it is
/// kept until its source byte/field changes or the user explicitly applies it.
/// This stops an unrelated ruler toggle or edit elsewhere from erasing partial input.
/// </summary>
public sealed class InspectorViewModel : ObservableObject, IDisposable
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
    // Remember empty text as _byteInput. Remember empty text as _fieldValueInput.
    private string _byteInput = "", _fieldValueInput = "";
    // The byte tuple and field snapshots remember the source last displayed in the
    // text boxes. Comparing these with new model values distinguishes a real target
    // change from an unrelated notification that should preserve the user's draft.
    private (DocumentModel? Document, int Offset, byte Value, NumericBase Base) _byteTarget;
    // Reserve _fieldDocument to hold an object or value of type DocumentModel?; setup can supply its value,
    // otherwise the type's default is used.
    private DocumentModel? _fieldDocument;
    // Reserve _fieldId to hold an object or value of type Guid?; setup can supply its value, otherwise the
    // type's default is used.
    private Guid? _fieldId;
    // Reserve _fieldValue to hold an object or value of type string?; setup can supply its value, otherwise
    // the type's default is used.
    private string? _fieldValue;
    // Remember an empty collection as _fieldBits.
    private long[] _fieldBits = [];
    // Remember a new object of the required type using the inputs in parentheses as _interpretation.
    private SelectionInterpretation _interpretation = new("—", "—", "—", "", "");

    /// <summary>Creates edit/flip commands and the eight reusable bit-button view models.</summary>
    public InspectorViewModel(ExplorerSession session, IUserInteractionService dialogs, Action<string> reportStatus)
    {
        // Set _session to session.
        _session = session;
        // Set _dialogs to dialogs.
        _dialogs = dialogs;
        // Set _reportStatus to reportStatus.
        _reportStatus = reportStatus;
        // Prepare ApplyByteCommand for later activation: call Run: Runs an operation on the calling thread
        // and converts exceptions into a message and a false result. It can run when both HasByte is true
        // and it is not the case that session.IsBusy is true. Creating the command here does not perform
        // its action.
        ApplyByteCommand = new RelayCommand(() => Run(ApplyByte), () => HasByte && !session.IsBusy);
        // Prepare ApplyFieldCommand for later activation: call Run: Runs an operation on the calling thread
        // and converts exceptions into a message and a false result. It can run when both HasSelectedField
        // is true and it is not the case that session.IsBusy is true. Creating the command here does not
        // perform its action.
        ApplyFieldCommand = new RelayCommand(() => Run(ApplyField), () => HasSelectedField && !session.IsBusy);
        // Repeat with int position = 0 as the starting state, while position is less than 8; after each
        // pass, increase position by one. The braces contain one pass.
        for (int position = 0; position < 8; position++)
        {
            // Give each lambda its own position value. Otherwise capturing a changing
            // loop variable would make buttons refer to the wrong bit after the loop.
            int physical = position;
            // Add new BitItemViewModel(physical, () => Run(() => FlipBit(physical)), () => HasByte &&
            // !session.IsBusy) to Bits.
            Bits.Add(new BitItemViewModel(physical, () => Run(() => FlipBit(physical)), () => HasByte && !session.IsBusy));
        }
        // Register SessionChanged as a listener for session.Changed; the listener runs when that event is
        // raised.
        session.Changed += SessionChanged;
        // Run Refresh to recalculate the values this part of the application presents.
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
    // Expose ByteDecimal as text. Reading it computes CurrentByte converted to text; the arguments select
    // its formatting when HasByte is true; otherwise the text "—".
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
    // Expose SelectionSigned as text. Reading it computes _interpretation.Signed.
    public string SelectionSigned => _interpretation.Signed;
    // Expose SelectionHex as text. Reading it computes _interpretation.Hex.
    public string SelectionHex => _interpretation.Hex;
    // Expose SignificanceLabel as text. Reading it computes _interpretation.Significance.
    public string SignificanceLabel => _interpretation.Significance;
    // Expose FieldEnds as text. Reading it computes _interpretation.Ends.
    public string FieldEnds => _interpretation.Ends;
    /// <summary>Adapts the order ComboBox's selected index to the session's interpretation enum.</summary>
    public int InterpretationOrderIndex
    {
        // When this property is read, return _session.InterpretationOrder, converted to int.
        get => (int)_session.InterpretationOrder;
        // When this property is written, the proposed new content is called value; run this setter to
        // decide how to store it.
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
        // Remember Offset as offset.
        int offset = Offset;
        // Remember the result returned by NumericText.Parse(...) as value.
        var value = NumericText.Parse(ByteInput, _session.Document.Settings.ByteBase == NumericBase.Hexadecimal);
        // If value is less than 0, or value is greater than 255, stop this normal path by reporting
        // ArgumentException; the arguments carry the error details.
        if (value < 0 || value > 255) throw new ArgumentException("A byte must be between 0 and 255 (00–FF hex).");
        // Ask the document to write the supplied value at the supplied byte offset, recording an undoable
        // edit if it changes.
        _session.Document.SetByte(offset, (byte)value);
        // Run Refresh to recalculate the values this part of the application presents, using the options in
        // parentheses.
        Refresh(resetByte: true);
        // Send the supplied message to the application's status display.
        _reportStatus($"Updated byte {offset} to 0x{(byte)value:X2} · {value} decimal.");
    }

    /// <summary>Writes the selected field's numeric value; the model checks that it fits the field's bit count.</summary>
    private void ApplyField()
    {
        // If _session.SelectedField matches not { } field, leave this method immediately.
        if (_session.SelectedField is not { } field) return;
        // Call _session.Document.SetFieldValue: Writes an unsigned field value as one undoable change,
        // preserving all bits outside the field.
        _session.Document.SetFieldValue(field, NumericText.Parse(FieldValueInput));
        // Run Refresh to recalculate the values this part of the application presents, using the options in
        // parentheses.
        Refresh(resetField: true);
        // Send the supplied message to the application's status display.
        _reportStatus($"Updated {field.Name}; other source bits were preserved.");
    }

    /// <summary>Converts a button's within-byte position to a file address and makes one undoable bit flip.</summary>
    private void FlipBit(int physical)
    {
        // Remember Offset as offset.
        int offset = Offset;
        // Ask the document to invert the bit at the supplied physical file address as an undoable edit.
        _session.Document.FlipBit((long)offset * 8 + physical);
        // Remember 7 minus physical when _session.Document.Settings.BitNumbering equals
        // BitNumbering.LsbZero; otherwise physical as position.
        int position = _session.Document.Settings.BitNumbering == BitNumbering.LsbZero ? 7 - physical : physical;
        // Send the supplied message to the application's status display.
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
        // Remember the values in parentheses grouped into one package (a tuple) as target.
        var target = (_session.Document, Offset, CurrentByte, _session.Document.Settings.ByteBase);
        // Tuple comparison checks the document, offset, value and byte notation together.
        if (resetByte || _byteTarget != target)
        {
            // Set ByteInput to the result returned by DisplayFormatter.FormatByte(...) when HasByte is
            // true; otherwise empty text.
            ByteInput = HasByte ? DisplayFormatter.FormatByte(CurrentByte, _session.Document.Settings.ByteBase) : "";
            // Set _byteTarget to target.
            _byteTarget = target;
        }
        // Remember _session.SelectedField as field.
        var field = _session.SelectedField;
        // Remember null (no value) when field matches null; otherwise _session.Document.ReadField(field)
        // converted to text; the arguments select its formatting as value.
        string? value = field is null ? null : _session.Document.ReadField(field).ToString(CultureInfo.InvariantCulture);
        // If resetField is true, or it is not the case that ReferenceEquals(_fieldDocument,
        // _session.Document) is true, or _fieldId differs from field?.Id, or _fieldValue differs from
        // value, or it is not the case that _fieldBits.SequenceEqual(field?.OrderedBits ?? []) is true, run
        // the following grouped instructions.
        if (resetField || !ReferenceEquals(_fieldDocument, _session.Document) || _fieldId != field?.Id || _fieldValue != value ||
            !_fieldBits.SequenceEqual(field?.OrderedBits ?? []))
        {
            // Set FieldValueInput to value, falling back to empty text if it is null.
            FieldValueInput = value ?? "";
            // Set _fieldDocument to _session.Document.
            _fieldDocument = _session.Document;
            // Set _fieldId to field?.Id.
            _fieldId = field?.Id;
            // Set _fieldValue to value.
            _fieldValue = value;
            // Set _fieldBits to field?.OrderedBits.ToArray(), falling back to an empty collection if it is
            // null.
            _fieldBits = field?.OrderedBits.ToArray() ?? [];
        }
        // Set _interpretation to the result returned by SelectionInterpretation.From(...).
        _interpretation = SelectionInterpretation.From(_session);
        // Take each item from Bits in turn, call the current item bit, and call bit.Update: Recalculates
        // geometry from the document and measured font width.
        foreach (var bit in Bits) bit.Update(CurrentByte, _session.Document.Settings.BitNumbering);
        // Ask WPF to reread all inspector values, then update command enablement.
        OnPropertyChanged(null);
        // Ask controls using ApplyByteCommand to check again whether the command is allowed to run.
        ApplyByteCommand.RaiseCanExecuteChanged();
        // Ask controls using ApplyFieldCommand to check again whether the command is allowed to run.
        ApplyFieldCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Turns invalid input and model errors into status/dialog feedback without crashing the UI.</summary>
    private void Run(Action action)
    {
        // Attempt this work. A matching catch below handles a reported exception; a finally block, when
        // present, performs cleanup on the way out.
        try { action(); }
        // If the preceding try reports Exception, refer to it as error; run this recovery path.
        catch (Exception error) { _reportStatus(error.Message); _dialogs.ShowError(error.Message); }
    }

    /// <summary>Detaches session notifications when the inspector's owning exploration closes.</summary>
    public void Dispose() => _session.Changed -= SessionChanged;
}
