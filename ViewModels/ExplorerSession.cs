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
/// FileOrder assembles bits by physical address. ReverseBytes reverses byte groups
/// while retaining bit order inside each byte. SavedField follows the label's explicit map.
/// </summary>
public enum InterpretationOrder { FileOrder, ReverseBytes, SavedField }

/// <summary>
/// The single shared state for an open exploration. The grid and all view models
/// use this same document and selection instead of keeping competing copies.
/// Selected physical bits describe where data lives; interpretation order describes
/// how those bits form a number. For example, selected addresses {0, 9} can form a
/// value in the order [9, 0] without moving either source bit in the file.
/// </summary>
public sealed class ExplorerSession : ObservableObject, IDisposable
{
    // Remember the creating thread's notification context, normally WPF's UI context.
    // Tests without a UI context receive notifications directly instead.
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;
    // Reserve _document to hold an object or value of type DocumentModel; setup can supply its value,
    // otherwise the type's default is used.
    private DocumentModel _document;
    // Reserve _displayName to hold text; setup can supply its value, otherwise the type's default is used.
    private string _displayName;
    // A stable ID survives undo/redo, which may replace NamedField object instances.
    private Guid? _selectedFieldId;
    // Reserve _interpretationOrder to hold an object or value of type InterpretationOrder; setup can supply
    // its value, otherwise the type's default is used.
    private InterpretationOrder _interpretationOrder;
    // Prevent our own selection updates from being mistaken for a new user selection.
    private bool _updatingSelection;
    // Reserve _isBusy to hold a true-or-false answer; setup can supply its value, otherwise the type's
    // default is used.
    private bool _isBusy;
    // Reserve _disposed to hold a true-or-false answer; setup can supply its value, otherwise the type's
    // default is used.
    private bool _disposed;

    /// <summary>Subscribes to model changes and initially selects all eight bits of byte zero, if it exists.</summary>
    public ExplorerSession(DocumentModel document, string displayName)
    {
        // Set _document to document.
        _document = document;
        // Set _displayName to displayName.
        _displayName = displayName;
        // Set Selection to a new BitSelection object.
        Selection = new BitSelection();
        // Call Selection.SetDocumentLength: Sets the valid address range and removes any selection left
        // beyond its end.
        Selection.SetDocumentLength(document.Data.LongLength * 8);
        // Register DocumentChanged as a listener for document.Changed; the listener runs when that event is
        // raised.
        document.Changed += DocumentChanged;
        // Register SelectionChanged as a listener for Selection.Changed; the listener runs when that event
        // is raised.
        Selection.Changed += SelectionChanged;
        // If document.Data.Length is greater than 0, request selection of the supplied physical bit
        // addresses; the selection code checks its bounds and limit.
        if (document.Data.Length > 0) Selection.SetSelection(Enumerable.Range(0, 8).Select(bit => (long)bit));
    }

    /// <summary>The active binary, field definitions, display settings and undo history.</summary>
    public DocumentModel Document => _document;
    /// <summary>The one shared set of selected source addresses, including its active byte and range anchor.</summary>
    public BitSelection Selection { get; }
    /// <summary>Looks up the current field instance by ID; returns null after deletion or raw selection.</summary>
    public NamedField? SelectedField => _document.Fields.FirstOrDefault(item => item.Id == _selectedFieldId);
    /// <summary>The filename or descriptive name shown in the header and window title.</summary>
    public string DisplayName
    {
        // When this property is read, return _displayName.
        get => _displayName;
        // When this property is written, the proposed new content is called value; run this setter to
        // decide how to store it.
        set { if (SetProperty(ref _displayName, value)) PublishChange(); }
    }
    /// <summary>True while a workflow must prevent edits, for example while exporting the document.</summary>
    public bool IsBusy
    {
        // When this property is read, return _isBusy.
        get => _isBusy;
        // When this property is written, the proposed new content is called value; run this setter to
        // decide how to store it.
        set { if (SetProperty(ref _isBusy, value)) PublishChange(); }
    }
    /// <summary>Controls value assembly without changing selected addresses or stored bytes.</summary>
    public InterpretationOrder InterpretationOrder
    {
        // When this property is read, return _interpretationOrder.
        get => _interpretationOrder;
        // When this property is written, the proposed new content is called value; run this setter to
        // decide how to store it.
        set
        {
            // If it is not the case that Enum.IsDefined(value) is true, or both value equals
            // InterpretationOrder.SavedField and SelectedField matches null, leave this method immediately.
            if (!Enum.IsDefined(value) || value == InterpretationOrder.SavedField && SelectedField is null) return;
            // If SetProperty(ref _interpretationOrder, value) is true, call PublishChange: Notifies both
            // WPF property bindings and panel subscribers after shared state is consistent.
            if (SetProperty(ref _interpretationOrder, value)) PublishChange();
        }
    }

