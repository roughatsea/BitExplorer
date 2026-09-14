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

// Make names from System.Globalization available here without writing their full prefix each time. This
// does not run that library's code.
using System.Globalization;
// Make names from System.Windows available here without writing their full prefix each time. This does not
// run that library's code.
using System.Windows;
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
/// Paints the grid directly rather than creating a WPF control for each byte. The shared layout
/// provides every position, while this class chooses text, colors, outlines, and field underlines.
/// </summary>
/// <remarks>
/// DrawingContext records drawing operations such as text and rectangles. A brush supplies a fill
/// color; a pen supplies a line color and thickness. Reusing font/annotation resources avoids
/// recreating color brushes and repeatedly scanning each field's full source-bit list. Field
/// lookups still occur for visible bytes; we do not create a separate UI control for each byte.
/// </remarks>
/// <param name="layout">The same geometry object that the grid uses to interpret pointer positions.</param>
internal sealed class ByteGridRenderer(ByteGridLayout layout)
{
    // Monospace fonts give every character the same width, which is essential for aligned columns
    // and faithful plain-text export. UI headings use a proportional font instead.
    private static readonly Typeface Mono = new("Cascadia Mono, Consolas");
    // Remember a new object of the required type using the inputs in parentheses as Ui.
    private static readonly Typeface Ui = new("Segoe UI");
    // Remember the result returned by Brush(...) as BackgroundBrush.
    private static readonly Brush BackgroundBrush = Brush("#10151E");
    // Remember the result returned by Brush(...) as AlternateBrush.
    private static readonly Brush AlternateBrush = Brush("#131A25");
    // Remember the result returned by Brush(...) as HeaderBrush.
    private static readonly Brush HeaderBrush = Brush("#18212E");
    // Remember the result returned by Brush(...) as LineBrush.
    private static readonly Brush LineBrush = Brush("#293648");
    // Remember the result returned by Brush(...) as TextBrush.
    private static readonly Brush TextBrush = Brush("#D5DFED");
    // Remember the result returned by Brush(...) as MutedBrush.
    private static readonly Brush MutedBrush = Brush("#8491A5");
    // Remember the result returned by Brush(...) as SelectionBrush.
    private static readonly Brush SelectionBrush = Brush("#143E42");
    // Remember the result returned by Brush(...) as SelectionTextBrush.
    private static readonly Brush SelectionTextBrush = Brush("#6CE1CE");
    // Remember the result returned by Brush(...) as ModifiedBrush.
    private static readonly Brush ModifiedBrush = Brush("#E6B76A");
    // Remember the result returned by Brush(...) as RulerBrush.
    private static readonly Brush RulerBrush = Brush("#A2B0C2");
    // A HashSet answers "does this field include this bit?" without scanning its complete bit list
    // for each character drawn. These caches are rebuilt whenever the document tells the grid to refresh.
    private readonly Dictionary<NamedField, HashSet<long>> _fieldBits = [];
    // Remember an empty collection as _fieldBrushes.
    private readonly Dictionary<NamedField, Brush> _fieldBrushes = [];
    // Reserve _document to hold an object or value of type DocumentModel?; setup can supply its value,
    // otherwise the type's default is used.
    private DocumentModel? _document;
    // Remember 1 as _pixelsPerDip.
    private double _pixelsPerDip = 1;

    /// <summary>
    /// Measures one normal-sized monospace character. PixelsPerDip tells WPF how many physical
    /// display pixels represent one layout unit at the current monitor scale (for example, 1.5 at 150%).
    /// </summary>
    public double MeasureCharacterWidth(double pixelsPerDip)
    {
        // Set _pixelsPerDip to pixelsPerDip.
        _pixelsPerDip = pixelsPerDip;
        // Return Text("0", TextBrush).WidthIncludingTrailingWhitespace to the caller and leave this method.
        return Text("0", TextBrush).WidthIncludingTrailingWhitespace;
    }

