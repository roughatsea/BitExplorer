using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using BitExplorer.Core;

namespace WpfApp1.Controls;

/// <summary>
/// Carries the byte's file position when the grid asks the window to open an editor.
/// An event is a notification: the grid reports the request, and the window decides how to handle it.
/// </summary>
public sealed class ByteEditEventArgs(int offset) : EventArgs
{
    /// <summary>Zero-based byte position; offset 0 means the first byte in the file.</summary>
    public int Offset { get; } = offset;
}

/// <summary>
/// Connects Windows Presentation Foundation (WPF) mouse, keyboard, drawing, and scrolling events
/// to the explorer. It delegates selection rules to BitSelection, positions to ByteGridLayout,
/// and drawing to ByteGridRenderer rather than implementing those jobs itself.
/// </summary>
/// <remarks>
/// This is a FrameworkElement, a WPF element that draws its own contents. It does not create a
/// separate control for every byte. IScrollInfo lets the surrounding ScrollViewer ask it to move
/// through a potentially large document without creating controls for all the off-screen data.
/// </remarks>
public sealed class ByteGrid : FrameworkElement, IScrollInfo
{
    /// <summary>Compatibility name for the shared selection limit: 65,536 bits, or 8 KiB.</summary>
    public const int MaximumSelectedBits = BitSelection.MaximumSelectedBits;
    // These collaborators each own one job. Sharing _layout keeps drawn positions and clickable
    // positions identical; sharing _selection keeps the grid and the inspector in agreement.
    private readonly ByteGridLayout _layout = new();
    private readonly ByteGridRenderer _renderer;
    private readonly ToolTip _hoverTip = new() { Placement = PlacementMode.Mouse, StaysOpen = true };
    private DocumentModel? _document;
    private BitSelection _selection = new();
    // Only the temporary details of the current mouse gesture belong to the control.
    // Persistent selected bits and their navigation anchors belong to BitSelection instead.
    private bool _dragging;
    private bool _dragUsesBytes;
    private HashSet<long>? _dragOriginal;
    private int _resizeColumn = -1;
    private double _resizeStartX;
    private double _resizeStartWidth;
    private long _tooltipKey = -2;
    private bool _layoutNotificationPending;

    /// <summary>Sets up drawing preferences, selection notifications, and WPF load/unload behavior.</summary>
    public ByteGrid()
    {
        _renderer = new ByteGridRenderer(_layout);
        _selection.Changed += OnSelectionChanged;
        _selection.SelectionLimitReached += OnSelectionLimitReached;
        // Receive keyboard input, keep drawing inside the control, and align text/lines with the
        // display's pixel grid. ClearType is WPF's text-smoothing mode for readable small type.
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

    /// <summary>
    /// The data being displayed. Replacing it detaches the old change handler, clears the old
    /// selection, returns scrolling to the start, and recalculates the display for the new file.
    /// </summary>
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

    /// <summary>
    /// The session-owned selection shared with inspectors and named fields. Assign the document
    /// before attaching this object so that selected positions can be checked against the right file.
    /// </summary>
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

    /// <summary>Selected physical bit addresses; callers can read them but cannot mutate this collection.</summary>
    public IReadOnlyCollection<long> SelectedBits => _selection.Bits;
    /// <summary>The byte containing the navigation cursor, or -1 when there is no active byte.</summary>
    public int ActiveByteOffset => _selection.ActiveByteOffset;
    /// <summary>Notifies existing grid consumers after the shared selection changes.</summary>
    public event EventHandler? SelectionChanged;
    /// <summary>Reports that a requested selection was too large and was left unchanged.</summary>
    public event EventHandler? SelectionLimitReached;
    /// <summary>Asks the containing window to focus its byte editor.</summary>
    public event EventHandler<ByteEditEventArgs>? EditRequested;
    /// <summary>Reports display settings changed by dragging a divider or automatically fitting rows.</summary>
    public event EventHandler? LayoutSettingsChanged;

    /// <summary>
    /// Refreshes cached annotation resources and geometry after document or display changes.
    /// Invalidating measure/visual asks WPF to schedule layout and drawing; it does not redraw immediately.
    /// </summary>
    public void Refresh()
    {
        _selection.SetDocumentLength(Document?.Data.LongLength * 8 ?? 0);
        _renderer.Refresh(Document);
        UpdateLayoutMetrics();
        HideTooltip();
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Replaces selected bits using the shared rules, and optionally reveals the active byte.</summary>
    public void SetSelection(IEnumerable<long> bits, bool scrollIntoView = true)
    {
        if (_selection.SetSelection(bits) && scrollIntoView) ScrollToByte(ActiveByteOffset);
    }

    /// <summary>Moves only far enough to bring a byte's row into the visible body of the grid.</summary>
    public void ScrollToByte(int offset)
    {
        _layout.ScrollToByte(offset);
        InvalidateScroll();
    }

    /// <summary>
    /// Responds to model changes on the UI thread. Dispatcher is WPF's queue for work that must
    /// touch UI objects; a background notification must enter that queue before refreshing the grid.
    /// </summary>
    private void DocumentChanged(object? sender, EventArgs args)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.InvokeAsync(Refresh); return; }
        Refresh();
    }