    /// <summary>Signals that panels should reread shared state after a completed session update.</summary>
    public event EventHandler? Changed;
    /// <summary>Lets the grid attach the replacement document before the first selection is restored.</summary>
    public event EventHandler? DocumentReplaced;

    /// <summary>
    /// Detaches the old model, installs the new one, and resets selection. The guard
    /// suppresses intermediate selection events so panels see the finished transition.
    /// </summary>
    public void ReplaceDocument(DocumentModel document, string displayName)
    {
        // Call ObjectDisposedException.ThrowIf(...); the values in parentheses are the inputs.
        ObjectDisposedException.ThrowIf(_disposed, this);
        // Reject document immediately if it is null (no object was supplied).
        ArgumentNullException.ThrowIfNull(document);
        // Unsubscribe first: later edits to an old document must not refresh this session.
        _document.Changed -= DocumentChanged;
        // Set _document to document.
        _document = document;
        // Set _displayName to displayName.
        _displayName = displayName;
        // Set _selectedFieldId to null (no value).
        _selectedFieldId = null;
        // Set _interpretationOrder to InterpretationOrder.FileOrder.
        _interpretationOrder = InterpretationOrder.FileOrder;
        // Register DocumentChanged as a listener for _document.Changed; the listener runs when that event
        // is raised.
        _document.Changed += DocumentChanged;
        // Set _updatingSelection to true (yes).
        _updatingSelection = true;
        // Attempt this work. A matching catch below handles a reported exception; a finally block, when
        // present, performs cleanup on the way out.
        try
        {
            // Remove all current items from Selection.
            Selection.Clear();
            // Call Selection.SetDocumentLength: Sets the valid address range and removes any selection left
            // beyond its end.
            Selection.SetDocumentLength(document.Data.LongLength * 8);
            // Views replace their document before the initial selection is restored.
            DocumentReplaced?.Invoke(this, EventArgs.Empty);
            // If both it is not the case that _disposed is true and document.Data.Length is greater than 0,
            // request selection of the supplied physical bit addresses; the selection code checks its
            // bounds and limit.
            if (!_disposed && document.Data.Length > 0) Selection.SetSelection(Enumerable.Range(0, 8).Select(bit => (long)bit));
        }
        // finally releases the guard even if a subscriber throws during replacement.
        finally { _updatingSelection = false; }
        // Call PublishChange: Notifies both WPF property bindings and panel subscribers after shared state
        // is consistent.
        PublishChange();
    }

    /// <summary>
    /// Enters a label's context: highlight its source bits and use its saved value order.
    /// The selection callback is suppressed because this is a field-driven change.
    /// </summary>
    public void SelectField(Guid id)
    {
        // Remember the first matching item, or the type's default (usually null here) when none matches as
        // field.
        var field = _document.Fields.FirstOrDefault(field => field.Id == id);
        // If field matches null, leave this method immediately.
        if (field is null) return;
        // Set _updatingSelection to true (yes).
        _updatingSelection = true;
        // Attempt this work. A matching catch below handles a reported exception; a finally block, when
        // present, performs cleanup on the way out.
        try
        {
            // Request selection of the supplied physical bit addresses; the selection code checks its
            // bounds and limit.
            Selection.SetSelection(field.OrderedBits);
            // Set _selectedFieldId to field.Id.
            _selectedFieldId = field.Id;
            // Set _interpretationOrder to InterpretationOrder.SavedField.
            _interpretationOrder = InterpretationOrder.SavedField;
        }
        // Run this cleanup when leaving the try, whether the work succeeded or reported an exception.
        finally { _updatingSelection = false; }
        // Call PublishChange: Notifies both WPF property bindings and panel subscribers after shared state
        // is consistent.
        PublishChange();
    }

    /// <summary>
    /// Selects addresses directly, leaving named-field context even if the addresses
    /// match a field. Rejected oversized selections leave the previous context intact.
    /// </summary>
    public void SelectRawBits(IEnumerable<long> bits)
    {
        // Set _updatingSelection to true (yes).
        _updatingSelection = true;
        // Attempt this work. A matching catch below handles a reported exception; a finally block, when
        // present, performs cleanup on the way out.
        try
        {
            // If it is not the case that Selection.SetSelection(bits) is true, leave this method
            // immediately.
            if (!Selection.SetSelection(bits)) return;
            // Call ClearSelectedField: Clears label context while retaining an explicitly chosen file or
            // reverse-byte order.
            ClearSelectedField();
        }
        // Run this cleanup when leaving the try, whether the work succeeded or reported an exception.
        finally { _updatingSelection = false; }
        // Call PublishChange: Notifies both WPF property bindings and panel subscribers after shared state
        // is consistent.
        PublishChange();
    }