    /// <summary>
    /// Attaches the currently displayed document and rebuilds field membership/color caches.
    /// A malformed stored color falls back to the selection color so one annotation cannot break drawing.
    /// </summary>
    public void Refresh(DocumentModel? document)
    {
        // Set _document to document.
        _document = document;
        // Remove all current items from _fieldBits.
        _fieldBits.Clear();
        // Remove all current items from _fieldBrushes.
        _fieldBrushes.Clear();
        // If document matches null, leave this method immediately.
        if (document is null) return;
        // Take each item from document.Fields in turn, call the current item field, and run the following
        // grouped instructions.
        foreach (var field in document.Fields)
        {
            // Set _fieldBits[field] to a new HashSet<long> object using the inputs in parentheses.
            _fieldBits[field] = new HashSet<long>(field.OrderedBits);
            // Attempt this work. A matching catch below handles a reported exception; a finally block, when
            // present, performs cleanup on the way out.
            try { _fieldBrushes[field] = Brush(field.Color); }
            // If the preceding try reports Exception, refer to it as ex. Handle it here only when ex
            // matches FormatException or NotSupportedException or ArgumentException; run this recovery
            // path.
            catch (Exception ex) when (ex is FormatException or NotSupportedException or ArgumentException)
            // Use the known selection color for this field if its color cannot be
            // interpreted. Continue drawing the other fields normally.
            { _fieldBrushes[field] = SelectionTextBrush; }
        }
    }
    /// <summary>
    /// Paints a frame: background, visible body rows, stationary header, then column dividers.
    /// The caller supplies the shared selection for this frame; the renderer never changes selected bits.
    /// </summary>
    public void Render(DrawingContext drawing, BitSelection selection, Size renderSize, bool keyboardFocus, double pixelsPerDip)
    {
        // Set _pixelsPerDip to pixelsPerDip.
        _pixelsPerDip = pixelsPerDip;
        // Record a rectangle to draw: the brush fills its interior, the pen outlines it, and Rect supplies
        // its position and size. null omits a fill or outline.
        drawing.DrawRectangle(BackgroundBrush, null, new Rect(renderSize));
        // If _document matches null, leave this method immediately.
        if (_document is null) return;
        // A transform changes the coordinates of subsequent drawing commands. Moving all content
        // left by the scroll offset makes a content x of 200 appear at x=150 after scrolling right 50.
        drawing.PushTransform(new TranslateTransform(-layout.HorizontalOffset, 0));
        // A clip acts like a window cutout: drawing outside this body rectangle is hidden. Rows
        // can scroll behind the header without painting over it. Push/Pop pairs restore drawing state.
        drawing.PushClip(new RectangleGeometry(new Rect(layout.HorizontalOffset, layout.HeaderHeight, renderSize.Width, Math.Max(0, renderSize.Height - layout.HeaderHeight))));

        // Set var (first, last) to the result returned by layout.VisibleRows(...).
        var (first, last) = layout.VisibleRows(renderSize.Height);
        // Repeat with int row = first as the starting state, while row is less than last; after each pass,
        // increase row by one. On each pass, call DrawRow: Draws one row at its already-scrolled vertical
        // position.
        for (int row = first; row < last; row++)
            // Call DrawRow: Draws one row at its already-scrolled vertical position.
            DrawRow(drawing, row, layout.HeaderHeight + row * layout.RowHeight - layout.VerticalOffset, selection, renderSize, keyboardFocus);
        // Remove the body clip before drawing the header, while retaining horizontal translation.
        drawing.Pop();
        // Call DrawHeader: Draws column names and the optional position ruler.
        DrawHeader(drawing, renderSize);
        // Call DrawColumnDividers: Draws visible column boundaries and paired grip marks that indicate
        // draggable header edges.
        DrawColumnDividers(drawing, renderSize);
        // Remove the most recently pushed drawing restriction or coordinate change, restoring the preceding
        // drawing state.
        drawing.Pop();

        // With both transforms/clips restored, the empty-file message is positioned in viewport space.
        if (_document.Data.Length == 0)
        {
            // Remember the result returned by Text(...) as title.
            var title = Text("Open a binary file to begin exploring", TextBrush, 16, false);
            // Record the supplied formatted text at the supplied Point (horizontal x, vertical y).
            drawing.DrawText(title, new Point(Math.Max(24, (renderSize.Width - title.Width) / 2), layout.HeaderHeight + 96));
            // Remember the result returned by Text(...) as note.
            var note = Text("Bytes, bits, and named fields stay connected as you explore.", MutedBrush, 12, false);
            // Record the supplied formatted text at the supplied Point (horizontal x, vertical y).
            drawing.DrawText(note, new Point(Math.Max(24, (renderSize.Width - note.Width) / 2), layout.HeaderHeight + 126));
        }
    }

