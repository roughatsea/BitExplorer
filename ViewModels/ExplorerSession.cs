using BitExplorer.Core;
using WpfApp1.Infrastructure;

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
    private DocumentModel _document;
    private string _displayName;
    // A stable ID survives undo/redo, which may replace NamedField object instances.
    private Guid? _selectedFieldId;
    private InterpretationOrder _interpretationOrder;
    // Prevent our own selection updates from being mistaken for a new user selection.
    private bool _updatingSelection;
    private bool _isBusy;
    private bool _disposed;

    /// <summary>Subscribes to model changes and initially selects all eight bits of byte zero, if it exists.</summary>
    public ExplorerSession(DocumentModel document, string displayName)
    {
        _document = document;
        _displayName = displayName;
        Selection = new BitSelection();
        Selection.SetDocumentLength(document.Data.LongLength * 8);
        document.Changed += DocumentChanged;
        Selection.Changed += SelectionChanged;
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
        get => _displayName;
        set { if (SetProperty(ref _displayName, value)) PublishChange(); }
    }
    /// <summary>True while a workflow must prevent edits, for example while exporting the document.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        set { if (SetProperty(ref _isBusy, value)) PublishChange(); }
    }
    /// <summary>Controls value assembly without changing selected addresses or stored bytes.</summary>
    public InterpretationOrder InterpretationOrder
    {
        get => _interpretationOrder;
        set
        {
            if (!Enum.IsDefined(value) || value == InterpretationOrder.SavedField && SelectedField is null) return;
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
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(document);
        // Unsubscribe first: later edits to an old document must not refresh this session.
        _document.Changed -= DocumentChanged;
        _document = document;
        _displayName = displayName;
        _selectedFieldId = null;
        _interpretationOrder = InterpretationOrder.FileOrder;
        _document.Changed += DocumentChanged;
        _updatingSelection = true;
        try
        {
            Selection.Clear();
            Selection.SetDocumentLength(document.Data.LongLength * 8);
            // Views replace their document before the initial selection is restored.
            DocumentReplaced?.Invoke(this, EventArgs.Empty);
            if (!_disposed && document.Data.Length > 0) Selection.SetSelection(Enumerable.Range(0, 8).Select(bit => (long)bit));
        }
        // finally releases the guard even if a subscriber throws during replacement.
        finally { _updatingSelection = false; }
        PublishChange();
    }

    /// <summary>
    /// Enters a label's context: highlight its source bits and use its saved value order.
    /// The selection callback is suppressed because this is a field-driven change.
    /// </summary>
    public void SelectField(Guid id)
    {
        var field = _document.Fields.FirstOrDefault(field => field.Id == id);
        if (field is null) return;
        _updatingSelection = true;
        try
        {
            Selection.SetSelection(field.OrderedBits);
            _selectedFieldId = field.Id;
            _interpretationOrder = InterpretationOrder.SavedField;
        }
        finally { _updatingSelection = false; }
        PublishChange();
    }

    /// <summary>
    /// Selects addresses directly, leaving named-field context even if the addresses
    /// match a field. Rejected oversized selections leave the previous context intact.
    /// </summary>
    public void SelectRawBits(IEnumerable<long> bits)
    {
        _updatingSelection = true;
        try
        {
            if (!Selection.SetSelection(bits)) return;
            ClearSelectedField();
        }
        finally { _updatingSelection = false; }
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
            return field.OrderedBits.ToArray();
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
        if (_updatingSelection) return;
        ClearSelectedField();
        PublishChange();
    }

    /// <summary>Clears label context while retaining an explicitly chosen file or reverse-byte order.</summary>
    private void ClearSelectedField()
    {
        _selectedFieldId = null;
        if (_interpretationOrder == InterpretationOrder.SavedField) _interpretationOrder = InterpretationOrder.FileOrder;
    }

    /// <summary>Moves notifications back to the original UI context when a model event arrives elsewhere.</summary>
    private void DocumentChanged(object? sender, EventArgs e)
    {
        if (_context is not null && SynchronizationContext.Current != _context)
        {
            // Post queues work rather than blocking the worker until the UI is free.
            _context.Post(_ => SynchronizeDocument(sender), null);
            return;
        }
        SynchronizeDocument(sender);
    }

    /// <summary>
    /// Reconciles a model edit with field selection: deletion leaves field context,
    /// and a changed field map updates its highlighted addresses. Undo and redo use
    /// this same path, so the inspector follows restored mappings automatically.
    /// </summary>
    private void SynchronizeDocument(object? sender)
    {
        if (_disposed || !ReferenceEquals(sender, _document)) return;
        if (_selectedFieldId.HasValue)
        {
            if (SelectedField is not { } field) ClearSelectedField();
            // Sorting both sides compares membership, not the field's value order.
            else if (!Selection.Bits.Order().SequenceEqual(field.OrderedBits.Order()))
            {
                _updatingSelection = true;
                try { Selection.SetSelection(field.OrderedBits); }
                finally { _updatingSelection = false; }
            }
        }
        PublishChange();
    }

    /// <summary>Notifies both WPF property bindings and panel subscribers after shared state is consistent.</summary>
    private void PublishChange()
    {
        if (_disposed) return;
        // A null property name means all bound properties may need to be read again.
        OnPropertyChanged(null);
        // ?.Invoke raises the event only if at least one listener is subscribed.
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Unsubscribes events and makes queued notifications harmless when the exploration closes.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _document.Changed -= DocumentChanged;
        Selection.Changed -= SelectionChanged;
    }
}
