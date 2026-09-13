using System.Windows;
using BitExplorer.Core;

namespace WpfApp1.Controls;

/// <summary>
/// Calculates where bytes, bits, and columns appear. Both drawing and hit testing (turning a mouse
/// position into a byte/bit address) use these calculations, so clickable targets match the text.
/// </summary>
/// <remarks>
/// Geometry uses WPF device-independent units, often called pixels in UI code. Text export uses
/// whole character cells instead. CharacterWidth bridges those systems: with an 8-unit glyph,
/// a 12-character offset column occupies 96 units on screen and 12 character positions in text
/// (including the offset's digits and surrounding padding).
/// </remarks>
internal sealed class ByteGridLayout
{
    // Keep the last calculated column widths and row geometry. Remembering the previous row size
    // lets a format change preserve the file position instead of jumping to an unrelated row.
    private DisplaySettings? _settings;
    private int _documentLength;
    private int _offsetCells = 14;
    private int _dataCells = 88;
    private int _asciiCells = 23;
    private int _previousBytesPerRow = 16;
    private double _previousRowHeight = 29;

    /// <summary>Measured advance of one monospace character: the distance from one glyph to the next.</summary>
    public double CharacterWidth { get; private set; } = 8.43;
    /// <summary>Whether each data token is eight individual bit characters instead of a byte value.</summary>
    public bool BitView => _settings?.ViewMode == DataViewMode.Bits;
    /// <summary>Number of file bytes in each displayed row, constrained to the supported 1–256 range.</summary>
    public int BytesPerRow => Math.Clamp(_settings?.BytesPerRow ?? 16, 1, 256);
    /// <summary>Whether named-field annotations need an extra text line below each data row.</summary>
    public bool HasAnnotations { get; private set; }
    /// <summary>Vertical space used by one data row, increased when annotations are shown.</summary>
    public double RowHeight => HasAnnotations ? 47 : 29;
    /// <summary>Stationary header height; the optional bit/byte ruler needs a second line.</summary>
    public double HeaderHeight => _settings?.ShowRuler == true ? 64 : 37;
    /// <summary>Characters in a byte's token: eight binary digits, three decimal digits, or two hex digits.</summary>
    public int TokenCharacters => BitView ? 8 : _settings?.ByteBase == NumericBase.Decimal ? 3 : 2;
    /// <summary>Width of one byte token plus its separating space; for hex, “FF ” uses three characters.</summary>
    public double CellWidth => (TokenCharacters + 1) * CharacterWidth;
    /// <summary>Empty margin at either side of a column, matching two spaces in text export.</summary>
    public double Gutter => 2 * CharacterWidth;
    /// <summary>Offset column width converted from whole text characters to screen units; zero means hidden.</summary>
    public double OffsetWidth => _offsetCells * CharacterWidth;
    /// <summary>Data column width, large enough for every token in the chosen row format.</summary>
    public double DataWidth => _dataCells * CharacterWidth;
    /// <summary>ASCII column width converted from character cells; zero means hidden.</summary>
    public double AsciiWidth => _asciiCells * CharacterWidth;
    /// <summary>Data column's left edge in content coordinates, immediately after the offset column.</summary>
    public double DataX => OffsetWidth;
    /// <summary>ASCII column's left edge, immediately after the offset and data columns.</summary>
    public double AsciiX => OffsetWidth + DataWidth;
    /// <summary>Number of rows, rounding up so a partly filled final row still appears.</summary>
    public int RowCount => (int)(((long)_documentLength + BytesPerRow - 1) / BytesPerRow);
    /// <summary>Size of the visible window through the content.</summary>
    public Size Viewport { get; private set; }
    /// <summary>Size of all scrollable content, including rows and columns outside the viewport.</summary>
    public Size Extent { get; private set; }
    /// <summary>How far the viewport has moved right from the content's left edge.</summary>
    public double HorizontalOffset { get; private set; }
    /// <summary>How far the viewport has moved down through the document body.</summary>
    public double VerticalOffset { get; private set; }