    /// <summary>
    /// Draws column names and the optional position ruler. The ruler comes from DisplayFormatter,
    /// the same source used by text export; changing bit-number labels never reverses the stored bits.
    /// </summary>
    private void DrawHeader(DrawingContext drawing, Size renderSize)
    {
        // Record a rectangle to draw: the brush fills its interior, the pen outlines it, and Rect supplies
        // its position and size. null omits a fill or outline.
        drawing.DrawRectangle(HeaderBrush, null, new Rect(0, 0, Math.Max(layout.Extent.Width, renderSize.Width + layout.HorizontalOffset), layout.HeaderHeight));
        // If layout.OffsetWidth is greater than 0, call DrawClippedText: Draws text inside a width-limited
        // rectangle, restoring the previous clip afterwards.
        if (layout.OffsetWidth > 0) DrawClippedText(drawing, "OFFSET · " + (_document!.Settings.OffsetBase == NumericBase.Hexadecimal ? "HEX" : "DEC"), layout.Gutter, 10, layout.OffsetWidth - layout.Gutter * 2, MutedBrush, false, 10);
        // Remember the text "BITS · MS bit → LS bit" when layout.BitView is true; otherwise the text "BYTES
        // · " + (_document!.Settings.ByteBase == NumericBase.Hexadecimal ? "HEXADECIMAL" : "DECIMAL")
        // (addition, or joining text) as label.
        string label = layout.BitView ? "BITS  ·  MS bit → LS bit" : "BYTES  ·  " + (_document!.Settings.ByteBase == NumericBase.Hexadecimal ? "HEXADECIMAL" : "DECIMAL");
        // Call DrawClippedText: Draws text inside a width-limited rectangle, restoring the previous clip
        // afterwards.
        DrawClippedText(drawing, label, layout.DataX + layout.Gutter, 10, layout.DataWidth - layout.Gutter * 2, RulerBrush, false, 10);
        // If layout.AsciiWidth is greater than 0, call DrawClippedText: Draws text inside a width-limited
        // rectangle, restoring the previous clip afterwards.
        if (layout.AsciiWidth > 0) DrawClippedText(drawing, "TEXT / ASCII", layout.AsciiX + layout.Gutter, 10, layout.AsciiWidth - layout.Gutter * 2, MutedBrush, false, 10);
        // If _document!.Settings.ShowRuler is true, run the following grouped instructions.
        if (_document!.Settings.ShowRuler)
        {
            // If layout.OffsetWidth is greater than 0, call DrawClippedText: Draws text inside a
            // width-limited rectangle, restoring the previous clip afterwards.
            if (layout.OffsetWidth > 0) DrawClippedText(drawing, layout.BitView ? "bit index" : "+ position", layout.Gutter, 36, layout.OffsetWidth - layout.Gutter * 2, MutedBrush, false, 10);
            // Limit subsequent drawing to the supplied shape until the matching Pop restores the previous
            // drawing state.
            drawing.PushClip(new RectangleGeometry(new Rect(layout.DataX + 1, 30, Math.Max(0, layout.DataWidth - 2), 31)));
            // Remember the result returned by DisplayFormatter.GetRuler(...) as ruler.
            string ruler = DisplayFormatter.GetRuler(_document!.Settings);
            // Record the supplied formatted text at the supplied Point (horizontal x, vertical y).
            drawing.DrawText(Text(ruler, MutedBrush, 14), new Point(layout.DataX + layout.Gutter, 35));
            // Remove the most recently pushed drawing restriction or coordinate change, restoring the
            // preceding drawing state.
            drawing.Pop();
            // If layout.AsciiWidth is greater than 0, call DrawClippedText: Draws text inside a
            // width-limited rectangle, restoring the previous clip afterwards.
            if (layout.AsciiWidth > 0) DrawClippedText(drawing, "printable characters", layout.AsciiX + layout.Gutter, 36, layout.AsciiWidth - layout.Gutter * 2, MutedBrush, false, 10);
        }
        // Record a line between the two supplied Points, using the Pen's color and thickness.
        drawing.DrawLine(new Pen(LineBrush, 1), new Point(0, layout.HeaderHeight - .5), new Point(Math.Max(layout.Extent.Width, renderSize.Width), layout.HeaderHeight - .5));
    }

