using System.Globalization;
using System.Windows;
using System.Windows.Media;
using BitExplorer.Core;

namespace WpfApp1.Controls;

/// <summary>Virtual painting and annotation resources; all positioning comes from the shared layout.</summary>
internal sealed class ByteGridRenderer(ByteGridLayout layout)
{
    private static readonly Typeface Mono = new("Cascadia Mono, Consolas");
    private static readonly Typeface Ui = new("Segoe UI");
    private static readonly Brush BackgroundBrush = Brush("#10151E");
    private static readonly Brush AlternateBrush = Brush("#131A25");
    private static readonly Brush HeaderBrush = Brush("#18212E");
    private static readonly Brush LineBrush = Brush("#293648");
    private static readonly Brush TextBrush = Brush("#D5DFED");
    private static readonly Brush MutedBrush = Brush("#8491A5");
    private static readonly Brush SelectionBrush = Brush("#143E42");
    private static readonly Brush SelectionTextBrush = Brush("#6CE1CE");
    private static readonly Brush ModifiedBrush = Brush("#E6B76A");
    private static readonly Brush RulerBrush = Brush("#A2B0C2");
    private readonly Dictionary<NamedField, HashSet<long>> _fieldBits = [];
    private readonly Dictionary<NamedField, Brush> _fieldBrushes = [];
    private DocumentModel? _document;
    private double _pixelsPerDip = 1;

    public double MeasureCharacterWidth(double pixelsPerDip)
    {
        _pixelsPerDip = pixelsPerDip;
        return Text("0", TextBrush).WidthIncludingTrailingWhitespace;
    }

    public void Refresh(DocumentModel? document)
    {
        _document = document;
        _fieldBits.Clear();
        _fieldBrushes.Clear();
        if (document is null) return;
        foreach (var field in document.Fields)
        {
            _fieldBits[field] = new HashSet<long>(field.OrderedBits);
            try { _fieldBrushes[field] = Brush(field.Color); }
            catch (Exception ex) when (ex is FormatException or NotSupportedException or ArgumentException)
            { _fieldBrushes[field] = SelectionTextBrush; }
        }
    }
    public void Render(DrawingContext drawing, BitSelection selection, Size renderSize, bool keyboardFocus, double pixelsPerDip)
    {
        _pixelsPerDip = pixelsPerDip;
        drawing.DrawRectangle(BackgroundBrush, null, new Rect(renderSize));
        if (_document is null) return;
        drawing.PushTransform(new TranslateTransform(-layout.HorizontalOffset, 0));
        drawing.PushClip(new RectangleGeometry(new Rect(layout.HorizontalOffset, layout.HeaderHeight, renderSize.Width, Math.Max(0, renderSize.Height - layout.HeaderHeight))));

        var (first, last) = layout.VisibleRows(renderSize.Height);
        for (int row = first; row < last; row++)
            DrawRow(drawing, row, layout.HeaderHeight + row * layout.RowHeight - layout.VerticalOffset, selection, renderSize, keyboardFocus);
        drawing.Pop();
        DrawHeader(drawing, renderSize);
        DrawColumnDividers(drawing, renderSize);
        drawing.Pop();

        if (_document.Data.Length == 0)
        {
            var title = Text("Open a binary file to begin exploring", TextBrush, 16, false);
            drawing.DrawText(title, new Point(Math.Max(24, (renderSize.Width - title.Width) / 2), layout.HeaderHeight + 96));
            var note = Text("Bytes, bits, and named fields stay connected as you explore.", MutedBrush, 12, false);
            drawing.DrawText(note, new Point(Math.Max(24, (renderSize.Width - note.Width) / 2), layout.HeaderHeight + 126));
        }
    }

