using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using BitExplorer.Core;

namespace WpfApp1.Controls;

public sealed class ByteEditEventArgs(int offset) : EventArgs
{
    public int Offset { get; } = offset;
}

/// <summary>WPF input, lifecycle, and scrolling adapter for the virtual byte/bit view.</summary>
public sealed class ByteGrid : FrameworkElement, IScrollInfo
{
    public const int MaximumSelectedBits = BitSelection.MaximumSelectedBits;
    private readonly ByteGridLayout _layout = new();
    private readonly ByteGridRenderer _renderer;
    private readonly ToolTip _hoverTip = new() { Placement = PlacementMode.Mouse, StaysOpen = true };
    private DocumentModel? _document;
    private BitSelection _selection = new();
    private bool _dragging;
    private bool _dragUsesBytes;
    private HashSet<long>? _dragOriginal;
    private int _resizeColumn = -1;
    private double _resizeStartX;
    private double _resizeStartWidth;
    private long _tooltipKey = -2;
    private bool _layoutNotificationPending;

    public ByteGrid()
    {
        _renderer = new ByteGridRenderer(_layout);
        _selection.Changed += OnSelectionChanged;
        _selection.SelectionLimitReached += OnSelectionLimitReached;
        Focusable = true;
        ClipToBounds = true;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
        ToolTipService.SetInitialShowDelay(this, 200);
        Loaded += (_, _) => Refresh();
        Unloaded += (_, _) => HideTooltip();
    }

    public DocumentModel? Document
    {
        get => _document;
        set
        {
            if (ReferenceEquals(_document, value)) return;
            if (_document is not null) _document.Changed -= DocumentChanged;
            _document = value;
            if (_document is not null) _document.Changed += DocumentChanged;
            EndPointerGesture();
            _selection.SetDocumentLength(_document?.Data.LongLength * 8 ?? 0);
            _selection.Clear();
            _layout.ResetScroll();
            Refresh();
        }
    }

