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

// Make names from System.Text available here without writing their full prefix each time. This does not run
// that library's code.
using System.Text;
// Make names from System.Windows available here without writing their full prefix each time. This does not
// run that library's code.
using System.Windows;
// Make names from BitExplorer.Core available here without writing their full prefix each time. This does
// not run that library's code.
using BitExplorer.Core;

// Place this file's definitions in the WpfApp1.Controls naming group, which prevents clashes with names in
// other groups.
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
        // If document matches null, return null (no value) to the caller and leave this method.
        if (document is null) return null;
        // Resize handles take priority over the nearby ruler text, matching the click behavior.
        int divider = layout.HitDivider(point);
        // If divider is at least 0, return the values in parentheses grouped into one package (a tuple) to
        // the caller and leave this method.
        if (divider >= 0)
            // Return the values in parentheses grouped into one package (a tuple) to the caller and leave
            // this method.
            return (long.MinValue + divider, "Drag to resize this column.\nWidths align to whole text characters for faithful export.\nReduce bytes per row, or enable Auto fit, to narrow the data column further.");

        // Remember document.Settings as settings.
        var settings = document.Settings;
        // Pointer coordinates describe the viewport; adding the scroll offset locates the same
        // point in the full content. The ruler stays at the top while rows scroll vertically.
        double contentX = point.X + layout.HorizontalOffset;
        // If both both both both settings.ShowRuler is true and point.Y matches >= 30 and point.Y is less
        // than layout.HeaderHeight and contentX is at least layout.DataX + layout.Gutter and contentX is
        // less than layout.AsciiX, run the following grouped instructions.
        if (settings.ShowRuler && point.Y is >= 30 && point.Y < layout.HeaderHeight && contentX >= layout.DataX + layout.Gutter && contentX < layout.AsciiX)
        {
            // Remember ((contentX - layout.DataX - layout.Gutter) divided by layout.CellWidth), converted
            // to int as position.
            int position = (int)((contentX - layout.DataX - layout.Gutter) / layout.CellWidth);
            // If both position is at least 0 and position is less than layout.BytesPerRow, run the
            // following grouped instructions.
            if (position >= 0 && position < layout.BytesPerRow)
            {
                // Remember the displayed text below; each {...} inserts a computed value into the text as
                // text.
                string text = $"Byte position within row  0x{position:X2}  ·  {position} decimal";
                // If it is not the case that layout.BitView is true, return the values in parentheses
                // grouped into one package (a tuple) to the caller and leave this method.
                if (!layout.BitView) return (long.MinValue + 1024 + position, text);
                // Physical position always counts left-to-right, 0..7. The chosen numbering convention
                // affects its label only: the leftmost bit is labeled 7 with LSB-zero or 0 with MSB-zero.
                int physical = Math.Clamp((int)((contentX - layout.DataX - layout.Gutter - position * layout.CellWidth) / layout.CharacterWidth), 0, 7);
                // Remember 7 minus physical when settings.BitNumbering equals BitNumbering.LsbZero;
                // otherwise physical as number.
                int number = settings.BitNumbering == BitNumbering.LsbZero ? 7 - physical : physical;
                // Add the displayed text below; each {...} inserts a computed value into the text to text
                // and keep the result there (+=).
                text += $"\nBit position {number}  ·  weight {1 << (7 - physical)}";
                // Add the text " · MS bit" when physical equals 0; otherwise the text " · LS bit" when
                // physical equals 7; otherwise empty text to text and keep the result there (+=).
                text += physical == 0 ? "  ·  MS bit" : physical == 7 ? "  ·  LS bit" : "";
                // Add the text written below to text and keep the result there (+=).
                text += "\nNumbering changes labels; the stored bits keep their order.";
                // Return the values in parentheses grouped into one package (a tuple) to the caller and
                // leave this method.
                return (long.MinValue + 1024 + position * 8 + physical, text);
            }
        }
        // Hovering a row offset always shows both number bases, even if that column displays only one.
        if (point.Y >= layout.HeaderHeight && contentX < layout.OffsetWidth && contentX >= 0)
        {
            // Remember ((point.Y - layout.HeaderHeight + layout.VerticalOffset) divided by
            // layout.RowHeight), converted to int as row.
            int row = (int)((point.Y - layout.HeaderHeight + layout.VerticalOffset) / layout.RowHeight);
            // If row is less than layout.RowCount, run the following grouped instructions.
            if (row < layout.RowCount)
            {
                // Remember row multiplied by layout.BytesPerRow as offset.
                int offset = row * layout.BytesPerRow;
                // Return the values in parentheses grouped into one package (a tuple) to the caller and
                // leave this method.
                return (long.MinValue + 8192 + row, $"Row offset  0x{offset:X8}  ·  {offset} decimal");
            }
        }

        // Reuse exactly the same data hit calculation as selection. Byte/ASCII hits describe a whole
        // byte; individual binary characters also receive their bit label, value, and numeric weight.
        var hit = layout.HitData(point);
        // If hit.Bit is less than 0, return null (no value) to the caller and leave this method.
        if (hit.Bit < 0) return null;
        // Remember (hit.Bit divided by 8), converted to int as byteOffset.
        int byteOffset = (int)(hit.Bit / 8);
        // Remember (the remainder after dividing hit.Bit by 8), converted to int as physicalBit.
        int physicalBit = (int)(hit.Bit % 8);
        // Remember document.Data[byteOffset] as value.
        byte value = document.Data[byteOffset];
        // Remember a new StringBuilder object as description.
        var description = new StringBuilder();
        // Call description.AppendLine(...); the values in parentheses are the inputs.
        description.AppendLine($"Byte offset  0x{byteOffset:X8}  ·  {byteOffset} decimal");
        // Call description.AppendLine(...); the values in parentheses are the inputs.
        description.AppendLine($"Hex  {value:X2}     Decimal  {value}     Binary  {Convert.ToString(value, 2).PadLeft(8, '0')}");
        // If it is not the case that hit.WholeByte is true, run the following grouped instructions.
        if (!hit.WholeByte)
        {
            // Remember 7 minus physicalBit when settings.BitNumbering equals BitNumbering.LsbZero;
            // otherwise physicalBit as index.
            int index = settings.BitNumbering == BitNumbering.LsbZero ? 7 - physicalBit : physicalBit;
            // Call description.AppendLine(...); the values in parentheses are the inputs.
            description.AppendLine($"Bit {index}  ·  value {(value >> (7 - physicalBit)) & 1}  ·  weight {1 << (7 - physicalBit)}" + (physicalBit == 0 ? "  ·  MS bit" : physicalBit == 7 ? "  ·  LS bit" : ""));
        }
        // A byte-level hover lists fields touching any of its eight bits. A bit-level hover lists only
        // fields containing that exact physical address, which is useful for overlapping packed fields.
        foreach (var field in document.Fields.Where(field => hit.WholeByte ? field.OrderedBits.Any(bit => bit / 8 == byteOffset) : field.OrderedBits.Contains(hit.Bit)))
            // Call description.AppendLine(...); the values in parentheses are the inputs.
            description.AppendLine($"Field: {field.Name}");
        // Call description.AppendLine(...); the values in parentheses are the inputs.
        description.AppendLine(hit.WholeByte ? "Click to inspect · Double click to edit · Shift to extend" : "Click to select · Ctrl to add bits · Shift to extend");
        // Call description.Append(...); the values in parentheses are the inputs.
        description.Append("Select up to 65,536 bits (8 KiB) at once. Text export includes the entire file.");
        // Return the values in parentheses grouped into one package (a tuple) to the caller and leave this
        // method.
        return (hit.Bit, description.ToString());
    }
}