    /// <summary>
    /// Recalculates geometry from the document and measured font width. The formatter supplies
    /// column character counts so the screen and whole-file text export use identical spacing.
    /// </summary>
    /// <returns>Whether automatic fitting changed the shared display settings.</returns>
    public bool Update(DocumentModel? document, double characterWidth)
    {
        // Remember the old top row's first byte and how far we scrolled into that row.
        // After regrouping, put the row containing that byte at the top, retaining fractional
        // progress subject to the scroll limits. Its first byte can differ: byte 16 is in
        // row 1 at 16 bytes per row, but in row 0 at 24 bytes per row.
        double previousRowPosition = VerticalOffset / _previousRowHeight;
        long topByte = (long)Math.Floor(previousRowPosition) * _previousBytesPerRow;
        _settings = document?.Settings;
        _documentLength = document?.Data.Length ?? 0;
        CharacterWidth = characterWidth;
        HasAnnotations = _settings?.ShowAnnotations == true && document?.Fields.Count > 0;
        bool changed = false;
        if (_settings is not null)
        {
            _settings.CharacterWidth = characterWidth;
            if (_settings.AutoBytesPerRow)
            {
                // Remove the left/right margins. Add back the final separating space, because
                // the last token has no following token. Floor chooses only completely fitting bytes.
                int count = Math.Clamp((int)Math.Floor((_settings.DataWidth - Gutter * 2 + CharacterWidth) / CellWidth), 1, 256);
                if (_settings.BytesPerRow != count)
                {
                    _settings.BytesPerRow = count;
                    changed = true;
                }
            }
            // The formatter also expands an undersized fixed-width column to avoid cutting off data.
            // Do not maintain a second screen-only version of that minimum-width calculation here.
            var columns = DisplayFormatter.GetColumnCharacterWidths(document!, _settings);
            _offsetCells = columns.Offset;
            _dataCells = columns.Data;
            _asciiCells = columns.Ascii;
        }
        if (_previousBytesPerRow != BytesPerRow || _previousRowHeight != RowHeight)
        {
            VerticalOffset = (topByte / BytesPerRow + (previousRowPosition - Math.Floor(previousRowPosition))) * RowHeight;
            _previousBytesPerRow = BytesPerRow;
            _previousRowHeight = RowHeight;
        }
        Extent = new Size(OffsetWidth + DataWidth + AsciiWidth, HeaderHeight + RowCount * RowHeight);
        ClampOffsets();
        return changed;
    }

    /// <summary>Stores the newly assigned visible size and prevents scrolling past the content edges.</summary>
    public void SetViewport(Size viewport)
    {
        Viewport = viewport;
        ClampOffsets();
    }

    /// <summary>Returns both scroll directions to the beginning, normally after opening another file.</summary>
    public void ResetScroll() => HorizontalOffset = VerticalOffset = 0;

    /// <summary>Sets horizontal scrolling within its legal range; ignores NaN, an undefined numeric value.</summary>
    public void SetHorizontalOffset(double offset)
    {
        if (!double.IsNaN(offset)) HorizontalOffset = Math.Clamp(offset, 0, Math.Max(0, Extent.Width - Viewport.Width));
    }

    /// <summary>Sets vertical scrolling within its legal range, also ignoring undefined numeric input.</summary>
    public void SetVerticalOffset(double offset)
    {
        if (!double.IsNaN(offset)) VerticalOffset = Math.Clamp(offset, 0, Math.Max(0, Extent.Height - Viewport.Height));
    }

    /// <summary>Reveals a byte's complete row with the smallest necessary vertical scroll adjustment.</summary>
    public void ScrollToByte(int offset)
    {
        if (offset < 0 || offset >= _documentLength) return;
        double top = (offset / BytesPerRow) * RowHeight;
        // The header stays fixed, so only the space beneath it can show document rows.
        double bodyHeight = Math.Max(RowHeight, Viewport.Height - HeaderHeight);
        if (top < VerticalOffset) SetVerticalOffset(top);
        else if (top + RowHeight > VerticalOffset + bodyHeight) SetVerticalOffset(top + RowHeight - bodyHeight);
    }

    /// <summary>
    /// Returns a first row and exclusive end row for painting. An extra row covers partial rows at
    /// the viewport edge. This is virtualization: draw visible data rather than processing every row.
    /// </summary>
    public (int First, int Last) VisibleRows(double height)
    {
        int first = Math.Max(0, (int)(VerticalOffset / RowHeight));
        return (first, Math.Min(RowCount, first + (int)Math.Ceiling(height / RowHeight) + 1));
    }

