using System.Globalization;

namespace BitExplorer.Core;

/// <summary>Shared, culture-independent textual representation for the screen and whole-file exports.</summary>
/// <remarks>
/// Keeping formatting in the core avoids two subtly different implementations in
/// the screen and exporter. These methods format data; they never edit the bytes.
/// "Culture-independent" means a machine's regional settings do not alter digits
/// or spacing in a file that should be reproducible on another machine.
/// </remarks>
public static class DisplayFormatter
{
    /// <summary>Formats a byte as two uppercase hex digits or three zero-padded decimal digits.</summary>
    /// <example>The same stored value becomes A1 in hexadecimal and 161 in decimal.</example>
    // X2 and D3 are .NET numeric format strings. Equal-width tokens make byte
    // columns and the ruler line up even when adjacent values have different sizes.
    public static string FormatByte(byte value, NumericBase numberBase) => numberBase switch
    {
        NumericBase.Hexadecimal => value.ToString("X2", CultureInfo.InvariantCulture),
        NumericBase.Decimal => value.ToString("D3", CultureInfo.InvariantCulture),
        _ => throw new ArgumentOutOfRangeException(nameof(numberBase))
    };

    /// <summary>Formats a nonnegative byte address with at least eight digits; larger addresses are never truncated.</summary>
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

    /// <summary>Returns the byte's eight physical bits from most significant to least significant.</summary>
    /// <remarks>The numbering argument is validated, but affects ruler labels rather than this bit string.</remarks>
    // Convert.ToString(value, 2) chooses base two; PadLeft retains leading zeros.
    // Numbering labels never rearrange stored bits or reverse their significance.
    public static string FormatBits(byte value, BitNumbering numbering)
    {
        if (!Enum.IsDefined(numbering)) throw new ArgumentOutOfRangeException(nameof(numbering));
        return Convert.ToString(value, 2).PadLeft(8, '0');
    }

    /// <summary>Counts characters required by one complete row's data tokens, excluding column gutters.</summary>
    // Each byte uses eight binary, two hexadecimal, or three decimal characters,
    // followed by one separating space. Subtract one for the absent final separator.
    // Example: three hex bytes need 3 * (2 + 1) - 1 = 8 characters: "FF F0 A1".
    public static int GetDataCharacterWidth(DisplaySettings settings)
    {
        settings.Validate();
        return settings.BytesPerRow * (settings.ViewMode == DataViewMode.Bits ? 9 : settings.ByteBase == NumericBase.Hexadecimal ? 3 : 4) - 1;
    }

    /// <summary>Column widths in monospace cells, including a two-character gutter at both edges.</summary>
    /// <remarks>
    /// Requested pixel widths are rounded up to complete character cells. Minimum
    /// widths ensure fixed row grouping cannot silently hide bytes or ASCII text.
    /// The returned measurements do not replace the user's requested widths.
    /// </remarks>
    public static ColumnCharacterWidths GetColumnCharacterWidths(DocumentModel document, DisplaySettings settings)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        // Reserve enough room for even the file's largest address. Four extra
        // cells account for two spaces on each edge; hidden columns occupy zero.
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