    /// <summary>Draws visible column boundaries and paired grip marks that indicate draggable header edges.</summary>
    private void DrawColumnDividers(DrawingContext drawing, Size renderSize)
    {
        // Remember a new Pen object using the inputs in parentheses as pen.
        var pen = new Pen(LineBrush, 1);
        // If layout.OffsetWidth is greater than 0, record a line between the two supplied Points, using the
        // Pen's color and thickness.
        if (layout.OffsetWidth > 0) drawing.DrawLine(pen, new Point(layout.DataX, 0), new Point(layout.DataX, renderSize.Height));
        // Record a line between the two supplied Points, using the Pen's color and thickness.
        drawing.DrawLine(pen, new Point(layout.AsciiX, 0), new Point(layout.AsciiX, renderSize.Height));
        // If layout.AsciiWidth is greater than 0, record a line between the two supplied Points, using the
        // Pen's color and thickness.
        if (layout.AsciiWidth > 0) drawing.DrawLine(pen, new Point(layout.AsciiX + layout.AsciiWidth, 0), new Point(layout.AsciiX + layout.AsciiWidth, renderSize.Height));
        // Take each item from layout.VisibleDividers() in turn, call the current item x, and run the
        // following grouped instructions.
        foreach (double x in layout.VisibleDividers())
        {
            // Record a line between the two supplied Points, using the Pen's color and thickness.
            drawing.DrawLine(new Pen(MutedBrush, 1), new Point(x - 2, 12), new Point(x - 2, 23));
            // Record a line between the two supplied Points, using the Pen's color and thickness.
            drawing.DrawLine(new Pen(MutedBrush, 1), new Point(x + 2, 12), new Point(x + 2, 23));
        }
    }

