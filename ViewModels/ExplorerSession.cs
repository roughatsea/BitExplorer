using BitExplorer.Core;
using WpfApp1.Infrastructure;

namespace WpfApp1.ViewModels;

public enum InterpretationOrder { FileOrder, ReverseBytes, SavedField }

/// <summary>Owns the document, source-bit membership and the independently ordered interpretation.</summary>
public sealed class ExplorerSession : ObservableObject, IDisposable
{
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;
    private DocumentModel _document;
    private string _displayName;
    private Guid? _selectedFieldId;
    private InterpretationOrder _interpretationOrder;
    private bool _updatingSelection;
    private bool _isBusy;
    private bool _disposed;

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

    public DocumentModel Document => _document;
    public BitSelection Selection { get; }
    public NamedField? SelectedField => _document.Fields.FirstOrDefault(item => item.Id == _selectedFieldId);
    public string DisplayName
    {
        get => _displayName;
        set { if (SetProperty(ref _displayName, value)) PublishChange(); }
    }
    public bool IsBusy
    {
        get => _isBusy;
        set { if (SetProperty(ref _isBusy, value)) PublishChange(); }
    }
    public InterpretationOrder InterpretationOrder
    {
        get => _interpretationOrder;
        set
        {
            if (!Enum.IsDefined(value) || value == InterpretationOrder.SavedField && SelectedField is null) return;
            if (SetProperty(ref _interpretationOrder, value)) PublishChange();
        }
    }

    public event EventHandler? Changed;
    public event EventHandler? DocumentReplaced;

    public void ReplaceDocument(DocumentModel document, string displayName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(document);
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
        finally { _updatingSelection = false; }
        PublishChange();
    }

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

    public IReadOnlyList<long> GetInterpretationBits()
    {
        if (_interpretationOrder == InterpretationOrder.SavedField && SelectedField is { } field)
            return field.OrderedBits.ToArray();
        var bits = Selection.Bits.Order();
        return _interpretationOrder == InterpretationOrder.ReverseBytes
            ? bits.GroupBy(bit => bit / 8).OrderByDescending(group => group.Key).SelectMany(group => group).ToArray()
            : bits.ToArray();
    }

    private void SelectionChanged(object? sender, EventArgs e)
    {
        if (_updatingSelection) return;
        ClearSelectedField();
        PublishChange();
    }

    private void ClearSelectedField()
    {
        _selectedFieldId = null;
        if (_interpretationOrder == InterpretationOrder.SavedField) _interpretationOrder = InterpretationOrder.FileOrder;
    }

    private void DocumentChanged(object? sender, EventArgs e)
    {
        if (_context is not null && SynchronizationContext.Current != _context)
        {
            _context.Post(_ => SynchronizeDocument(sender), null);
            return;
        }
        SynchronizeDocument(sender);
    }

    private void SynchronizeDocument(object? sender)
    {
        if (_disposed || !ReferenceEquals(sender, _document)) return;
        if (_selectedFieldId.HasValue)
        {
            if (SelectedField is not { } field) ClearSelectedField();
            else if (!Selection.Bits.Order().SequenceEqual(field.OrderedBits.Order()))
            {
                _updatingSelection = true;
                try { Selection.SetSelection(field.OrderedBits); }
                finally { _updatingSelection = false; }
            }
        }
        PublishChange();
    }

    private void PublishChange()
    {
        if (_disposed) return;
        OnPropertyChanged(null);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _document.Changed -= DocumentChanged;
        Selection.Changed -= SelectionChanged;
    }
}