    /// <summary>Enumerates the boundaries of currently visible columns for drawing resize handles.</summary>
    public IEnumerable<double> VisibleDividers()
    {
        if (OffsetWidth > 0) yield return DataX;
        yield return AsciiX;
        if (AsciiWidth > 0) yield return AsciiX + AsciiWidth;
    }

    /// <summary>Returns the actual displayed width of column 0 (offset), 1 (data), or 2 (ASCII).</summary>
    public double GetColumnWidth(int column) => column switch { 0 => OffsetWidth, 1 => DataWidth, 2 => AsciiWidth, _ => throw new ArgumentOutOfRangeException(nameof(column)) };

    /// <summary>
    /// Updates the requested width while a header divider is dragged. The next Update converts it
    /// to complete character cells; fixed bytes-per-row requirements may keep the displayed width larger.
    /// </summary>
    public void ResizeColumn(int column, double width)
    {
        if (_settings is null) return;
        switch (column)
        {
            case 0: _settings.OffsetWidth = Math.Clamp(width, 60, 1000); break;
            case 1: _settings.DataWidth = Math.Clamp(width, 80, 12000); break;
            case 2: _settings.AsciiWidth = Math.Clamp(width, 40, 4000); break;
            default: throw new ArgumentOutOfRangeException(nameof(column));
        }
    }

    /// <summary>
    /// Finds a resize handle within six screen units of a column boundary in the header.
    /// A pointer is viewport-relative, so horizontal scrolling is added to find its content coordinate.
    /// </summary>
    public int HitDivider(Point point)
    {
        if (point.Y > HeaderHeight) return -1;
        double x = point.X + HorizontalOffset;
        if (OffsetWidth > 0 && Math.Abs(x - DataX) <= 6) return 0;
        if (Math.Abs(x - AsciiX) <= 6) return 1;
        if (AsciiWidth > 0 && Math.Abs(x - AsciiX - AsciiWidth) <= 6) return 2;
        return -1;
    }

    /// <summary>
    /// Maps a point on the grid to a physical bit address. WholeByte is true for byte/ASCII cells;
    /// in bit mode each binary character can be selected separately. Bit -1 means there was no data hit.
    /// With clamp enabled, a drag beyond the data edges sticks to the closest valid byte/bit.
    /// </summary>
    public (long Bit, bool WholeByte) HitData(Point point, bool clamp = false)
    {
        if (_documentLength == 0 || (!clamp && point.Y < HeaderHeight)) return (-1, false);
        // Subtract the fixed header, undo vertical scrolling, then divide by row height.
        // Integer row/column positions identify a byte: with 16 bytes per row, row 2 + column 3 is byte 35.
        int row = Math.Clamp((int)Math.Floor((Math.Max(HeaderHeight, point.Y) - HeaderHeight + VerticalOffset) / RowHeight), 0, Math.Max(0, RowCount - 1));
        if (!clamp && point.Y - HeaderHeight + VerticalOffset >= RowCount * RowHeight) return (-1, false);
        double x = point.X + HorizontalOffset;
        bool ascii = AsciiWidth > 0 && x >= AsciiX && x < AsciiX + AsciiWidth;
        if (!clamp && !ascii && (x < DataX + Gutter || x >= AsciiX)) return (-1, false);
        // ASCII uses one character per byte; data uses a complete token and separating space.
        double position = ascii ? (x - AsciiX - Gutter) / CharacterWidth : (x - DataX - Gutter) / CellWidth;
        if (!clamp && (position < 0 || position >= BytesPerRow)) return (-1, false);
        int column = Math.Clamp((int)Math.Floor(position), 0, BytesPerRow - 1);
        int offset = row * BytesPerRow + column;
        if (offset >= _documentLength)
        {
            if (!clamp) return (-1, false);
            offset = _documentLength - 1;
        }
        // Within byte 35, the leftmost displayed bit is physical address 35*8 = 280.
        // Ruler numbering may call that bit 7 or bit 0; the physical address does not change.
        int bit = BitView && !ascii ? Math.Clamp((int)Math.Floor((x - DataX - Gutter - column * CellWidth) / CharacterWidth), 0, 7) : 0;
        return ((long)offset * 8 + bit, !BitView || ascii);
    }

    /// <summary>Rechecks both scroll positions after content or viewport size changes.</summary>
    private void ClampOffsets()
    {
        SetHorizontalOffset(HorizontalOffset);
        SetVerticalOffset(VerticalOffset);
    }
}