    /// <summary>
    /// Draws one row at its already-scrolled vertical position. Only the bytes that exist in the file
    /// are drawn, so a partial final row has no invented padding bytes. Each column has its own clip.
    /// </summary>
    private void DrawRow(DrawingContext drawing, int row, double y, BitSelection selection, Size renderSize, bool keyboardFocus)
    {
        // If _document matches null, leave this method immediately.
        if (_document is null) return;
        // Remember row multiplied by layout.BytesPerRow as offset.
        int offset = row * layout.BytesPerRow;
        // Remember the smaller of the two supplied numbers as length.
        int length = Math.Min(layout.BytesPerRow, _document.Data.Length - offset);
        // If (row & 1) equals 1, record a rectangle to draw: the brush fills its interior, the pen outlines
        // it, and Rect supplies its position and size. null omits a fill or outline.
        if ((row & 1) == 1) drawing.DrawRectangle(AlternateBrush, null, new Rect(0, y, Math.Max(layout.Extent.Width, renderSize.Width), layout.RowHeight));

        // If layout.OffsetWidth is greater than 0, call DrawClippedText: Draws text inside a width-limited
        // rectangle, restoring the previous clip afterwards.
        if (layout.OffsetWidth > 0)
            // Call DrawClippedText: Draws text inside a width-limited rectangle, restoring the previous
            // clip afterwards.
            DrawClippedText(drawing, DisplayFormatter.FormatOffset(offset, _document!.Settings.OffsetBase), layout.Gutter, y + 5, layout.OffsetWidth - layout.Gutter * 2, MutedBrush);

        // Constrain data, highlights, and field labels to the data column. A long annotation must not
        // spill into ASCII, and horizontal scrolling must not require changing individual byte positions.
        drawing.PushClip(new RectangleGeometry(new Rect(layout.DataX + 1, y, Math.Max(0, layout.DataWidth - 2), layout.RowHeight)));
        // Repeat with int i = 0 as the starting state, while i is less than length; after each pass,
        // increase i by one. The braces contain one pass.
        for (int i = 0; i < length; i++)
        {
            // Remember offset + i (addition, or joining text) as byteOffset.
            int byteOffset = offset + i;
            // Remember layout.DataX + layout.Gutter (addition, or joining text) + i multiplied by
            // layout.CellWidth (addition, or joining text) as x.
            double x = layout.DataX + layout.Gutter + i * layout.CellWidth;
            // If x is at least layout.AsciiX, run the following instruction.
            if (x >= layout.AsciiX) break;
            // Remember the result returned by _document.IsByteModified(...) as modified.
            bool modified = _document.IsByteModified(byteOffset);
            // Remember _document.Data[byteOffset] as value.
            byte value = _document.Data[byteOffset];
            // If layout.BitView is true, run the following grouped instructions.
            if (layout.BitView)
            {
                // Repeat with int bit = 0 as the starting state, while bit is less than 8; after each pass,
                // increase bit by one. The braces contain one pass.
                for (int bit = 0; bit < 8; bit++)
                {
                    // Physical bit addresses increase left-to-right from a byte's most significant bit.
                    // For example, 0xA1 is displayed as 10100001; its first character has weight 128.
                    long address = (long)byteOffset * 8 + bit;
                    // Remember whether selection contains address as selected.
                    bool selected = selection.Contains(address);
                    // Remember a new Rect object using the inputs in parentheses as rect.
                    var rect = new Rect(x + bit * layout.CharacterWidth - .5, y + 2, layout.CharacterWidth + .25, 23);
                    // If selected is true, record a rectangle to draw: the brush fills its interior, the
                    // pen outlines it, and Rect supplies its position and size. null omits a fill or
                    // outline.
                    if (selected) drawing.DrawRectangle(SelectionBrush, null, rect);
                    // Record the supplied formatted text at the supplied Point (horizontal x, vertical y).
                    drawing.DrawText(Text((value & (1 << (7 - bit))) != 0 ? "1" : "0", selected ? SelectionTextBrush : modified ? ModifiedBrush : TextBrush), new Point(x + bit * layout.CharacterWidth, y + 5));
                    // Selection fill and keyboard focus are separate: many bits may be selected,
                    // but only the active navigation position receives the thin focus outline.
                    if (address == selection.ActiveBit && keyboardFocus)
                        // Record a rectangle to draw: the brush fills its interior, the pen outlines it,
                        // and Rect supplies its position and size. null omits a fill or outline.
                        drawing.DrawRectangle(null, new Pen(SelectionTextBrush, .8), rect);
                }
            }
            // If the preceding condition was false, run this alternative path.
            else
            {
                // In byte view, selecting even one of the byte's bits highlights the entire byte token.
                bool selected = selection.ByteHasSelection(byteOffset);
                // Remember a new Rect object using the inputs in parentheses as rect.
                var rect = new Rect(x - 3, y + 2, layout.TokenCharacters * layout.CharacterWidth + 6, 23);
                // If selected is true, record a rectangle with rounded corners; the last two numbers set
                // the horizontal and vertical corner radii. null omits a fill or outline.
                if (selected) drawing.DrawRoundedRectangle(SelectionBrush, null, rect, 3, 3);
                // Record the supplied formatted text at the supplied Point (horizontal x, vertical y).
                drawing.DrawText(Text(DisplayFormatter.FormatByte(value, _document!.Settings.ByteBase), selected ? SelectionTextBrush : modified ? ModifiedBrush : TextBrush), new Point(x, y + 5));
                // If both byteOffset equals selection.ActiveByteOffset and keyboardFocus is true, record a
                // rectangle with rounded corners; the last two numbers set the horizontal and vertical
                // corner radii. null omits a fill or outline.
                if (byteOffset == selection.ActiveByteOffset && keyboardFocus)
                    // Record a rectangle with rounded corners; the last two numbers set the horizontal and
                    // vertical corner radii. null omits a fill or outline.
                    drawing.DrawRoundedRectangle(null, new Pen(SelectionTextBrush, .8), rect, 3, 3);
            }
            // If modified is true, record an oval using the supplied center Point and horizontal/vertical
            // radii; equal radii make a circle.
            if (modified) drawing.DrawEllipse(ModifiedBrush, null, new Point(x - 5, y + 7), 1.5, 1.5);
            // Call DrawFieldUnderlines: Marks bits belonging to named fields.
            DrawFieldUnderlines(drawing, byteOffset, x, y);
        }
        // The formatter controls annotation text and truncation, keeping visible labels consistent
        // with the whole-file export. Colored underlines above are visual hints only.
        if (layout.HasAnnotations)
        {
            // Remember the result returned by DisplayFormatter.FormatRow(...) as rowText.
            var rowText = DisplayFormatter.FormatRow(_document, offset, _document!.Settings);
            // Record the supplied formatted text at the supplied Point (horizontal x, vertical y).
            drawing.DrawText(Text(rowText.Annotations, MutedBrush), new Point(layout.DataX + layout.Gutter, y + 28));
        }
        // Remove the most recently pushed drawing restriction or coordinate change, restoring the preceding
        // drawing state.
        drawing.Pop();

        // ASCII is another view of the same bytes and uses the same selection. Non-printable bytes
        // become dots for legibility; the dots do not replace or modify those bytes in the document.
        if (layout.AsciiWidth > 0)
        {
            // Limit subsequent drawing to the supplied shape until the matching Pop restores the previous
            // drawing state.
            drawing.PushClip(new RectangleGeometry(new Rect(layout.AsciiX + 1, y, Math.Max(0, layout.AsciiWidth - 2), layout.RowHeight)));
            // Repeat with int i = 0 as the starting state, while i is less than length; after each pass,
            // increase i by one. The braces contain one pass.
            for (int i = 0; i < length; i++)
            {
                // Remember layout.AsciiX + layout.Gutter (addition, or joining text) + i multiplied by
                // layout.CharacterWidth (addition, or joining text) as x.
                double x = layout.AsciiX + layout.Gutter + i * layout.CharacterWidth;
                // If x is at least layout.AsciiX + layout.AsciiWidth, run the following instruction.
                if (x >= layout.AsciiX + layout.AsciiWidth) break;
                // Remember _document.Data[offset + i] as value.
                byte value = _document.Data[offset + i];
                // Remember the result returned by selection.ByteHasSelection(...) as selected.
                bool selected = selection.ByteHasSelection(offset + i);
                // If selected is true, record a rectangle to draw: the brush fills its interior, the pen
                // outlines it, and Rect supplies its position and size. null omits a fill or outline.
                if (selected) drawing.DrawRectangle(SelectionBrush, null, new Rect(x, y + 2, layout.CharacterWidth, 23));
                // Record the supplied formatted text at the supplied Point (horizontal x, vertical y).
                drawing.DrawText(Text(value >= 32 && value <= 126 ? ((char)value).ToString() : ".", selected ? SelectionTextBrush : value >= 32 && value <= 126 ? TextBrush : MutedBrush), new Point(x, y + 5));
            }
            // Remove the most recently pushed drawing restriction or coordinate change, restoring the
            // preceding drawing state.
            drawing.Pop();
        }
    }