    /// <summary>
    /// Returns copied addresses from the interpreted most significant bit (MSB,
    /// highest numeric weight) to least significant bit (LSB, lowest numeric weight).
    /// </summary>
    public IReadOnlyList<long> GetInterpretationBits()
    {
        // "is { } field" means the selected field is not null and names it locally.
        if (_interpretationOrder == InterpretationOrder.SavedField && SelectedField is { } field)
            // Return a separate array containing field.OrderedBits's items to the caller and leave this
            // method.
            return field.OrderedBits.ToArray();
        // Remember the items from Selection.Bits in ascending order as bits.
        var bits = Selection.Bits.Order();
        // Integer division by eight finds a byte's offset. GroupBy collects bits in
        // each byte; SelectMany joins the reversed groups into one ordered list.
        return _interpretationOrder == InterpretationOrder.ReverseBytes
            ? bits.GroupBy(bit => bit / 8).OrderByDescending(group => group.Key).SelectMany(group => group).ToArray()
            : bits.ToArray();
    }

    /// <summary>Handles direct grid selection changes, which leave the previously selected label.</summary>
    private void SelectionChanged(object? sender, EventArgs e)
    {
        // If _updatingSelection is true, leave this method immediately.
        if (_updatingSelection) return;
        // Call ClearSelectedField: Clears label context while retaining an explicitly chosen file or
        // reverse-byte order.
        ClearSelectedField();
        // Call PublishChange: Notifies both WPF property bindings and panel subscribers after shared state
        // is consistent.
        PublishChange();
    }

    /// <summary>Clears label context while retaining an explicitly chosen file or reverse-byte order.</summary>
    private void ClearSelectedField()
    {
        // Set _selectedFieldId to null (no value).
        _selectedFieldId = null;
        // If _interpretationOrder equals InterpretationOrder.SavedField, set _interpretationOrder to
        // InterpretationOrder.FileOrder.
        if (_interpretationOrder == InterpretationOrder.SavedField) _interpretationOrder = InterpretationOrder.FileOrder;
    }

    /// <summary>Moves notifications back to the original UI context when a model event arrives elsewhere.</summary>
    private void DocumentChanged(object? sender, EventArgs e)
    {
        // If both _context matches not null and SynchronizationContext.Current differs from _context, run
        // the following grouped instructions.
        if (_context is not null && SynchronizationContext.Current != _context)
        {
            // Post queues work rather than blocking the worker until the UI is free.
            _context.Post(_ => SynchronizeDocument(sender), null);
            // Leave this method immediately.
            return;
        }
        // Call SynchronizeDocument: Reconciles a model edit with field selection: deletion leaves field
        // context, and a changed field map updates its highlighted addresses.
        SynchronizeDocument(sender);
    }

    /// <summary>
    /// Reconciles a model edit with field selection: deletion leaves field context,
    /// and a changed field map updates its highlighted addresses. Undo and redo use
    /// this same path, so the inspector follows restored mappings automatically.
    /// </summary>
    private void SynchronizeDocument(object? sender)
    {
        // If _disposed is true, or it is not the case that ReferenceEquals(sender, _document) is true,
        // leave this method immediately.
        if (_disposed || !ReferenceEquals(sender, _document)) return;
        // If _selectedFieldId.HasValue is true, run the following grouped instructions.
        if (_selectedFieldId.HasValue)
        {
            // If SelectedField matches not { } field, call ClearSelectedField: Clears label context while
            // retaining an explicitly chosen file or reverse-byte order.
            if (SelectedField is not { } field) ClearSelectedField();
            // Sorting both sides compares membership, not the field's value order.
            else if (!Selection.Bits.Order().SequenceEqual(field.OrderedBits.Order()))
            {
                // Set _updatingSelection to true (yes).
                _updatingSelection = true;
                // Attempt this work. A matching catch below handles a reported exception; a finally block,
                // when present, performs cleanup on the way out.
                try { Selection.SetSelection(field.OrderedBits); }
                // Run this cleanup when leaving the try, whether the work succeeded or reported an
                // exception.
                finally { _updatingSelection = false; }
            }
        }
        // Call PublishChange: Notifies both WPF property bindings and panel subscribers after shared state
        // is consistent.
        PublishChange();
    }

    /// <summary>Notifies both WPF property bindings and panel subscribers after shared state is consistent.</summary>
    private void PublishChange()
    {
        // If _disposed is true, leave this method immediately.
        if (_disposed) return;
        // A null property name means all bound properties may need to be read again.
        OnPropertyChanged(null);
        // ?.Invoke raises the event only if at least one listener is subscribed.
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Unsubscribes events and makes queued notifications harmless when the exploration closes.</summary>
    public void Dispose()
    {
        // If _disposed is true, leave this method immediately.
        if (_disposed) return;
        // Set _disposed to true (yes).
        _disposed = true;
        // Stop sending _document.Changed notifications to DocumentChanged.
        _document.Changed -= DocumentChanged;
        // Stop sending Selection.Changed notifications to SelectionChanged.
        Selection.Changed -= SelectionChanged;
    }
}