    private void DrawHeader(DrawingContext drawing, Size renderSize)
    {
        drawing.DrawRectangle(HeaderBrush, null, new Rect(0, 0, Math.Max(layout.Extent.Width, renderSize.Width + layout.HorizontalOffset), layout.HeaderHeight));
        if (layout.OffsetWidth > 0) DrawClippedText(drawing, "OFFSET · " + (_document!.Settings.OffsetBase == NumericBase.Hexadecimal ? "HEX" : "DEC"), layout.Gutter, 10, layout.OffsetWidth - layout.Gutter * 2, MutedBrush, false, 10);
        string label = layout.BitView ? "BITS  ·  MS bit → LS bit" : "BYTES  ·  " + (_document!.Settings.ByteBase == NumericBase.Hexadecimal ? "HEXADECIMAL" : "DECIMAL");
        DrawClippedText(drawing, label, layout.DataX + layout.Gutter, 10, layout.DataWidth - layout.Gutter * 2, RulerBrush, false, 10);
        if (layout.AsciiWidth > 0) DrawClippedText(drawing, "TEXT / ASCII", layout.AsciiX + layout.Gutter, 10, layout.AsciiWidth - layout.Gutter * 2, MutedBrush, false, 10);
        if (_document!.Settings.ShowRuler)
        {
            if (layout.OffsetWidth > 0) DrawClippedText(drawing, layout.BitView ? "bit index" : "+ position", layout.Gutter, 36, layout.OffsetWidth - layout.Gutter * 2, MutedBrush, false, 10);
            drawing.PushClip(new RectangleGeometry(new Rect(layout.DataX + 1, 30, Math.Max(0, layout.DataWidth - 2), 31)));
            string ruler = DisplayFormatter.GetRuler(_document!.Settings);
            drawing.DrawText(Text(ruler, MutedBrush, 14), new Point(layout.DataX + layout.Gutter, 35));
            drawing.Pop();
            if (layout.AsciiWidth > 0) DrawClippedText(drawing, "printable characters", layout.AsciiX + layout.Gutter, 36, layout.AsciiWidth - layout.Gutter * 2, MutedBrush, false, 10);
        }
        drawing.DrawLine(new Pen(LineBrush, 1), new Point(0, layout.HeaderHeight - .5), new Point(Math.Max(layout.Extent.Width, renderSize.Width), layout.HeaderHeight - .5));
    }

    private void DrawColumnDividers(DrawingContext drawing, Size renderSize)
    {
        var pen = new Pen(LineBrush, 1);
        if (layout.OffsetWidth > 0) drawing.DrawLine(pen, new Point(layout.DataX, 0), new Point(layout.DataX, renderSize.Height));
        drawing.DrawLine(pen, new Point(layout.AsciiX, 0), new Point(layout.AsciiX, renderSize.Height));
        if (layout.AsciiWidth > 0) drawing.DrawLine(pen, new Point(layout.AsciiX + layout.AsciiWidth, 0), new Point(layout.AsciiX + layout.AsciiWidth, renderSize.Height));
        foreach (double x in layout.VisibleDividers())
        {
            drawing.DrawLine(new Pen(MutedBrush, 1), new Point(x - 2, 12), new Point(x - 2, 23));
            drawing.DrawLine(new Pen(MutedBrush, 1), new Point(x + 2, 12), new Point(x + 2, 23));
        }
    }

