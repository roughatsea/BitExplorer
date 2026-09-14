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

// Make names from System.Windows available here without writing their full prefix each time. This does not
// run that library's code.
using System.Windows;
// Make names from System.Windows.Controls available here without writing their full prefix each time. This
// does not run that library's code.
using System.Windows.Controls;
// Make names from System.Windows.Controls.Primitives available here without writing their full prefix each
// time. This does not run that library's code.
using System.Windows.Controls.Primitives;
// Make names from System.Windows.Input available here without writing their full prefix each time. This
// does not run that library's code.
using System.Windows.Input;
// Make names from System.Windows.Media available here without writing their full prefix each time. This
// does not run that library's code.
using System.Windows.Media;
// Make names from BitExplorer.Core available here without writing their full prefix each time. This does
// not run that library's code.
using BitExplorer.Core;

// Place this file's definitions in the WpfApp1.Controls naming group, which prevents clashes with names in
// other groups.
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
    // Reserve _renderer to hold an object or value of type ByteGridRenderer; setup can supply its value,
    // otherwise the type's default is used.
    private readonly ByteGridRenderer _renderer;
    // Remember a new object of the required type; the entries in braces set its initial contents or
    // properties as _hoverTip.
    private readonly ToolTip _hoverTip = new() { Placement = PlacementMode.Mouse, StaysOpen = true };
    // Reserve _document to hold an object or value of type DocumentModel?; setup can supply its value,
    // otherwise the type's default is used.
    private DocumentModel? _document;
    // Remember a new object of the required type as _selection.
    private BitSelection _selection = new();
    // Only the temporary details of the current mouse gesture belong to the control.
    // Persistent selected bits and their navigation anchors belong to BitSelection instead.
    private bool _dragging;
    // Reserve _dragUsesBytes to hold a true-or-false answer; setup can supply its value, otherwise the
    // type's default is used.
    private bool _dragUsesBytes;
    // Reserve _dragOriginal to hold a collection that keeps only distinct items; setup can supply its
    // value, otherwise the type's default is used.
    private HashSet<long>? _dragOriginal;
    // Remember -1 as _resizeColumn.
    private int _resizeColumn = -1;
    // Reserve _resizeStartX to hold a number that can include a fraction; setup can supply its value,
    // otherwise the type's default is used.
    private double _resizeStartX;
    // Reserve _resizeStartWidth to hold a number that can include a fraction; setup can supply its value,
    // otherwise the type's default is used.
    private double _resizeStartWidth;
    // Remember -2 as _tooltipKey.
    private long _tooltipKey = -2;
    // Reserve _layoutNotificationPending to hold a true-or-false answer; setup can supply its value,
    // otherwise the type's default is used.
    private bool _layoutNotificationPending;

    /// <summary>Sets up drawing preferences, selection notifications, and WPF load/unload behavior.</summary>
    public ByteGrid()
    {
        // Set _renderer to a new ByteGridRenderer object using the inputs in parentheses.
        _renderer = new ByteGridRenderer(_layout);
        // Register OnSelectionChanged as a listener for _selection.Changed; the listener runs when that
        // event is raised.
        _selection.Changed += OnSelectionChanged;
        // Register OnSelectionLimitReached as a listener for _selection.SelectionLimitReached; the listener
        // runs when that event is raised.
        _selection.SelectionLimitReached += OnSelectionLimitReached;
        // Receive keyboard input, keep drawing inside the control, and align text/lines with the
        // display's pixel grid. ClearType is WPF's text-smoothing mode for readable small type.
        Focusable = true;
        // Set ClipToBounds to true (yes).
        ClipToBounds = true;
        // Set SnapsToDevicePixels to true (yes).
        SnapsToDevicePixels = true;
        // Set UseLayoutRounding to true (yes).
        UseLayoutRounding = true;
        // Call TextOptions.SetTextFormattingMode(...); the values in parentheses are the inputs.
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        // Call TextOptions.SetTextRenderingMode(...); the values in parentheses are the inputs.
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
        // Call ToolTipService.SetInitialShowDelay(...); the values in parentheses are the inputs.
        ToolTipService.SetInitialShowDelay(this, 200);
        // Register the operation after => as a listener for Loaded; the listener runs when that event is
        // raised.
        Loaded += (_, _) => Refresh();
        // Register the operation after => as a listener for Unloaded; the listener runs when that event is
        // raised.
        Unloaded += (_, _) => HideTooltip();
    }

    /// <summary>
    /// The data being displayed. Replacing it detaches the old change handler, clears the old
    /// selection, returns scrolling to the start, and recalculates the display for the new file.
    /// </summary>
    public DocumentModel? Document
    {
        // When this property is read, return _document.
        get => _document;
        // When this property is written, the proposed new content is called value; run this setter to
        // decide how to store it.
        set
        {
            // If ReferenceEquals(_document, value) is true, leave this method immediately.
            if (ReferenceEquals(_document, value)) return;
            // If _document matches not null, stop sending _document.Changed notifications to
            // DocumentChanged.
            if (_document is not null) _document.Changed -= DocumentChanged;
            // Set _document to value.
            _document = value;
            // If _document matches not null, register DocumentChanged as a listener for _document.Changed;
            // the listener runs when that event is raised.
            if (_document is not null) _document.Changed += DocumentChanged;
            // Call EndPointerGesture: Clears transient drag/resize state and releases any mouse capture
            // owned by the grid.
            EndPointerGesture();
            // Call _selection.SetDocumentLength: Sets the valid address range and removes any selection
            // left beyond its end.
            _selection.SetDocumentLength(_document?.Data.LongLength * 8 ?? 0);
            // Remove all current items from _selection.
            _selection.Clear();
            // Call _layout.ResetScroll: Returns both scroll directions to the beginning, normally after
            // opening another file.
            _layout.ResetScroll();
            // Run Refresh to recalculate the values this part of the application presents.
            Refresh();
        }
    }

    /// <summary>
    /// The session-owned selection shared with inspectors and named fields. Assign the document
    /// before attaching this object so that selected positions can be checked against the right file.
    /// </summary>
    public BitSelection Selection
    {
        // When this property is read, return _selection.
        get => _selection;
        // When this property is written, the proposed new content is called value; run this setter to
        // decide how to store it.
        set
        {
            // Reject value immediately if it is null (no object was supplied).
            ArgumentNullException.ThrowIfNull(value);
            // If ReferenceEquals(value, _selection) is true, leave this method immediately.
            if (ReferenceEquals(value, _selection)) return;
            // Call EndPointerGesture: Clears transient drag/resize state and releases any mouse capture
            // owned by the grid.
            EndPointerGesture();
            // Stop sending _selection.Changed notifications to OnSelectionChanged.
            _selection.Changed -= OnSelectionChanged;
            // Stop sending _selection.SelectionLimitReached notifications to OnSelectionLimitReached.
            _selection.SelectionLimitReached -= OnSelectionLimitReached;
            // Set _selection to value.
            _selection = value;
            // Register OnSelectionChanged as a listener for _selection.Changed; the listener runs when that
            // event is raised.
            _selection.Changed += OnSelectionChanged;
            // Register OnSelectionLimitReached as a listener for _selection.SelectionLimitReached; the
            // listener runs when that event is raised.
            _selection.SelectionLimitReached += OnSelectionLimitReached;
            // Call _selection.SetDocumentLength: Sets the valid address range and removes any selection
            // left beyond its end.
            _selection.SetDocumentLength(Document?.Data.LongLength * 8 ?? 0);
            // Call HideTooltip: Closes hover text and clears its key so the same target can be described
            // freshly next time.
            HideTooltip();
            // Ask WPF to draw this control again; the drawing happens during a later rendering pass.
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
        // Call _selection.SetDocumentLength: Sets the valid address range and removes any selection left
        // beyond its end.
        _selection.SetDocumentLength(Document?.Data.LongLength * 8 ?? 0);
        // Run _renderer.Refresh to recalculate the values this part of the application presents, using the
        // options in parentheses.
        _renderer.Refresh(Document);
        // Call UpdateLayoutMetrics: Measures the actual monospace glyph width at the current screen scale,
        // updates geometry, and tells the ScrollViewer that the content size or scroll position may have
        // changed.
        UpdateLayoutMetrics();
        // Call HideTooltip: Closes hover text and clears its key so the same target can be described
        // freshly next time.
        HideTooltip();
        // Ask WPF to measure this control again because its desired size may have changed.
        InvalidateMeasure();
        // Ask WPF to draw this control again; the drawing happens during a later rendering pass.
        InvalidateVisual();
    }

    /// <summary>Replaces selected bits using the shared rules, and optionally reveals the active byte.</summary>
    public void SetSelection(IEnumerable<long> bits, bool scrollIntoView = true)
    {
        // If both _selection.SetSelection(bits) is true and scrollIntoView is true, call ScrollToByte:
        // Moves only far enough to bring a byte's row into the visible body of the grid.
        if (_selection.SetSelection(bits) && scrollIntoView) ScrollToByte(ActiveByteOffset);
    }

    /// <summary>Moves only far enough to bring a byte's row into the visible body of the grid.</summary>
    public void ScrollToByte(int offset)
    {
        // Call _layout.ScrollToByte: Moves only far enough to bring a byte's row into the visible body of
        // the grid.
        _layout.ScrollToByte(offset);
        // Call InvalidateScroll: Updates scroll bars, dismisses position-dependent hover text, and requests
        // repainting.
        InvalidateScroll();
    }

    /// <summary>
    /// Responds to model changes on the UI thread. Dispatcher is WPF's queue for work that must
    /// touch UI objects; a background notification must enter that queue before refreshing the grid.
    /// </summary>
    private void DocumentChanged(object? sender, EventArgs args)
    {
        // If it is not the case that Dispatcher.CheckAccess() is true, run the following grouped
        // instructions.
        if (!Dispatcher.CheckAccess()) { Dispatcher.InvokeAsync(Refresh); return; }
        // Run Refresh to recalculate the values this part of the application presents.
        Refresh();
    }

    /// <summary>Removes stale hover text, redraws selection highlights, and forwards the notification.</summary>
    private void OnSelectionChanged(object? sender, EventArgs args)
    {
        // Call HideTooltip: Closes hover text and clears its key so the same target can be described
        // freshly next time.
        HideTooltip();
        // Ask WPF to draw this control again; the drawing happens during a later rendering pass.
        InvalidateVisual();
        // If SelectionChanged is present, perform .Invoke(this, EventArgs.Empty); ?. skips this call when
        // there is no recipient.
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Stops an oversized drag while retaining the last successfully selected bits.</summary>
    private void OnSelectionLimitReached(object? sender, EventArgs args)
    {
        // Call EndPointerGesture: Clears transient drag/resize state and releases any mouse capture owned
        // by the grid.
        EndPointerGesture();
        // If SelectionLimitReached is present, perform .Invoke(this, EventArgs.Empty); ?. skips this call
        // when there is no recipient.
        SelectionLimitReached?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// WPF's measure step asks how much space this element would like. A scroll container may
    /// offer unlimited space, so finite fallback sizes keep the requested window size reasonable.
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        // Call UpdateLayoutMetrics: Measures the actual monospace glyph width at the current screen scale,
        // updates geometry, and tells the ScrollViewer that the content size or scroll position may have
        // changed.
        UpdateLayoutMetrics();
        // Return a new Size object using the inputs in parentheses to the caller and leave this method.
        return new Size(double.IsInfinity(availableSize.Width) ? Math.Min(1100, ExtentWidth) : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 500 : availableSize.Height);
    }

    /// <summary>
    /// WPF's arrange step supplies the space actually assigned to the grid. That rectangle becomes
    /// the viewport: the window through which the much larger document is visible.
    /// </summary>
    protected override Size ArrangeOverride(Size finalSize)
    {
        // Call _layout.SetViewport: Stores the newly assigned visible size and prevents scrolling past the
        // content edges.
        _layout.SetViewport(finalSize);
        // Call UpdateLayoutMetrics: Measures the actual monospace glyph width at the current screen scale,
        // updates geometry, and tells the ScrollViewer that the content size or scroll position may have
        // changed.
        UpdateLayoutMetrics();
        // Return finalSize to the caller and leave this method.
        return finalSize;
    }

    /// <summary>
    /// Measures the actual monospace glyph width at the current screen scale, updates geometry,
    /// and tells the ScrollViewer that the content size or scroll position may have changed.
    /// </summary>
    private void UpdateLayoutMetrics()
    {
        // Remember the result returned by _renderer.MeasureCharacterWidth(...) as characterWidth.
        double characterWidth = _renderer.MeasureCharacterWidth(VisualTreeHelper.GetDpi(this).PixelsPerDip);
        // If _layout.Update(Document, characterWidth) is true, call ScheduleLayoutNotification: Combines
        // repeated layout changes into one later notification.
        if (_layout.Update(Document, characterWidth)) ScheduleLayoutNotification();
        // If ScrollOwner is present, perform .InvalidateScrollInfo(); ?. skips this call when there is no
        // recipient.
        ScrollOwner?.InvalidateScrollInfo();
    }

    /// <summary>
    /// Combines repeated layout changes into one later notification. Deferring it avoids changing
    /// bound controls while WPF is still inside its current measurement or arrangement pass.
    /// </summary>
    private void ScheduleLayoutNotification()
    {
        // If _layoutNotificationPending is true, leave this method immediately.
        if (_layoutNotificationPending) return;
        // Set _layoutNotificationPending to true (yes).
        _layoutNotificationPending = true;
        // Call Dispatcher.BeginInvoke(...); the values in parentheses are the inputs. Any => block supplies
        // an operation for the receiving code to invoke.
        Dispatcher.BeginInvoke(() =>
        {
            // Set _layoutNotificationPending to false (no).
            _layoutNotificationPending = false;
            // If LayoutSettingsChanged is present, perform .Invoke(this, EventArgs.Empty); ?. skips this
            // call when there is no recipient.
            LayoutSettingsChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    /// <summary>WPF calls this when painting is needed; the renderer draws only the visible rows.</summary>
    protected override void OnRender(DrawingContext drawing)
    {
        // Call base.OnRender with the event or drawing arguments so the inherited WPF behavior also runs.
        base.OnRender(drawing);
        // Call _renderer.Render: Paints a frame: background, visible body rows, stationary header, then
        // column dividers.
        _renderer.Render(drawing, _selection, RenderSize, IsKeyboardFocusWithin, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }

    /// <summary>
    /// Starts a divider resize or data selection. Shift extends a range, Ctrl toggles/adds bits,
    /// and a double-click requests editing. Hit testing means translating a pointer position into data.
    /// </summary>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        // Call base.OnMouseLeftButtonDown with the event or drawing arguments so the inherited WPF
        // behavior also runs.
        base.OnMouseLeftButtonDown(e);
        // Ask WPF to give this control keyboard focus, so keyboard input is directed here.
        Focus();
        // Remember the result returned by e.GetPosition(...) as point.
        var point = e.GetPosition(this);
        // Call HideTooltip: Closes hover text and clears its key so the same target can be described
        // freshly next time.
        HideTooltip();
        // Handled events below stop traveling through the parent controls, preventing the same click
        // from also starting another action in the surrounding UI.
        _resizeColumn = _layout.HitDivider(point);
        // If both _resizeColumn is at least 0 and Document matches not null, run the following grouped
        // instructions.
        if (_resizeColumn >= 0 && Document is not null)
        {
            // Set _resizeStartX to point.X.
            _resizeStartX = point.X;
            // Set _resizeStartWidth to the result returned by _layout.GetColumnWidth(...).
            _resizeStartWidth = _layout.GetColumnWidth(_resizeColumn);
            // Mouse capture keeps delivering this gesture's events even if the pointer leaves the grid.
            CaptureMouse();
            // Set e.Handled to true (yes).
            e.Handled = true;
            // Leave this method immediately.
            return;
        }
        // Remember the result returned by _layout.HitData(...) as hit.
        var hit = _layout.HitData(point);
        // If hit.Bit is less than 0, leave this method immediately.
        if (hit.Bit < 0) return;
        // If e.ClickCount equals 2, run the following grouped instructions.
        if (e.ClickCount == 2)
        {
            // Call _selection.SetActiveBit: Moves the cursor without changing membership or the range
            // anchor.
            _selection.SetActiveBit(hit.Bit);
            // If EditRequested is present, perform .Invoke(this, new ByteEditEventArgs((int)(hit.Bit /
            // 8))); ?. skips this call when there is no recipient.
            EditRequested?.Invoke(this, new ByteEditEventArgs((int)(hit.Bit / 8)));
            // Set e.Handled to true (yes).
            e.Handled = true;
            // Leave this method immediately.
            return;
        }
        // Remember the result returned by Keyboard.Modifiers.HasFlag(...) as shift.
        bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        // Remember the result returned by Keyboard.Modifiers.HasFlag(...) as ctrl.
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        // Set _dragUsesBytes to hit.WholeByte.
        _dragUsesBytes = hit.WholeByte;
        // Keep the selection from BEFORE the drag so Ctrl-drag can preserve separate earlier groups.
        // This is a gesture snapshot, not a second owner of the live selection.
        _dragOriginal = ctrl ? new HashSet<long>(_selection.Bits) : null;
        // If _selection.SelectAt(hit.Bit, hit.WholeByte, shift, ctrl) is true, run the following grouped
        // instructions.
        if (_selection.SelectAt(hit.Bit, hit.WholeByte, shift, ctrl))
        {
            // Set _dragging to true (yes).
            _dragging = true;
            // Keep receiving pointer events during the drag even when the pointer leaves the control.
            CaptureMouse();
        }
        // Set e.Handled to true (yes).
        e.Handled = true;
    }

    /// <summary>
    /// Continues a captured resize/selection, scrolls near the top or bottom while dragging,
    /// or shows hover information when no selection gesture is underway.
    /// </summary>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        // Call base.OnMouseMove with the event or drawing arguments so the inherited WPF behavior also
        // runs.
        base.OnMouseMove(e);
        // Remember the result returned by e.GetPosition(...) as point.
        var point = e.GetPosition(this);
        // If both both _resizeColumn is at least 0 and IsMouseCaptured is true and Document matches not
        // null, run the following grouped instructions.
        if (_resizeColumn >= 0 && IsMouseCaptured && Document is not null)
        {
            // Call _layout.ResizeColumn: Updates the requested width while a header divider is dragged.
            _layout.ResizeColumn(_resizeColumn, _resizeStartWidth + point.X - _resizeStartX);
            // Run Refresh to recalculate the values this part of the application presents.
            Refresh();
            // Call ScheduleLayoutNotification: Combines repeated layout changes into one later
            // notification.
            ScheduleLayoutNotification();
            // Set e.Handled to true (yes).
            e.Handled = true;
            // Leave this method immediately.
            return;
        }
        // Set Cursor to Cursors.SizeWE when _layout.HitDivider(point) is at least 0; otherwise
        // Cursors.Arrow.
        Cursor = _layout.HitDivider(point) >= 0 ? Cursors.SizeWE : Cursors.Arrow;
        // If both _dragging is true and e.LeftButton equals MouseButtonState.Pressed, run the following
        // grouped instructions.
        if (_dragging && e.LeftButton == MouseButtonState.Pressed)
        {
            // If point.Y is less than _layout.HeaderHeight, call SetVerticalOffset: Clamps a requested
            // vertical position to the content and refreshes scrolling feedback.
            if (point.Y < _layout.HeaderHeight) SetVerticalOffset(VerticalOffset - _layout.RowHeight);
            // Otherwise, try this next condition: point.Y > RenderSize.Height - 12.
            else if (point.Y > RenderSize.Height - 12) SetVerticalOffset(VerticalOffset + _layout.RowHeight);
            // Clamp keeps an out-of-bounds drag pointed at the nearest valid bit instead of losing it.
            var hit = _layout.HitData(point, true);
            // If both hit.Bit is at least 0 and hit.Bit differs from _selection.ActiveBit, call
            // _selection.SelectRange: Selects an inclusive range and optionally retains a previous
            // selection for Ctrl-drag.
            if (hit.Bit >= 0 && hit.Bit != _selection.ActiveBit)
                // Call _selection.SelectRange: Selects an inclusive range and optionally retains a previous
                // selection for Ctrl-drag.
                _selection.SelectRange(_selection.Anchor, hit.Bit, _dragUsesBytes, _dragOriginal);
            // Set e.Handled to true (yes).
            e.Handled = true;
        }
        // If the preceding condition was false, run this alternative path.
        else UpdateTooltip(point);
    }

    /// <summary>Finishes the gesture when the left mouse button is released.</summary>
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        // Call base.OnMouseLeftButtonUp with the event or drawing arguments so the inherited WPF behavior
        // also runs.
        base.OnMouseLeftButtonUp(e);
        // If both it is not the case that _dragging is true and _resizeColumn is less than 0, leave this
        // method immediately.
        if (!_dragging && _resizeColumn < 0) return;
        // Call EndPointerGesture: Clears transient drag/resize state and releases any mouse capture owned
        // by the grid.
        EndPointerGesture();
        // Set e.Handled to true (yes).
        e.Handled = true;
    }

    /// <summary>Clears transient drag/resize state and releases any mouse capture owned by the grid.</summary>
    private void EndPointerGesture()
    {
        // Set _dragging to false (no).
        _dragging = false;
        // Set _resizeColumn to -1.
        _resizeColumn = -1;
        // Set _dragOriginal to null (no value).
        _dragOriginal = null;
        // If IsMouseCaptured is true, end pointer capture so future mouse events follow the pointer's
        // normal target.
        if (IsMouseCaptured) ReleaseMouseCapture();
    }

    /// <summary>Also ends a gesture if WPF or another control takes mouse capture away unexpectedly.</summary>
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        // Call EndPointerGesture: Clears transient drag/resize state and releases any mouse capture owned
        // by the grid.
        EndPointerGesture();
        // Call base.OnLostMouseCapture with the event or drawing arguments so the inherited WPF behavior
        // also runs.
        base.OnLostMouseCapture(e);
    }

    /// <summary>Hides hover text when the pointer leaves; captured dragging is handled separately.</summary>
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        // Call HideTooltip: Closes hover text and clears its key so the same target can be described
        // freshly next time.
        HideTooltip();
        // Call base.OnMouseLeave with the event or drawing arguments so the inherited WPF behavior also
        // runs.
        base.OnMouseLeave(e);
    }

    /// <summary>Asks the tooltip builder for a description, and reopens the popup only when its target changes.</summary>
    private void UpdateTooltip(Point point)
    {
        // Remember the result returned by ByteGridTooltip.Describe(...) as description.
        var description = ByteGridTooltip.Describe(Document, _layout, point);
        // If description matches null, run the following grouped instructions.
        if (description is null) { HideTooltip(); return; }
        // If _tooltipKey equals description.Value.Key, leave this method immediately.
        if (_tooltipKey == description.Value.Key) return;
        // Set _tooltipKey to description.Value.Key.
        _tooltipKey = description.Value.Key;
        // Set _hoverTip.Content to description.Value.Text.
        _hoverTip.Content = description.Value.Text;
        // Set _hoverTip.PlacementTarget to this.
        _hoverTip.PlacementTarget = this;
        // Set _hoverTip.IsOpen to true (yes).
        _hoverTip.IsOpen = true;
    }

    /// <summary>Closes hover text and clears its key so the same target can be described freshly next time.</summary>
    private void HideTooltip()
    {
        // Set _hoverTip.IsOpen to false (no).
        _hoverTip.IsOpen = false;
        // Set _tooltipKey to -2.
        _tooltipKey = -2;
    }

    /// <summary>
    /// Maps WPF keys to UI-independent selection operations. The selection model performs the
    /// range arithmetic and limit checks; this adapter handles Enter and marks handled keys.
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Call base.OnKeyDown with the event or drawing arguments so the inherited WPF behavior also
        // runs.
        base.OnKeyDown(e);
        // If Document matches null, or Document.Data.Length equals 0, or e.Handled is true, leave this
        // method immediately.
        if (Document is null || Document.Data.Length == 0 || e.Handled) return;
        // If both e.Key equals Key.Enter and ActiveByteOffset is at least 0, run the following grouped
        // instructions.
        if (e.Key == Key.Enter && ActiveByteOffset >= 0)
        {
            // If EditRequested is present, perform .Invoke(this, new ByteEditEventArgs(ActiveByteOffset));
            // ?. skips this call when there is no recipient.
            EditRequested?.Invoke(this, new ByteEditEventArgs(ActiveByteOffset));
            // Set e.Handled to true (yes).
            e.Handled = true;
            // Leave this method immediately.
            return;
        }
        // Remember the result returned by Keyboard.Modifiers.HasFlag(...) as ctrl.
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        // If both ctrl is true and e.Key equals Key.A, run the following grouped instructions.
        if (ctrl && e.Key == Key.A)
        {
            // Call _selection.SelectRange: Selects an inclusive range and optionally retains a previous
            // selection for Ctrl-drag.
            _selection.SelectRange(0, Document.Data.LongLength * 8 - 1, true);
            // Set e.Handled to true (yes).
            e.Handled = true;
            // Leave this method immediately.
            return;
        }
        // Remember the result selected by matching e.Key to one of the cases below as direction.
        SelectionNavigation? direction = e.Key switch
        {
            // For Key.Left, use SelectionNavigation.Left.
            Key.Left => SelectionNavigation.Left, Key.Right => SelectionNavigation.Right,
            // For Key.Up, use SelectionNavigation.Up.
            Key.Up => SelectionNavigation.Up, Key.Down => SelectionNavigation.Down,
            // For Key.Home, use SelectionNavigation.Home.
            Key.Home => SelectionNavigation.Home, Key.End => SelectionNavigation.End,
            // For Key.PageUp, use SelectionNavigation.PageUp.
            Key.PageUp => SelectionNavigation.PageUp, Key.PageDown => SelectionNavigation.PageDown,
            // For any remaining case, use null (no value).
            _ => null
        };
        // If direction matches null, leave this method immediately.
        if (direction is null) return;
        // Remember the larger of the two supplied numbers as visibleRows.
        int visibleRows = Math.Max(1, (int)((ViewportHeight - _layout.HeaderHeight) / _layout.RowHeight));
        // Call _selection.Navigate: Turns arrow, Home/End, or page movement into the same selection
        // operations used by the mouse.
        _selection.Navigate(direction.Value, _layout.BitView, _layout.BytesPerRow, visibleRows, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift), ctrl);
        // Call ScrollToByte: Moves only far enough to bring a byte's row into the visible body of the grid.
        ScrollToByte(ActiveByteOffset);
        // Set e.Handled to true (yes).
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
        // Store the requested horizontal position after limiting it to the content's
        // bounds. This calculation alone does not update the visible scroll bars.
        _layout.SetHorizontalOffset(offset);
        // Call InvalidateScroll: Updates scroll bars, dismisses position-dependent hover text, and requests
        // repainting.
        InvalidateScroll();
    }

    /// <summary>Clamps a requested vertical position to the content and refreshes scrolling feedback.</summary>
    public void SetVerticalOffset(double offset)
    {
        // Store the requested vertical position after limiting it to the content's
        // bounds. The following notification makes WPF reflect that new position.
        _layout.SetVerticalOffset(offset);
        // Call InvalidateScroll: Updates scroll bars, dismisses position-dependent hover text, and requests
        // repainting.
        InvalidateScroll();
    }

    /// <summary>Updates scroll bars, dismisses position-dependent hover text, and requests repainting.</summary>
    private void InvalidateScroll()
    {
        // If ScrollOwner is present, perform .InvalidateScrollInfo(); ?. skips this call when there is no
        // recipient.
        ScrollOwner?.InvalidateScrollInfo();
        // Call HideTooltip: Closes hover text and clears its key so the same target can be described
        // freshly next time.
        HideTooltip();
        // Ask WPF to draw this control again; the drawing happens during a later rendering pass.
        InvalidateVisual();
    }

    /// <summary>
    /// Implements WPF's request to reveal a child visual. Bytes are painted rather than child controls,
    /// so byte navigation uses ScrollToByte; only a request for this grid's own rectangle is accepted here.
    /// </summary>
    public Rect MakeVisible(Visual visual, Rect rectangle) => ReferenceEquals(visual, this) ? rectangle : Rect.Empty;
}