    /// <summary>Formats the source bytes beginning at an offset into data, offset, ASCII, and annotation pieces.</summary>
    /// <param name="document">The bytes and named-field layout to read.</param>
    /// <param name="offset">A zero-based byte address, normally a multiple of BytesPerRow.</param>
    /// <param name="settings">The display choices to apply; the final row may contain fewer bytes.</param>
    /// <returns>Text components without the final column gutters or row line breaks.</returns>
    public static RowText FormatRow(DocumentModel document, int offset, DisplaySettings settings)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        if (offset < 0 || offset > document.Data.Length) throw new ArgumentOutOfRangeException(nameof(offset));
        // The last row is often incomplete. Read only real bytes; padding below
        // is whitespace for alignment, never invented zero-valued source data.
        int count = Math.Min(settings.BytesPerRow, document.Data.Length - offset);
        var tokens = new string[count];
        var ascii = new char[count];
        for (int i = 0; i < count; i++)
        {
            byte value = document.Data[offset + i];
            tokens[i] = settings.ViewMode == DataViewMode.Bytes
                ? FormatByte(value, settings.ByteBase) : FormatBits(value, settings.BitNumbering);
            // ASCII 32 is a real space and 126 is '~'. Control bytes and bytes
            // outside printable ASCII use '.', so they do not disturb text layout.
            ascii[i] = value is >= 32 and <= 126 ? (char)value : '.';
        }
        string data = string.Join(' ', tokens);
        // Pad an incomplete data row when ASCII follows it, so the ASCII column
        // starts at the same position as it does on every full row.
        if (settings.ShowAscii) data = data.PadRight(GetDataCharacterWidth(settings));
        long startBit = (long)offset * 8;
        long endBit = (long)(offset + count) * 8;
        // The row covers [startBit, endBit): include the start, exclude the end.
        // A field label appears whenever any of its bits intersects that interval,
        // including fields spanning rows or assembled from separated source ranges.
        string annotations = settings.ShowAnnotations
            ? string.Join(" ", document.Fields.Where(f => f.OrderedBits.Any(b => b >= startBit && b < endBit))
                .Select(f => $"[{SingleLine(f.Name)}]"))
            : "";
        int annotationWidth = GetColumnCharacterWidths(document, settings).Data - 4;
        if (annotations.Length > annotationWidth)
        {
            // Reserve one character for the ellipsis. A Unicode character such
            // as an emoji can occupy two UTF-16 chars; do not retain only its first
            // surrogate, which would create a broken character at the cut point.
            int prefixLength = annotationWidth - 1;
            if (prefixLength > 0 && char.IsHighSurrogate(annotations[prefixLength - 1])) prefixLength--;
            annotations = annotations[..prefixLength] + "…";
        }
        return new RowText(settings.ShowOffsets ? FormatOffset(offset, settings.OffsetBase) : "",
            data, settings.ShowAscii ? new string(ascii) : "", annotations);
    }

    /// <summary>Builds byte-within-row positions or repeated within-byte bit labels aligned with the data tokens.</summary>
    /// <remarks>Changing LsbZero to MsbZero changes 76543210 to 01234567, while the binary data stays unchanged.</remarks>
    public static string GetRuler(DisplaySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        return string.Join(' ', Enumerable.Range(0, settings.BytesPerRow).Select(i =>
            settings.ViewMode == DataViewMode.Bits
                ? settings.BitNumbering == BitNumbering.LsbZero ? "76543210" : "01234567"
                : FormatByte((byte)i, settings.ByteBase)));
    }

    /// <summary>Writes the entire document using the current row grouping, notation, columns, ruler, and annotations.</summary>
    /// <param name="writer">Destination text writer; the caller owns its encoding, newline convention, and disposal.</param>
    /// <param name="document">The source data and fields, which should remain stable for the duration of export.</param>
    /// <param name="settings">A stable display configuration, normally cloned before an asynchronous export.</param>
    /// <remarks>This streams one row at a time instead of constructing one giant string for the whole file.</remarks>
    public static void Export(TextWriter writer, DocumentModel document, DisplaySettings settings)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        ColumnCharacterWidths widths = GetColumnCharacterWidths(document, settings);
        // Text begins two cells inside every column. With offsets visible, the
        // ruler and field names start after the complete offset column plus that
        // two-cell data gutter. With offsets hidden, only the gutter remains.
        string dataIndent = new(' ', widths.Offset + 2);
        bool annotationRows = settings.ShowAnnotations && document.Fields.Count > 0;
        if (settings.ShowRuler)
            writer.WriteLine(dataIndent + GetRuler(settings));
        for (int offset = 0; offset < document.Data.Length; offset += settings.BytesPerRow)
        {
            RowText row = FormatRow(document, offset, settings);
            // One common leading gutter plus full-width padded columns puts each
            // later token at the same character position as the rendered grid.
            // Do not trim ASCII: a trailing space may be an actual source byte.
            writer.Write("  ");
            if (settings.ShowOffsets) writer.Write(row.Offset.PadRight(widths.Offset));
            writer.Write(settings.ShowAscii ? row.Data.PadRight(widths.Data) : row.Data.TrimEnd());
            if (settings.ShowAscii) writer.Write(row.Ascii);
            writer.WriteLine();
            // Annotation-enabled rows have two text lines even when no field
            // touches this particular row. The blank line mirrors the grid's
            // reserved annotation space; an empty document invents no data rows.
            if (annotationRows)
                writer.WriteLine(row.Annotations.Length > 0 ? dataIndent + row.Annotations : "");
        }
    }

    /// <summary>Replaces control characters with spaces so a field label cannot inject extra export lines or tabs.</summary>
    // Normal field validation already rejects control characters in names. This
    // final formatting guard also protects callers that directly mutated metadata.
    private static string SingleLine(string value) => new(value.Select(c => char.IsControl(c) ? ' ' : c).ToArray());
}
