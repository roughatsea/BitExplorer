using System.Globalization;

namespace BitExplorer.Core;

/// <summary>Shared, culture-independent textual representation for the screen and whole-file exports.</summary>
public static class DisplayFormatter
{
    public static string FormatByte(byte value, NumericBase numberBase) => numberBase switch
    {
        NumericBase.Hexadecimal => value.ToString("X2", CultureInfo.InvariantCulture),
        NumericBase.Decimal => value.ToString("D3", CultureInfo.InvariantCulture),
        _ => throw new ArgumentOutOfRangeException(nameof(numberBase))
    };

    public static string FormatOffset(long offset, NumericBase numberBase)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        return numberBase switch
        {
            NumericBase.Hexadecimal => offset.ToString("X8", CultureInfo.InvariantCulture),
            NumericBase.Decimal => offset.ToString("D8", CultureInfo.InvariantCulture),
            _ => throw new ArgumentOutOfRangeException(nameof(numberBase))
        };
    }

    // Numbering labels never rearrange the stored bits or reverse their significance.
    public static string FormatBits(byte value, BitNumbering numbering)
    {
        if (!Enum.IsDefined(numbering)) throw new ArgumentOutOfRangeException(nameof(numbering));
        return Convert.ToString(value, 2).PadLeft(8, '0');
    }

    public static int GetDataCharacterWidth(DisplaySettings settings)
    {
        settings.Validate();
        return settings.BytesPerRow * (settings.ViewMode == DataViewMode.Bits ? 9 : settings.ByteBase == NumericBase.Hexadecimal ? 3 : 4) - 1;
    }

    /// <summary>Column widths in monospace cells, including a two-character gutter at both edges.</summary>
    public static ColumnCharacterWidths GetColumnCharacterWidths(DocumentModel document, DisplaySettings settings)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        int offsets = settings.ShowOffsets
            ? Math.Max((int)Math.Ceiling(settings.OffsetWidth / settings.CharacterWidth),
                FormatOffset(Math.Max(0, document.Data.LongLength - 1), settings.OffsetBase).Length + 4)
            : 0;
        int data = Math.Max((int)Math.Ceiling(settings.DataWidth / settings.CharacterWidth), GetDataCharacterWidth(settings) + 4);
        int ascii = settings.ShowAscii
            ? Math.Max((int)Math.Ceiling(settings.AsciiWidth / settings.CharacterWidth), settings.BytesPerRow + 4)
            : 0;
        return new ColumnCharacterWidths(offsets, data, ascii);
    }

    public static RowText FormatRow(DocumentModel document, int offset, DisplaySettings settings)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        if (offset < 0 || offset > document.Data.Length) throw new ArgumentOutOfRangeException(nameof(offset));
        int count = Math.Min(settings.BytesPerRow, document.Data.Length - offset);
        var tokens = new string[count];
        var ascii = new char[count];
        for (int i = 0; i < count; i++)
        {
            byte value = document.Data[offset + i];
            tokens[i] = settings.ViewMode == DataViewMode.Bytes
                ? FormatByte(value, settings.ByteBase) : FormatBits(value, settings.BitNumbering);
            ascii[i] = value is >= 32 and <= 126 ? (char)value : '.';
        }
        string data = string.Join(' ', tokens);
        if (settings.ShowAscii) data = data.PadRight(GetDataCharacterWidth(settings));
        long startBit = (long)offset * 8;
        long endBit = (long)(offset + count) * 8;
        string annotations = settings.ShowAnnotations
            ? string.Join(" ", document.Fields.Where(f => f.OrderedBits.Any(b => b >= startBit && b < endBit))
                .Select(f => $"[{SingleLine(f.Name)}]"))
            : "";
        int annotationWidth = GetColumnCharacterWidths(document, settings).Data - 4;
        if (annotations.Length > annotationWidth)
        {
            int prefixLength = annotationWidth - 1;
            if (prefixLength > 0 && char.IsHighSurrogate(annotations[prefixLength - 1])) prefixLength--;
            annotations = annotations[..prefixLength] + "…";
        }
        return new RowText(settings.ShowOffsets ? FormatOffset(offset, settings.OffsetBase) : "",
            data, settings.ShowAscii ? new string(ascii) : "", annotations);
    }

    public static string GetRuler(DisplaySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        return string.Join(' ', Enumerable.Range(0, settings.BytesPerRow).Select(i =>
            settings.ViewMode == DataViewMode.Bits
                ? settings.BitNumbering == BitNumbering.LsbZero ? "76543210" : "01234567"
                : FormatByte((byte)i, settings.ByteBase)));
    }

    public static void Export(TextWriter writer, DocumentModel document, DisplaySettings settings)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        ColumnCharacterWidths widths = GetColumnCharacterWidths(document, settings);
        string dataIndent = new(' ', widths.Offset + 2);
        bool annotationRows = settings.ShowAnnotations && document.Fields.Count > 0;
        if (settings.ShowRuler)
            writer.WriteLine(dataIndent + GetRuler(settings));
        for (int offset = 0; offset < document.Data.Length; offset += settings.BytesPerRow)
        {
            RowText row = FormatRow(document, offset, settings);
            writer.Write("  ");
            if (settings.ShowOffsets) writer.Write(row.Offset.PadRight(widths.Offset));
            writer.Write(settings.ShowAscii ? row.Data.PadRight(widths.Data) : row.Data.TrimEnd());
            if (settings.ShowAscii) writer.Write(row.Ascii);
            writer.WriteLine();
            if (annotationRows)
                writer.WriteLine(row.Annotations.Length > 0 ? dataIndent + row.Annotations : "");
        }
    }

    private static string SingleLine(string value) => new(value.Select(c => char.IsControl(c) ? ' ' : c).ToArray());
}