    /// <summary>The session-owned selection, shared with inspectors and named fields.</summary>
    public BitSelection Selection
    {
        get => _selection;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(value, _selection)) return;
            EndPointerGesture();
            _selection.Changed -= OnSelectionChanged;
            _selection.SelectionLimitReached -= OnSelectionLimitReached;
            _selection = value;
            _selection.Changed += OnSelectionChanged;
            _selection.SelectionLimitReached += OnSelectionLimitReached;
            _selection.SetDocumentLength(Document?.Data.LongLength * 8 ?? 0);
            HideTooltip();
            InvalidateVisual();
        }
    }

    public IReadOnlyCollection<long> SelectedBits => _selection.Bits;
    public int ActiveByteOffset => _selection.ActiveByteOffset;
    public event EventHandler? SelectionChanged;
    public event EventHandler? SelectionLimitReached;
    public event EventHandler<ByteEditEventArgs>? EditRequested;
    public event EventHandler? LayoutSettingsChanged;

    public void Refresh()
    {
        _selection.SetDocumentLength(Document?.Data.LongLength * 8 ?? 0);
        _renderer.Refresh(Document);
        UpdateLayoutMetrics();
        HideTooltip();
        InvalidateMeasure();
        InvalidateVisual();
    }

    public void SetSelection(IEnumerable<long> bits, bool scrollIntoView = true)
    {
        if (_selection.SetSelection(bits) && scrollIntoView) ScrollToByte(ActiveByteOffset);
    }

    public void ScrollToByte(int offset)
    {
        _layout.ScrollToByte(offset);
        InvalidateScroll();
    }

    private void DocumentChanged(object? sender, EventArgs args)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.InvokeAsync(Refresh); return; }
        Refresh();
    }

    private void OnSelectionChanged(object? sender, EventArgs args)
    {
        HideTooltip();
        InvalidateVisual();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSelectionLimitReached(object? sender, EventArgs args)
    {
        EndPointerGesture();
        SelectionLimitReached?.Invoke(this, EventArgs.Empty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        UpdateLayoutMetrics();
        return new Size(double.IsInfinity(availableSize.Width) ? Math.Min(1100, ExtentWidth) : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 500 : availableSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _layout.SetViewport(finalSize);
        UpdateLayoutMetrics();
        return finalSize;
    }

    private void UpdateLayoutMetrics()
    {
        double characterWidth = _renderer.MeasureCharacterWidth(VisualTreeHelper.GetDpi(this).PixelsPerDip);
        if (_layout.Update(Document, characterWidth)) ScheduleLayoutNotification();
        ScrollOwner?.InvalidateScrollInfo();
    }

    private void ScheduleLayoutNotification()
    {
        if (_layoutNotificationPending) return;
        _layoutNotificationPending = true;
        Dispatcher.BeginInvoke(() =>
        {
            _layoutNotificationPending = false;
            LayoutSettingsChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        _renderer.Render(drawing, _selection, RenderSize, IsKeyboardFocusWithin, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        var point = e.GetPosition(this);
        HideTooltip();
        _resizeColumn = _layout.HitDivider(point);
        if (_resizeColumn >= 0 && Document is not null)
        {
            _resizeStartX = point.X;
            _resizeStartWidth = _layout.GetColumnWidth(_resizeColumn);
            CaptureMouse();
            e.Handled = true;
            return;
        }
        var hit = _layout.HitData(point);
        if (hit.Bit < 0) return;
        if (e.ClickCount == 2)
        {
            _selection.SetActiveBit(hit.Bit);
            EditRequested?.Invoke(this, new ByteEditEventArgs((int)(hit.Bit / 8)));
            e.Handled = true;
            return;
        }
        bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        _dragUsesBytes = hit.WholeByte;
        _dragOriginal = ctrl ? new HashSet<long>(_selection.Bits) : null;
        if (_selection.SelectAt(hit.Bit, hit.WholeByte, shift, ctrl))
        {
            _dragging = true;
            CaptureMouse();
        }
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var point = e.GetPosition(this);
        if (_resizeColumn >= 0 && IsMouseCaptured && Document is not null)
        {
            _layout.ResizeColumn(_resizeColumn, _resizeStartWidth + point.X - _resizeStartX);
            Refresh();
            ScheduleLayoutNotification();
            e.Handled = true;
            return;
        }
        Cursor = _layout.HitDivider(point) >= 0 ? Cursors.SizeWE : Cursors.Arrow;
        if (_dragging && e.LeftButton == MouseButtonState.Pressed)
        {
            if (point.Y < _layout.HeaderHeight) SetVerticalOffset(VerticalOffset - _layout.RowHeight);
            else if (point.Y > RenderSize.Height - 12) SetVerticalOffset(VerticalOffset + _layout.RowHeight);
            var hit = _layout.HitData(point, true);
            if (hit.Bit >= 0 && hit.Bit != _selection.ActiveBit)
                _selection.SelectRange(_selection.Anchor, hit.Bit, _dragUsesBytes, _dragOriginal);
            e.Handled = true;
        }
        else UpdateTooltip(point);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_dragging && _resizeColumn < 0) return;
        EndPointerGesture();
        e.Handled = true;
    }

    private void EndPointerGesture()
    {
        _dragging = false;
        _resizeColumn = -1;
        _dragOriginal = null;
        if (IsMouseCaptured) ReleaseMouseCapture();
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        EndPointerGesture();
        base.OnLostMouseCapture(e);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        HideTooltip();
        base.OnMouseLeave(e);
    }

    private void UpdateTooltip(Point point)
    {
        var description = ByteGridTooltip.Describe(Document, _layout, point);
        if (description is null) { HideTooltip(); return; }
        if (_tooltipKey == description.Value.Key) return;
        _tooltipKey = description.Value.Key;
        _hoverTip.Content = description.Value.Text;
        _hoverTip.PlacementTarget = this;
        _hoverTip.IsOpen = true;
    }

    private void HideTooltip()
    {
        _hoverTip.IsOpen = false;
        _tooltipKey = -2;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Document is null || Document.Data.Length == 0 || e.Handled) return;
        if (e.Key == Key.Enter && ActiveByteOffset >= 0)
        {
            EditRequested?.Invoke(this, new ByteEditEventArgs(ActiveByteOffset));
            e.Handled = true;
            return;
        }
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (ctrl && e.Key == Key.A)
        {
            _selection.SelectRange(0, Document.Data.LongLength * 8 - 1, true);
            e.Handled = true;
            return;
        }
        SelectionNavigation? direction = e.Key switch
        {
            Key.Left => SelectionNavigation.Left, Key.Right => SelectionNavigation.Right,
            Key.Up => SelectionNavigation.Up, Key.Down => SelectionNavigation.Down,
            Key.Home => SelectionNavigation.Home, Key.End => SelectionNavigation.End,
            Key.PageUp => SelectionNavigation.PageUp, Key.PageDown => SelectionNavigation.PageDown,
            _ => null
        };
        if (direction is null) return;
        int visibleRows = Math.Max(1, (int)((ViewportHeight - _layout.HeaderHeight) / _layout.RowHeight));
        _selection.Navigate(direction.Value, _layout.BitView, _layout.BytesPerRow, visibleRows, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift), ctrl);
        ScrollToByte(ActiveByteOffset);
        e.Handled = true;
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnGotKeyboardFocus(e); InvalidateVisual(); }
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnLostKeyboardFocus(e); InvalidateVisual(); }

    public bool CanHorizontallyScroll { get; set; } = true;
    public bool CanVerticallyScroll { get; set; } = true;
    public double ExtentWidth => _layout.Extent.Width;
    public double ExtentHeight => _layout.Extent.Height;
    public double ViewportWidth => _layout.Viewport.Width;
    public double ViewportHeight => _layout.Viewport.Height;
    public double HorizontalOffset => _layout.HorizontalOffset;
    public double VerticalOffset => _layout.VerticalOffset;
    public ScrollViewer? ScrollOwner { get; set; }
    public void LineUp() => SetVerticalOffset(VerticalOffset - _layout.RowHeight);
    public void LineDown() => SetVerticalOffset(VerticalOffset + _layout.RowHeight);
    public void LineLeft() => SetHorizontalOffset(HorizontalOffset - _layout.CellWidth);
    public void LineRight() => SetHorizontalOffset(HorizontalOffset + _layout.CellWidth);
    public void MouseWheelUp() => SetVerticalOffset(VerticalOffset - _layout.RowHeight * 3);
    public void MouseWheelDown() => SetVerticalOffset(VerticalOffset + _layout.RowHeight * 3);
    public void MouseWheelLeft() => SetHorizontalOffset(HorizontalOffset - _layout.CellWidth * 3);
    public void MouseWheelRight() => SetHorizontalOffset(HorizontalOffset + _layout.CellWidth * 3);
    public void PageUp() => SetVerticalOffset(VerticalOffset - Math.Max(_layout.RowHeight, ViewportHeight - _layout.HeaderHeight));
    public void PageDown() => SetVerticalOffset(VerticalOffset + Math.Max(_layout.RowHeight, ViewportHeight - _layout.HeaderHeight));
    public void PageLeft() => SetHorizontalOffset(HorizontalOffset - ViewportWidth);
    public void PageRight() => SetHorizontalOffset(HorizontalOffset + ViewportWidth);

    public void SetHorizontalOffset(double offset)
    {
        _layout.SetHorizontalOffset(offset);
        InvalidateScroll();
    }

    public void SetVerticalOffset(double offset)
    {
        _layout.SetVerticalOffset(offset);
        InvalidateScroll();
    }

    private void InvalidateScroll()
    {
        ScrollOwner?.InvalidateScrollInfo();
        HideTooltip();
        InvalidateVisual();
    }

    public Rect MakeVisible(Visual visual, Rect rectangle) => ReferenceEquals(visual, this) ? rectangle : Rect.Empty;
}
