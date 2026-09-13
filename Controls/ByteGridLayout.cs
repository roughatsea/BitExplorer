using System.Windows;
using BitExplorer.Core;

namespace WpfApp1.Controls;

/// <summary>One geometry model for drawing, hit testing, scrolling, and export-aligned columns.</summary>
internal sealed class ByteGridLayout
{
    private DisplaySettings? _settings;
    private int _documentLength;
    private int _offsetCells = 14;
    private int _dataCells = 88;
    private int _asciiCells = 23;
    private int _previousBytesPerRow = 16;
    private double _previousRowHeight = 29;

    public double CharacterWidth { get; private set; } = 8.43;
    public bool BitView => _settings?.ViewMode == DataViewMode.Bits;
    public int BytesPerRow => Math.Clamp(_settings?.BytesPerRow ?? 16, 1, 256);
    public bool HasAnnotations { get; private set; }
    public double RowHeight => HasAnnotations ? 47 : 29;
    public double HeaderHeight => _settings?.ShowRuler == true ? 64 : 37;
    public int TokenCharacters => BitView ? 8 : _settings?.ByteBase == NumericBase.Decimal ? 3 : 2;
    public double CellWidth => (TokenCharacters + 1) * CharacterWidth;
    public double Gutter => 2 * CharacterWidth;
    public double OffsetWidth => _offsetCells * CharacterWidth;
    public double DataWidth => _dataCells * CharacterWidth;
    public double AsciiWidth => _asciiCells * CharacterWidth;
    public double DataX => OffsetWidth;
    public double AsciiX => OffsetWidth + DataWidth;
    public int RowCount => (int)(((long)_documentLength + BytesPerRow - 1) / BytesPerRow);
    public Size Viewport { get; private set; }
    public Size Extent { get; private set; }
    public double HorizontalOffset { get; private set; }
    public double VerticalOffset { get; private set; }

    /// <returns>Whether automatic fitting changed the shared display settings.</returns>
    public bool Update(DocumentModel? document, double characterWidth)
    {
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
                int count = Math.Clamp((int)Math.Floor((_settings.DataWidth - Gutter * 2 + CharacterWidth) / CellWidth), 1, 256);
                if (_settings.BytesPerRow != count)
                {
                    _settings.BytesPerRow = count;
                    changed = true;
                }
            }
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

    public void SetViewport(Size viewport)
    {
        Viewport = viewport;
        ClampOffsets();
    }

    public void ResetScroll() => HorizontalOffset = VerticalOffset = 0;

    public void SetHorizontalOffset(double offset)
    {
        if (!double.IsNaN(offset)) HorizontalOffset = Math.Clamp(offset, 0, Math.Max(0, Extent.Width - Viewport.Width));
    }

    public void SetVerticalOffset(double offset)
    {
        if (!double.IsNaN(offset)) VerticalOffset = Math.Clamp(offset, 0, Math.Max(0, Extent.Height - Viewport.Height));
    }

    public void ScrollToByte(int offset)
    {
        if (offset < 0 || offset >= _documentLength) return;
        double top = (offset / BytesPerRow) * RowHeight;
        double bodyHeight = Math.Max(RowHeight, Viewport.Height - HeaderHeight);
        if (top < VerticalOffset) SetVerticalOffset(top);
        else if (top + RowHeight > VerticalOffset + bodyHeight) SetVerticalOffset(top + RowHeight - bodyHeight);
    }

    public (int First, int Last) VisibleRows(double height)
    {
        int first = Math.Max(0, (int)(VerticalOffset / RowHeight));
        return (first, Math.Min(RowCount, first + (int)Math.Ceiling(height / RowHeight) + 1));
    }

    public IEnumerable<double> VisibleDividers()
    {
        if (OffsetWidth > 0) yield return DataX;
        yield return AsciiX;
        if (AsciiWidth > 0) yield return AsciiX + AsciiWidth;
    }

    public double GetColumnWidth(int column) => column switch { 0 => OffsetWidth, 1 => DataWidth, 2 => AsciiWidth, _ => throw new ArgumentOutOfRangeException(nameof(column)) };

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

    public int HitDivider(Point point)
    {
        if (point.Y > HeaderHeight) return -1;
        double x = point.X + HorizontalOffset;
        if (OffsetWidth > 0 && Math.Abs(x - DataX) <= 6) return 0;
        if (Math.Abs(x - AsciiX) <= 6) return 1;
        if (AsciiWidth > 0 && Math.Abs(x - AsciiX - AsciiWidth) <= 6) return 2;
        return -1;
    }

    public (long Bit, bool WholeByte) HitData(Point point, bool clamp = false)
    {
        if (_documentLength == 0 || (!clamp && point.Y < HeaderHeight)) return (-1, false);
        int row = Math.Clamp((int)Math.Floor((Math.Max(HeaderHeight, point.Y) - HeaderHeight + VerticalOffset) / RowHeight), 0, Math.Max(0, RowCount - 1));
        if (!clamp && point.Y - HeaderHeight + VerticalOffset >= RowCount * RowHeight) return (-1, false);
        double x = point.X + HorizontalOffset;
        bool ascii = AsciiWidth > 0 && x >= AsciiX && x < AsciiX + AsciiWidth;
        if (!clamp && !ascii && (x < DataX + Gutter || x >= AsciiX)) return (-1, false);
        double position = ascii ? (x - AsciiX - Gutter) / CharacterWidth : (x - DataX - Gutter) / CellWidth;
        if (!clamp && (position < 0 || position >= BytesPerRow)) return (-1, false);
        int column = Math.Clamp((int)Math.Floor(position), 0, BytesPerRow - 1);
        int offset = row * BytesPerRow + column;
        if (offset >= _documentLength)
        {
            if (!clamp) return (-1, false);
            offset = _documentLength - 1;
        }
        int bit = BitView && !ascii ? Math.Clamp((int)Math.Floor((x - DataX - Gutter - column * CellWidth) / CharacterWidth), 0, 7) : 0;
        return ((long)offset * 8 + bit, !BitView || ascii);
    }

    private void ClampOffsets()
    {
        SetHorizontalOffset(HorizontalOffset);
        SetVerticalOffset(VerticalOffset);
    }
}
