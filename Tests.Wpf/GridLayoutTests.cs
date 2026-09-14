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
// Make names from BitExplorer.Core available here without writing their full prefix each time. This does
// not run that library's code.
using BitExplorer.Core;
// Make names from WpfApp1.Controls available here without writing their full prefix each time. This does
// not run that library's code.
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
        // Set document.Settings.BytesPerRow to 3.
        document.Settings.BytesPerRow = 3;
        // Remember a new ByteGridLayout object as layout.
        var layout = new ByteGridLayout();
        // Call layout.Update: Recalculates geometry from the document and measured font width.
        layout.Update(document, 8);
        // Call layout.SetViewport: Stores the newly assigned visible size and prevents scrolling past the
        // content edges.
        layout.SetViewport(new Size(320, 120));
        // Remember the result returned by DisplayFormatter.GetColumnCharacterWidths(...) as columns.
        var columns = DisplayFormatter.GetColumnCharacterWidths(document, document.Settings);
        // Check the bridge from plain-text columns to screen coordinates: character counts multiplied
        // by the supplied 8-unit glyph width must match each column's left edge exactly.
        Equal(columns.Offset * 8d, layout.DataX);
        // Check that layout.AsciiX equals the expected (columns.Offset + columns.Data) * 8d; a mismatch
        // fails this test.
        Equal((columns.Offset + columns.Data) * 8d, layout.AsciiX);
        // Act/check: move two byte cells from the data start, then four units inside the token.
        // This is byte index 2, whose first physical bit is 2*8 = 16; byte mode selects the whole byte.
        // Five units below the header stays safely inside row 0 rather than hitting its boundary.
        var hit = layout.HitData(new Point(layout.DataX + layout.Gutter + 2 * layout.CellWidth + 4, layout.HeaderHeight + 5));
        // Check that hit equals the expected (16L, true); a mismatch fails this test.
        Equal((16L, true), hit);
        // ASCII uses one character per byte, so moving two character widths must identify the same byte.
        hit = layout.HitData(new Point(layout.AsciiX + layout.Gutter + 2 * layout.CharacterWidth + 4, layout.HeaderHeight + 5));
        // Check that hit equals the expected (16L, true); a mismatch fails this test.
        Equal((16L, true), hit);
        // One unit above the body belongs to the header, not the data; -1 is the "no data hit" marker.
        Equal((-1L, false), layout.HitData(new Point(layout.DataX + layout.Gutter, layout.HeaderHeight - 1)));

        // Set document.Settings.ViewMode to DataViewMode.Bits.
        document.Settings.ViewMode = DataViewMode.Bits;
        // Call layout.Update: Recalculates geometry from the document and measured font width.
        layout.Update(document, 8);
        // Switch to individual bits and scroll right by 48 units. A pointer position is relative to
        // the viewport, so subtract the scroll offset from the desired content coordinate.
        layout.SetHorizontalOffset(48);
        // Set hit to the result returned by layout.HitData(...).
        hit = layout.HitData(new Point(layout.DataX + layout.Gutter + layout.CellWidth + 3.5 * layout.CharacterWidth - layout.HorizontalOffset,
            layout.HeaderHeight + 5));
        // One complete byte cell plus 3.5 glyphs targets the middle of byte 1's fourth displayed bit:
        // physical address 1*8 + 3 = 11. false means this is an individual bit, not a whole-byte hit.
        Equal((11L, false), hit);
        // Remember a new Point object using the inputs in parentheses as absent.
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
        // Remember a new ByteGridLayout object as layout.
        var layout = new ByteGridLayout();
        // Set document.Settings.DataWidth to 128.
        document.Settings.DataWidth = 128;
        // Set document.Settings.AutoBytesPerRow to true (yes).
        document.Settings.AutoBytesPerRow = true;
        // Act/check: each edge gutter is 2*8 = 16 units. Hex cells use (2 digits + 1 space)*8 = 24.
        // Accounting for the absent final space gives floor((128 - 32 + 8)/24) = 4 fitting bytes.
        // Update returns true because automatic fitting changed the document's bytes-per-row setting.
        Equal(true, layout.Update(document, 8));
        // Check that document.Settings.BytesPerRow equals the expected 4; a mismatch fails this test.
        Equal(4, document.Settings.BytesPerRow);
        // Eight binary digits plus a space use 9*8 = 72 units per byte, so that same width fits only
        // floor((128 - 32 + 8)/72) = 1 byte in bit mode.
        document.Settings.ViewMode = DataViewMode.Bits;
        // Check that layout.Update(document, 8) equals the expected true; a mismatch fails this test.
        Equal(true, layout.Update(document, 8));
        // Check that document.Settings.BytesPerRow equals the expected 1; a mismatch fails this test.
        Equal(1, document.Settings.BytesPerRow);
        // Turn off auto fit and request 16 bytes explicitly. Layout must retain that grouping and
        // let the data column expand/scroll; returning false means no automatic row-count change occurred.
        document.Settings.AutoBytesPerRow = false;
        // Set document.Settings.BytesPerRow to 16.
        document.Settings.BytesPerRow = 16;
        // Check that layout.Update(document, 8) equals the expected false; a mismatch fails this test.
        Equal(false, layout.Update(document, 8));
        // Check that document.Settings.BytesPerRow equals the expected 16; a mismatch fails this test.
        Equal(16, document.Settings.BytesPerRow);
        // Call layout.SetViewport: Stores the newly assigned visible size and prevents scrolling past the
        // content edges.
        layout.SetViewport(new Size(320, 200));
        // Scroll to row 20 at 16 bytes per row: its first source byte is 20*16 = 320.
        // Reflow to eight bytes per row should put byte 320 at the top again (now row 40).
        layout.SetVerticalOffset(layout.RowHeight * 20);
        // Set document.Settings.BytesPerRow to 8.
        document.Settings.BytesPerRow = 8;
        // Call layout.Update: Recalculates geometry from the document and measured font width.
        layout.Update(document, 8);
        // Check that (long)Math.Floor(layout.VerticalOffset / layout.RowHeight) *
        // document.Settings.BytesPerRow equals the expected 320L; a mismatch fails this test.
        Equal(320L, (long)Math.Floor(layout.VerticalOffset / layout.RowHeight) * document.Settings.BytesPerRow);
        // Showing annotations increases every row from 29 to 47 units, even though this flag touches
        // only byte 0. The taller rows must still preserve top source byte 320 rather than raw pixels.
        document.AddField(new NamedField { Name = "Flag", OrderedBits = [0] });
        // Call layout.Update: Recalculates geometry from the document and measured font width.
        layout.Update(document, 8);
        // Check that layout.RowHeight equals the expected 47d; a mismatch fails this test.
        Equal(47d, layout.RowHeight);
        // Check that (long)Math.Floor(layout.VerticalOffset / layout.RowHeight) *
        // document.Settings.BytesPerRow equals the expected 320L; a mismatch fails this test.
        Equal(320L, (long)Math.Floor(layout.VerticalOffset / layout.RowHeight) * document.Settings.BytesPerRow);
    }
}