    /// <summary>Removes stale hover text, redraws selection highlights, and forwards the notification.</summary>
    private void OnSelectionChanged(object? sender, EventArgs args)
    {
        HideTooltip();
        InvalidateVisual();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Stops an oversized drag while retaining the last successfully selected bits.</summary>
    private void OnSelectionLimitReached(object? sender, EventArgs args)
    {
        EndPointerGesture();
        SelectionLimitReached?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// WPF's measure step asks how much space this element would like. A scroll container may
    /// offer unlimited space, so finite fallback sizes keep the requested window size reasonable.
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        UpdateLayoutMetrics();
        return new Size(double.IsInfinity(availableSize.Width) ? Math.Min(1100, ExtentWidth) : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 500 : availableSize.Height);
    }

    /// <summary>
    /// WPF's arrange step supplies the space actually assigned to the grid. That rectangle becomes
    /// the viewport: the window through which the much larger document is visible.
    /// </summary>
    protected override Size ArrangeOverride(Size finalSize)
    {
        _layout.SetViewport(finalSize);
        UpdateLayoutMetrics();
        return finalSize;
    }

    /// <summary>
    /// Measures the actual monospace glyph width at the current screen scale, updates geometry,
    /// and tells the ScrollViewer that the content size or scroll position may have changed.
    /// </summary>
    private void UpdateLayoutMetrics()
    {
        double characterWidth = _renderer.MeasureCharacterWidth(VisualTreeHelper.GetDpi(this).PixelsPerDip);
        if (_layout.Update(Document, characterWidth)) ScheduleLayoutNotification();
        ScrollOwner?.InvalidateScrollInfo();
    }

    /// <summary>
    /// Combines repeated layout changes into one later notification. Deferring it avoids changing
    /// bound controls while WPF is still inside its current measurement or arrangement pass.
    /// </summary>
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

    /// <summary>WPF calls this when painting is needed; the renderer draws only the visible rows.</summary>
    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        _renderer.Render(drawing, _selection, RenderSize, IsKeyboardFocusWithin, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }

    /// <summary>
    /// Starts a divider resize or data selection. Shift extends a range, Ctrl toggles/adds bits,
    /// and a double-click requests editing. Hit testing means translating a pointer position into data.
    /// </summary>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        var point = e.GetPosition(this);
        HideTooltip();
        // Handled events below stop traveling through the parent controls, preventing the same click
        // from also starting another action in the surrounding UI.
        _resizeColumn = _layout.HitDivider(point);
        if (_resizeColumn >= 0 && Document is not null)
        {
            _resizeStartX = point.X;
            _resizeStartWidth = _layout.GetColumnWidth(_resizeColumn);
            // Mouse capture keeps delivering this gesture's events even if the pointer leaves the grid.
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
        // Keep the selection from BEFORE the drag so Ctrl-drag can preserve separate earlier groups.
        // This is a gesture snapshot, not a second owner of the live selection.
        _dragOriginal = ctrl ? new HashSet<long>(_selection.Bits) : null;
        if (_selection.SelectAt(hit.Bit, hit.WholeByte, shift, ctrl))
        {
            _dragging = true;
            CaptureMouse();
        }
        e.Handled = true;
    }

    /// <summary>
    /// Continues a captured resize/selection, scrolls near the top or bottom while dragging,
    /// or shows hover information when no selection gesture is underway.
    /// </summary>
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
            // Clamp keeps an out-of-bounds drag pointed at the nearest valid bit instead of losing it.
            var hit = _layout.HitData(point, true);
            if (hit.Bit >= 0 && hit.Bit != _selection.ActiveBit)
                _selection.SelectRange(_selection.Anchor, hit.Bit, _dragUsesBytes, _dragOriginal);
            e.Handled = true;
        }
        else UpdateTooltip(point);
    }

    /// <summary>Finishes the gesture when the left mouse button is released.</summary>
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_dragging && _resizeColumn < 0) return;
        EndPointerGesture();
        e.Handled = true;
    }

    /// <summary>Clears transient drag/resize state and releases any mouse capture owned by the grid.</summary>
    private void EndPointerGesture()
    {
        _dragging = false;
        _resizeColumn = -1;
        _dragOriginal = null;
        if (IsMouseCaptured) ReleaseMouseCapture();
    }

    /// <summary>Also ends a gesture if WPF or another control takes mouse capture away unexpectedly.</summary>
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        EndPointerGesture();
        base.OnLostMouseCapture(e);
    }

    /// <summary>Hides hover text when the pointer leaves; captured dragging is handled separately.</summary>
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        HideTooltip();
        base.OnMouseLeave(e);
    }

    /// <summary>Asks the tooltip builder for a description, and reopens the popup only when its target changes.</summary>
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

    /// <summary>Closes hover text and clears its key so the same target can be described freshly next time.</summary>
    private void HideTooltip()
    {
        _hoverTip.IsOpen = false;
        _tooltipKey = -2;
    }

    /// <summary>
    /// Maps WPF keys to UI-independent selection operations. The selection model performs the
    /// range arithmetic and limit checks; this adapter handles Enter and marks handled keys.
    /// </summary>
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

    /// <summary>Redraws the active-byte/bit outline when the grid becomes the keyboard input target.</summary>
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnGotKeyboardFocus(e); InvalidateVisual(); }
    /// <summary>Redraws that outline when keyboard focus moves elsewhere.</summary>
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnLostKeyboardFocus(e); InvalidateVisual(); }

    // IScrollInfo properties describe the complete content (extent), the visible window (viewport),
    // and how far that window has moved (offset). Distances use WPF's device-independent units:
    // one unit is 1/96 inch at normal scaling, rather than necessarily one physical monitor pixel.
    /// <summary>Lets the surrounding scroll container advertise horizontal scrolling support.</summary>
    public bool CanHorizontallyScroll { get; set; } = true;
    /// <summary>Lets the surrounding scroll container advertise vertical scrolling support.</summary>
    public bool CanVerticallyScroll { get; set; } = true;
    /// <summary>Full content width/height, including off-screen columns and rows.</summary>
    public double ExtentWidth => _layout.Extent.Width;
    /// <summary>Full content height, including the fixed header and every document row.</summary>
    public double ExtentHeight => _layout.Extent.Height;
    /// <summary>Width of the visible rectangle WPF assigned to the grid.</summary>
    public double ViewportWidth => _layout.Viewport.Width;
    /// <summary>Height of that visible rectangle, including its fixed header.</summary>
    public double ViewportHeight => _layout.Viewport.Height;
    /// <summary>Distance scrolled right from the content's left edge.</summary>
    public double HorizontalOffset => _layout.HorizontalOffset;
    /// <summary>Distance scrolled down through the document rows.</summary>
    public double VerticalOffset => _layout.VerticalOffset;
    /// <summary>The ScrollViewer to notify when scroll bars need updated sizes or positions.</summary>
    public ScrollViewer? ScrollOwner { get; set; }
    /// <summary>Scrolls upward by one displayed row.</summary>
    public void LineUp() => SetVerticalOffset(VerticalOffset - _layout.RowHeight);
    /// <summary>Scrolls downward by one displayed row.</summary>
    public void LineDown() => SetVerticalOffset(VerticalOffset + _layout.RowHeight);
    /// <summary>Scrolls left by one byte token, or one group of eight displayed bits.</summary>
    public void LineLeft() => SetHorizontalOffset(HorizontalOffset - _layout.CellWidth);
    /// <summary>Scrolls right by one byte token, or one group of eight displayed bits.</summary>
    public void LineRight() => SetHorizontalOffset(HorizontalOffset + _layout.CellWidth);
    /// <summary>Moves three rows upward for one upward wheel step.</summary>
    public void MouseWheelUp() => SetVerticalOffset(VerticalOffset - _layout.RowHeight * 3);
    /// <summary>Moves three rows downward for one downward wheel step.</summary>
    public void MouseWheelDown() => SetVerticalOffset(VerticalOffset + _layout.RowHeight * 3);
    /// <summary>Moves three byte cells left for a horizontal wheel step.</summary>
    public void MouseWheelLeft() => SetHorizontalOffset(HorizontalOffset - _layout.CellWidth * 3);
    /// <summary>Moves three byte cells right for a horizontal wheel step.</summary>
    public void MouseWheelRight() => SetHorizontalOffset(HorizontalOffset + _layout.CellWidth * 3);
    /// <summary>Moves upward by a body-sized page, excluding the stationary header.</summary>
    public void PageUp() => SetVerticalOffset(VerticalOffset - Math.Max(_layout.RowHeight, ViewportHeight - _layout.HeaderHeight));
    /// <summary>Moves downward by a body-sized page, excluding the stationary header.</summary>
    public void PageDown() => SetVerticalOffset(VerticalOffset + Math.Max(_layout.RowHeight, ViewportHeight - _layout.HeaderHeight));
    /// <summary>Moves left by one visible viewport width.</summary>
    public void PageLeft() => SetHorizontalOffset(HorizontalOffset - ViewportWidth);
    /// <summary>Moves right by one visible viewport width.</summary>
    public void PageRight() => SetHorizontalOffset(HorizontalOffset + ViewportWidth);

    /// <summary>Clamps a requested horizontal position to the content and refreshes scrolling feedback.</summary>
    public void SetHorizontalOffset(double offset)
    {
        _layout.SetHorizontalOffset(offset);
        InvalidateScroll();
    }

    /// <summary>Clamps a requested vertical position to the content and refreshes scrolling feedback.</summary>
    public void SetVerticalOffset(double offset)
    {
        _layout.SetVerticalOffset(offset);
        InvalidateScroll();
    }

    /// <summary>Updates scroll bars, dismisses position-dependent hover text, and requests repainting.</summary>
    private void InvalidateScroll()
    {
        ScrollOwner?.InvalidateScrollInfo();
        HideTooltip();
        InvalidateVisual();
    }

    /// <summary>
    /// Implements WPF's request to reveal a child visual. Bytes are painted rather than child controls,
    /// so byte navigation uses ScrollToByte; only a request for this grid's own rectangle is accepted here.
    /// </summary>
    public Rect MakeVisible(Visual visual, Rect rectangle) => ReferenceEquals(visual, this) ? rectangle : Rect.Empty;
}