    private void DrawRow(DrawingContext drawing, int row, double y, BitSelection selection, Size renderSize, bool keyboardFocus)
    {
        if (_document is null) return;
        int offset = row * layout.BytesPerRow;
        int length = Math.Min(layout.BytesPerRow, _document.Data.Length - offset);
        if ((row & 1) == 1) drawing.DrawRectangle(AlternateBrush, null, new Rect(0, y, Math.Max(layout.Extent.Width, renderSize.Width), layout.RowHeight));

        if (layout.OffsetWidth > 0)
            DrawClippedText(drawing, DisplayFormatter.FormatOffset(offset, _document!.Settings.OffsetBase), layout.Gutter, y + 5, layout.OffsetWidth - layout.Gutter * 2, MutedBrush);

        drawing.PushClip(new RectangleGeometry(new Rect(layout.DataX + 1, y, Math.Max(0, layout.DataWidth - 2), layout.RowHeight)));
        for (int i = 0; i < length; i++)
        {
            int byteOffset = offset + i;
            double x = layout.DataX + layout.Gutter + i * layout.CellWidth;
            if (x >= layout.AsciiX) break;
            bool modified = _document.IsByteModified(byteOffset);
            byte value = _document.Data[byteOffset];
            if (layout.BitView)
            {
                for (int bit = 0; bit < 8; bit++)
                {
                    long address = (long)byteOffset * 8 + bit;
                    bool selected = selection.Contains(address);
                    var rect = new Rect(x + bit * layout.CharacterWidth - .5, y + 2, layout.CharacterWidth + .25, 23);
                    if (selected) drawing.DrawRectangle(SelectionBrush, null, rect);
                    drawing.DrawText(Text((value & (1 << (7 - bit))) != 0 ? "1" : "0", selected ? SelectionTextBrush : modified ? ModifiedBrush : TextBrush), new Point(x + bit * layout.CharacterWidth, y + 5));
                    if (address == selection.ActiveBit && keyboardFocus)
                        drawing.DrawRectangle(null, new Pen(SelectionTextBrush, .8), rect);
                }
            }
            else
            {
                bool selected = selection.ByteHasSelection(byteOffset);
                var rect = new Rect(x - 3, y + 2, layout.TokenCharacters * layout.CharacterWidth + 6, 23);
                if (selected) drawing.DrawRoundedRectangle(SelectionBrush, null, rect, 3, 3);
                drawing.DrawText(Text(DisplayFormatter.FormatByte(value, _document!.Settings.ByteBase), selected ? SelectionTextBrush : modified ? ModifiedBrush : TextBrush), new Point(x, y + 5));
                if (byteOffset == selection.ActiveByteOffset && keyboardFocus)
                    drawing.DrawRoundedRectangle(null, new Pen(SelectionTextBrush, .8), rect, 3, 3);
            }
            if (modified) drawing.DrawEllipse(ModifiedBrush, null, new Point(x - 5, y + 7), 1.5, 1.5);
            DrawFieldUnderlines(drawing, byteOffset, x, y);
        }
        if (layout.HasAnnotations)
        {
            var rowText = DisplayFormatter.FormatRow(_document, offset, _document!.Settings);
            drawing.DrawText(Text(rowText.Annotations, MutedBrush), new Point(layout.DataX + layout.Gutter, y + 28));
        }
        drawing.Pop();

        if (layout.AsciiWidth > 0)
        {
            drawing.PushClip(new RectangleGeometry(new Rect(layout.AsciiX + 1, y, Math.Max(0, layout.AsciiWidth - 2), layout.RowHeight)));
            for (int i = 0; i < length; i++)
            {
                double x = layout.AsciiX + layout.Gutter + i * layout.CharacterWidth;
                if (x >= layout.AsciiX + layout.AsciiWidth) break;
                byte value = _document.Data[offset + i];
                bool selected = selection.ByteHasSelection(offset + i);
                if (selected) drawing.DrawRectangle(SelectionBrush, null, new Rect(x, y + 2, layout.CharacterWidth, 23));
                drawing.DrawText(Text(value >= 32 && value <= 126 ? ((char)value).ToString() : ".", selected ? SelectionTextBrush : value >= 32 && value <= 126 ? TextBrush : MutedBrush), new Point(x, y + 5));
            }
            drawing.Pop();
        }
    }

    private void DrawFieldUnderlines(DrawingContext drawing, int byteOffset, double x, double y)
    {
        if (_document is null || !_document!.Settings.ShowAnnotations) return;
        long first = (long)byteOffset * 8;
        int lane = 0;
        foreach (NamedField field in _document.Fields)
        {
            if (!_fieldBits.TryGetValue(field, out var members)) continue;
            bool touchesByte = false;
            for (int bit = 0; bit < 8; bit++) if (members.Contains(first + bit)) { touchesByte = true; break; }
            if (!touchesByte) continue;
            Brush color = _fieldBrushes.GetValueOrDefault(field, SelectionTextBrush);
            if (layout.BitView)
            {
                for (int bit = 0; bit < 8; bit++)
                    if (members.Contains(first + bit))
                        drawing.DrawLine(new Pen(color, 2), new Point(x + bit * layout.CharacterWidth, y + 25 + lane * 2), new Point(x + (bit + 1) * layout.CharacterWidth, y + 25 + lane * 2));
            }
            else drawing.DrawLine(new Pen(color, 2), new Point(x, y + 25 + lane * 2), new Point(x + layout.TokenCharacters * layout.CharacterWidth, y + 25 + lane * 2));
            if (++lane == 2) break;
        }
    }

    private FormattedText Text(string value, Brush color, double size = 14, bool monospace = true) =>
        new(value ?? "", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, monospace ? Mono : Ui, size, color, _pixelsPerDip);

    private void DrawClippedText(DrawingContext drawing, string value, double x, double y, double width, Brush color, bool monospace = true, double size = 14)
    {
        if (width <= 0) return;
        drawing.PushClip(new RectangleGeometry(new Rect(x, y, width, size + 8)));
        drawing.DrawText(Text(value, color, size, monospace), new Point(x, y));
        drawing.Pop();
    }

    private static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

}


