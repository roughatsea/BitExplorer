using System.Text;
using System.Windows;
using BitExplorer.Core;

namespace WpfApp1.Controls;

/// <summary>
/// Builds hover descriptions for resize handles, rulers, offsets, and actual data. It only returns
/// text; ByteGrid owns showing/hiding the WPF popup. Using the shared layout keeps hover targets
/// consistent with clicking and rendering.
/// </summary>
/// <remarks>
/// MS means most significant, and LS means least significant. Within a byte, the leftmost displayed
/// bit has weight 128 and the rightmost has weight 1, regardless of the chosen bit-number labels.
/// </remarks>
internal static class ByteGridTooltip
{
    /// <summary>
    /// Returns a stable target key and its explanation, or null when the pointer has no useful target.
    /// Data keys are nonnegative physical bit addresses. Special header/divider keys are near the
    /// smallest possible long value, keeping them separate from every real bit in the file.
    /// </summary>
    public static (long Key, string Text)? Describe(DocumentModel? document, ByteGridLayout layout, Point point)
    {
        if (document is null) return null;
        // Resize handles take priority over the nearby ruler text, matching the click behavior.
        int divider = layout.HitDivider(point);
        if (divider >= 0)
            return (long.MinValue + divider, "Drag to resize this column.\nWidths align to whole text characters for faithful export.\nReduce bytes per row, or enable Auto fit, to narrow the data column further.");

        var settings = document.Settings;
        // Pointer coordinates describe the viewport; adding the scroll offset locates the same
        // point in the full content. The ruler stays at the top while rows scroll vertically.
        double contentX = point.X + layout.HorizontalOffset;
        if (settings.ShowRuler && point.Y is >= 30 && point.Y < layout.HeaderHeight && contentX >= layout.DataX + layout.Gutter && contentX < layout.AsciiX)
        {
            int position = (int)((contentX - layout.DataX - layout.Gutter) / layout.CellWidth);
            if (position >= 0 && position < layout.BytesPerRow)
            {
                string text = $"Byte position within row  0x{position:X2}  ·  {position} decimal";
                if (!layout.BitView) return (long.MinValue + 1024 + position, text);
                // Physical position always counts left-to-right, 0..7. The chosen numbering convention
                // affects its label only: the leftmost bit is labeled 7 with LSB-zero or 0 with MSB-zero.
                int physical = Math.Clamp((int)((contentX - layout.DataX - layout.Gutter - position * layout.CellWidth) / layout.CharacterWidth), 0, 7);
                int number = settings.BitNumbering == BitNumbering.LsbZero ? 7 - physical : physical;
                text += $"\nBit position {number}  ·  weight {1 << (7 - physical)}";
                text += physical == 0 ? "  ·  MS bit" : physical == 7 ? "  ·  LS bit" : "";
                text += "\nNumbering changes labels; the stored bits keep their order.";
                return (long.MinValue + 1024 + position * 8 + physical, text);
            }
        }
        // Hovering a row offset always shows both number bases, even if that column displays only one.
        if (point.Y >= layout.HeaderHeight && contentX < layout.OffsetWidth && contentX >= 0)
        {
            int row = (int)((point.Y - layout.HeaderHeight + layout.VerticalOffset) / layout.RowHeight);
            if (row < layout.RowCount)
            {
                int offset = row * layout.BytesPerRow;
                return (long.MinValue + 8192 + row, $"Row offset  0x{offset:X8}  ·  {offset} decimal");
            }
        }

        // Reuse exactly the same data hit calculation as selection. Byte/ASCII hits describe a whole
        // byte; individual binary characters also receive their bit label, value, and numeric weight.
        var hit = layout.HitData(point);
        if (hit.Bit < 0) return null;
        int byteOffset = (int)(hit.Bit / 8);
        int physicalBit = (int)(hit.Bit % 8);
        byte value = document.Data[byteOffset];
        var description = new StringBuilder();
        description.AppendLine($"Byte offset  0x{byteOffset:X8}  ·  {byteOffset} decimal");
        description.AppendLine($"Hex  {value:X2}     Decimal  {value}     Binary  {Convert.ToString(value, 2).PadLeft(8, '0')}");
        if (!hit.WholeByte)
        {
            int index = settings.BitNumbering == BitNumbering.LsbZero ? 7 - physicalBit : physicalBit;
            description.AppendLine($"Bit {index}  ·  value {(value >> (7 - physicalBit)) & 1}  ·  weight {1 << (7 - physicalBit)}" + (physicalBit == 0 ? "  ·  MS bit" : physicalBit == 7 ? "  ·  LS bit" : ""));
        }
        // A byte-level hover lists fields touching any of its eight bits. A bit-level hover lists only
        // fields containing that exact physical address, which is useful for overlapping packed fields.
        foreach (var field in document.Fields.Where(field => hit.WholeByte ? field.OrderedBits.Any(bit => bit / 8 == byteOffset) : field.OrderedBits.Contains(hit.Bit)))
            description.AppendLine($"Field: {field.Name}");
        description.AppendLine(hit.WholeByte ? "Click to inspect · Double click to edit · Shift to extend" : "Click to select · Ctrl to add bits · Shift to extend");
        description.Append("Select up to 65,536 bits (8 KiB) at once. Text export includes the entire file.");
        return (hit.Bit, description.ToString());
    }
}