    /// <summary>
    /// Marks bits belonging to named fields. Bit mode underlines individual members; byte mode
    /// underlines the token when any member belongs to that byte. At most two stacked lines are drawn.
    /// </summary>
    private void DrawFieldUnderlines(DrawingContext drawing, int byteOffset, double x, double y)
    {
        // If _document matches null, or it is not the case that _document!.Settings.ShowAnnotations is
        // true, leave this method immediately.
        if (_document is null || !_document!.Settings.ShowAnnotations) return;
        // Remember byteOffset, converted to long multiplied by 8 as first.
        long first = (long)byteOffset * 8;
        // Remember 0 as lane.
        int lane = 0;
        // Take each item from _document.Fields in turn, call the current item field, and run the following
        // grouped instructions.
        foreach (NamedField field in _document.Fields)
        {
            // If it is not the case that _fieldBits.TryGetValue(field, out var members) is true, run the
            // following instruction.
            if (!_fieldBits.TryGetValue(field, out var members)) continue;
            // Remember false (no) as touchesByte.
            bool touchesByte = false;
            // Repeat with int bit = 0 as the starting state, while bit is less than 8; after each pass,
            // increase bit by one. On each pass, if members.Contains(first + bit) is true, run the
            // following grouped instructions.
            for (int bit = 0; bit < 8; bit++) if (members.Contains(first + bit)) { touchesByte = true; break; }
            // If it is not the case that touchesByte is true, run the following instruction.
            if (!touchesByte) continue;
            // Remember the value at the supplied key, or the fallback when the key is absent as color.
            Brush color = _fieldBrushes.GetValueOrDefault(field, SelectionTextBrush);
            // If layout.BitView is true, run the following grouped instructions.
            if (layout.BitView)
            {
                // Repeat with int bit = 0 as the starting state, while bit is less than 8; after each pass,
                // increase bit by one. On each pass, if members.Contains(first + bit) is true, record a
                // line between the two supplied Points, using the Pen's color and thickness.
                for (int bit = 0; bit < 8; bit++)
                    // If members.Contains(first + bit) is true, record a line between the two supplied
                    // Points, using the Pen's color and thickness.
                    if (members.Contains(first + bit))
                        // Record a line between the two supplied Points, using the Pen's color and
                        // thickness.
                        drawing.DrawLine(new Pen(color, 2), new Point(x + bit * layout.CharacterWidth, y + 25 + lane * 2), new Point(x + (bit + 1) * layout.CharacterWidth, y + 25 + lane * 2));
            }
            // If the preceding condition was false, run this alternative path.
            else drawing.DrawLine(new Pen(color, 2), new Point(x, y + 25 + lane * 2), new Point(x + layout.TokenCharacters * layout.CharacterWidth, y + 25 + lane * 2));
            // Limiting visual lanes prevents overlapping fields from consuming unbounded row height.
            if (++lane == 2) break;
        }
    }

