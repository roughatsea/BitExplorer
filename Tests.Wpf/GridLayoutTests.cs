using System.Windows;
using BitExplorer.Core;
using WpfApp1.Controls;

internal static partial class Program
{
    private static void GridHitTesting()
    {
        var document = new DocumentModel([0x41, 0x42, 0x43, 0x44, 0x45]);
        document.Settings.BytesPerRow = 3;
        var layout = new ByteGridLayout();
        layout.Update(document, 8);
        layout.SetViewport(new Size(320, 120));
        var columns = DisplayFormatter.GetColumnCharacterWidths(document, document.Settings);
        Equal(columns.Offset * 8d, layout.DataX);
        Equal((columns.Offset + columns.Data) * 8d, layout.AsciiX);
        var hit = layout.HitData(new Point(layout.DataX + layout.Gutter + 2 * layout.CellWidth + 4, layout.HeaderHeight + 5));
        Equal((16L, true), hit);
        hit = layout.HitData(new Point(layout.AsciiX + layout.Gutter + 2 * layout.CharacterWidth + 4, layout.HeaderHeight + 5));
        Equal((16L, true), hit);
        Equal((-1L, false), layout.HitData(new Point(layout.DataX + layout.Gutter, layout.HeaderHeight - 1)));

        document.Settings.ViewMode = DataViewMode.Bits;
        layout.Update(document, 8);
        layout.SetHorizontalOffset(48);
        hit = layout.HitData(new Point(layout.DataX + layout.Gutter + layout.CellWidth + 3.5 * layout.CharacterWidth - layout.HorizontalOffset,
            layout.HeaderHeight + 5));
        Equal((11L, false), hit);
        var absent = new Point(layout.DataX + layout.Gutter + 2 * layout.CellWidth - layout.HorizontalOffset,
            layout.HeaderHeight + layout.RowHeight + 5);
        Equal((-1L, false), layout.HitData(absent));
        Equal(0, layout.HitDivider(new Point(layout.DataX - layout.HorizontalOffset, 10)));
    }

    private static void GridFittingAndReflow()
    {
        var document = new DocumentModel(new byte[4096]);
        var layout = new ByteGridLayout();
        document.Settings.DataWidth = 128;
        document.Settings.AutoBytesPerRow = true;
        Equal(true, layout.Update(document, 8));
        Equal(4, document.Settings.BytesPerRow);
        document.Settings.ViewMode = DataViewMode.Bits;
        Equal(true, layout.Update(document, 8));
        Equal(1, document.Settings.BytesPerRow);
        document.Settings.AutoBytesPerRow = false;
        document.Settings.BytesPerRow = 16;
        Equal(false, layout.Update(document, 8));
        Equal(16, document.Settings.BytesPerRow);
        layout.SetViewport(new Size(320, 200));
        layout.SetVerticalOffset(layout.RowHeight * 20);
        document.Settings.BytesPerRow = 8;
        layout.Update(document, 8);
        Equal(320L, (long)Math.Floor(layout.VerticalOffset / layout.RowHeight) * document.Settings.BytesPerRow);
        document.AddField(new NamedField { Name = "Flag", OrderedBits = [0] });
        layout.Update(document, 8);
        Equal(47d, layout.RowHeight);
        Equal(320L, (long)Math.Floor(layout.VerticalOffset / layout.RowHeight) * document.Settings.BytesPerRow);
    }
}
