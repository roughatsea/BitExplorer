namespace BitExplorer.Core;

public enum DataViewMode { Bytes, Bits }
public enum NumericBase { Hexadecimal, Decimal }
public enum BitNumbering { LsbZero, MsbZero }

public sealed class DisplaySettings
{
    public DataViewMode ViewMode { get; set; } = DataViewMode.Bytes;
    public NumericBase ByteBase { get; set; } = NumericBase.Hexadecimal;
    public NumericBase OffsetBase { get; set; } = NumericBase.Hexadecimal;
    public BitNumbering BitNumbering { get; set; } = BitNumbering.LsbZero;
    public int BytesPerRow { get; set; } = 16;
    public bool AutoBytesPerRow { get; set; }
    public bool ShowOffsets { get; set; } = true;
    public bool ShowAscii { get; set; } = true;
    public bool ShowRuler { get; set; } = true;
    public bool ShowAnnotations { get; set; } = true;
    public double OffsetWidth { get; set; } = 110;
    public double DataWidth { get; set; } = 600;
    public double AsciiWidth { get; set; } = 180;
    /// <summary>Measured monospace glyph advance used to normalize pixel column widths for text export.</summary>
    public double CharacterWidth { get; set; } = 8;

    public DisplaySettings Clone() => (DisplaySettings)MemberwiseClone();

    internal void Validate()
    {
        if (!Enum.IsDefined(ViewMode) || !Enum.IsDefined(ByteBase) ||
            !Enum.IsDefined(OffsetBase) || !Enum.IsDefined(BitNumbering))
            throw new InvalidDataException("The display settings contain an unknown notation or numbering mode.");
        if (BytesPerRow is < 1 or > 256)
            throw new InvalidDataException("Bytes per row must be between 1 and 256.");
        foreach (double width in new[] { OffsetWidth, DataWidth, AsciiWidth })
            if (!double.IsFinite(width) || width < 24 || width > 16384)
                throw new InvalidDataException("Column widths must be finite and between 24 and 16384 pixels.");
        if (!double.IsFinite(CharacterWidth) || CharacterWidth < 1 || CharacterWidth > 64)
            throw new InvalidDataException("The character width must be finite and between 1 and 64 pixels.");
    }
}

/// <summary>OrderedBits lists physical file bit addresses from the interpreted value's MSB to LSB.</summary>
/// <remarks>Physical address zero is byte zero's high bit, independent of the visible bit-numbering convention.</remarks>
public sealed class NamedField
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Field";
    public string Color { get; set; } = "#54BFA6";
    public string Notes { get; set; } = "";
    public List<long> OrderedBits { get; set; } = [];

    public NamedField Clone() => new()
    {
        Id = Id, Name = Name, Color = Color, Notes = Notes,
        OrderedBits = OrderedBits.ToList()
    };
}

public sealed record RowText(string Offset, string Data, string Ascii, string Annotations);

public sealed record ColumnCharacterWidths(int Offset, int Data, int Ascii);