    /// <summary>
    /// Creates text with the current display scale and a fixed culture. InvariantCulture avoids
    /// machine-specific number or punctuation rules changing the visual representation.
    /// </summary>
    private FormattedText Text(string value, Brush color, double size = 14, bool monospace = true) =>
        new(value ?? "", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, monospace ? Mono : Ui, size, color, _pixelsPerDip);

    /// <summary>Draws text inside a width-limited rectangle, restoring the previous clip afterwards.</summary>
    private void DrawClippedText(DrawingContext drawing, string value, double x, double y, double width, Brush color, bool monospace = true, double size = 14)
    {
        // If width is at most 0, leave this method immediately.
        if (width <= 0) return;
        // Limit subsequent drawing to the supplied shape until the matching Pop restores the previous
        // drawing state.
        drawing.PushClip(new RectangleGeometry(new Rect(x, y, width, size + 8)));
        // Record the supplied formatted text at the supplied Point (horizontal x, vertical y).
        drawing.DrawText(Text(value, color, size, monospace), new Point(x, y));
        // Remove the most recently pushed drawing restriction or coordinate change, restoring the preceding
        // drawing state.
        drawing.Pop();
    }

    /// <summary>
    /// Parses a color and freezes the resulting brush. Frozen WPF resources are immutable, allowing
    /// efficient reuse without change tracking; callers cannot accidentally recolor a shared brush.
    /// </summary>
    private static SolidColorBrush Brush(string hex)
    {
        // Remember a new SolidColorBrush object using the inputs in parentheses as brush.
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        // Make this WPF resource unchangeable so it can be safely reused without monitoring it for edits.
        brush.Freeze();
        // Return brush to the caller and leave this method.
        return brush;
    }

}


