using System.Windows;
using BitExplorer.Core;
using WpfApp1.Controls;

// These tests exercise geometry without showing a window. Supplying an exact 8-unit character width
// makes every expected coordinate reproducible across fonts and monitor scaling. WPF's Point and Size
// still use device-independent units; the tests do not assume those units equal physical monitor pixels.
internal static partial class Program
{
    /// <summary>Drawing/export column positions and mouse hit testing must identify the same bytes and bits, even after scrolling.</summary>
    private static void GridHitTesting()
    {
        // Arrange: five ASCII bytes (41..45 hex = A..E), three per row, leave a partly filled second row.
        // The 320-by-120 viewport creates scrollable content, while direct layout calls let us test any
        // content coordinate, including columns that a real viewport would currently clip off-screen.
        var document = new DocumentModel([0x41, 0x42, 0x43, 0x44, 0x45]);
        document.Settings.BytesPerRow = 3;
        var layout = new ByteGridLayout();
        layout.Update(document, 8);
        layout.SetViewport(new Size(320, 120));
        var columns = DisplayFormatter.GetColumnCharacterWidths(document, document.Settings);
        // Check the bridge from plain-text columns to screen coordinates: character counts multiplied
        // by the supplied 8-unit glyph width must match each column's left edge exactly.
        Equal(columns.Offset * 8d, layout.DataX);
        Equal((columns.Offset + columns.Data) * 8d, layout.AsciiX);
        // Act/check: move two byte cells from the data start, then four units inside the token.
        // This is byte index 2, whose first physical bit is 2*8 = 16; byte mode selects the whole byte.
        // Five units below the header stays safely inside row 0 rather than hitting its boundary.
        var hit = layout.HitData(new Point(layout.DataX + layout.Gutter + 2 * layout.CellWidth + 4, layout.HeaderHeight + 5));
        Equal((16L, true), hit);
        // ASCII uses one character per byte, so moving two character widths must identify the same byte.
        hit = layout.HitData(new Point(layout.AsciiX + layout.Gutter + 2 * layout.CharacterWidth + 4, layout.HeaderHeight + 5));
        Equal((16L, true), hit);
        // One unit above the body belongs to the header, not the data; -1 is the "no data hit" marker.
        Equal((-1L, false), layout.HitData(new Point(layout.DataX + layout.Gutter, layout.HeaderHeight - 1)));

        document.Settings.ViewMode = DataViewMode.Bits;
        layout.Update(document, 8);
        // Switch to individual bits and scroll right by 48 units. A pointer position is relative to
        // the viewport, so subtract the scroll offset from the desired content coordinate.
        layout.SetHorizontalOffset(48);
        hit = layout.HitData(new Point(layout.DataX + layout.Gutter + layout.CellWidth + 3.5 * layout.CharacterWidth - layout.HorizontalOffset,
            layout.HeaderHeight + 5));
        // One complete byte cell plus 3.5 glyphs targets the middle of byte 1's fourth displayed bit:
        // physical address 1*8 + 3 = 11. false means this is an individual bit, not a whole-byte hit.
        Equal((11L, false), hit);
        var absent = new Point(layout.DataX + layout.Gutter + 2 * layout.CellWidth - layout.HorizontalOffset,
            layout.HeaderHeight + layout.RowHeight + 5);
        // The third cell of row 1 would be byte 1*3 + 2 = 5, but only indices 0..4 exist.
        // The grid must not invent a byte in that empty part of the final row.
        Equal((-1L, false), layout.HitData(absent));
        // Header divider 0 is the offset/data boundary. Its hit target must move with horizontal scrolling.
        Equal(0, layout.HitDivider(new Point(layout.DataX - layout.HorizontalOffset, 10)));
    }

    /// <summary>Automatic fitting may change row grouping; manual grouping and the top source byte must survive later layout changes.</summary>
    private static void GridFittingAndReflow()
    {
        // Arrange: enough bytes to scroll well away from the ends, with a requested 128-unit data width.
        var document = new DocumentModel(new byte[4096]);
        var layout = new ByteGridLayout();
        document.Settings.DataWidth = 128;
        document.Settings.AutoBytesPerRow = true;
        // Act/check: each edge gutter is 2*8 = 16 units. Hex cells use (2 digits + 1 space)*8 = 24.
        // Accounting for the absent final space gives floor((128 - 32 + 8)/24) = 4 fitting bytes.
        // Update returns true because automatic fitting changed the document's bytes-per-row setting.
        Equal(true, layout.Update(document, 8));
        Equal(4, document.Settings.BytesPerRow);
        // Eight binary digits plus a space use 9*8 = 72 units per byte, so that same width fits only
        // floor((128 - 32 + 8)/72) = 1 byte in bit mode.
        document.Settings.ViewMode = DataViewMode.Bits;
        Equal(true, layout.Update(document, 8));
        Equal(1, document.Settings.BytesPerRow);
        // Turn off auto fit and request 16 bytes explicitly. Layout must retain that grouping and
        // let the data column expand/scroll; returning false means no automatic row-count change occurred.
        document.Settings.AutoBytesPerRow = false;
        document.Settings.BytesPerRow = 16;
        Equal(false, layout.Update(document, 8));
        Equal(16, document.Settings.BytesPerRow);
        layout.SetViewport(new Size(320, 200));
        // Scroll to row 20 at 16 bytes per row: its first source byte is 20*16 = 320.
        // Reflow to eight bytes per row should put byte 320 at the top again (now row 40).
        layout.SetVerticalOffset(layout.RowHeight * 20);
        document.Settings.BytesPerRow = 8;
        layout.Update(document, 8);
        Equal(320L, (long)Math.Floor(layout.VerticalOffset / layout.RowHeight) * document.Settings.BytesPerRow);
        // Showing annotations increases every row from 29 to 47 units, even though this flag touches
        // only byte 0. The taller rows must still preserve top source byte 320 rather than raw pixels.
        document.AddField(new NamedField { Name = "Flag", OrderedBits = [0] });
        layout.Update(document, 8);
        Equal(47d, layout.RowHeight);
        Equal(320L, (long)Math.Floor(layout.VerticalOffset / layout.RowHeight) * document.Settings.BytesPerRow);
    }
}
