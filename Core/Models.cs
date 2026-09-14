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

// Place this file's definitions in the BitExplorer.Core naming group, which prevents clashes with names in
// other groups.
namespace BitExplorer.Core;

/// <summary>Chooses whether each stored byte is drawn as one number or as its eight individual bits.</summary>
public enum DataViewMode { Bytes, Bits }
/// <summary>Chooses base 16 (hexadecimal) or base 10 (decimal) for a numeric display.</summary>
public enum NumericBase { Hexadecimal, Decimal }
/// <summary>
/// Chooses which end of a byte receives the ruler label zero. LsbZero labels the
/// rightmost, least significant bit zero; MsbZero labels the leftmost bit zero.
/// Neither choice changes the stored data or the left-to-right order of its bits.
/// </summary>
public enum BitNumbering { LsbZero, MsbZero }

/// <summary>Describes how the same binary data should appear in the grid and in a text export.</summary>
/// <remarks>
/// This class contains values, not WPF controls, so the core library and its tests
/// can use it without opening a window. A project saves these settings with its data.
/// </remarks>
public sealed class DisplaySettings
{
    /// <summary>The representation of the central data column.</summary>
    public DataViewMode ViewMode { get; set; } = DataViewMode.Bytes;
    /// <summary>The notation for byte values; offsets have a separate choice below.</summary>
    public NumericBase ByteBase { get; set; } = NumericBase.Hexadecimal;
    /// <summary>The notation for the byte addresses at the beginning of each row.</summary>
    public NumericBase OffsetBase { get; set; } = NumericBase.Hexadecimal;
    /// <summary>The visible bit-position labels, independent of physical bit addresses.</summary>
    public BitNumbering BitNumbering { get; set; } = BitNumbering.LsbZero;
    /// <summary>The actual number of source bytes grouped into each row, in either display mode.</summary>
    public int BytesPerRow { get; set; } = 16;
    /// <summary>Whether the grid may recalculate BytesPerRow to fit the requested data-column width.</summary>
    public bool AutoBytesPerRow { get; set; }
    /// <summary>Whether rows include their starting byte address.</summary>
    public bool ShowOffsets { get; set; } = true;
    /// <summary>Whether rows include a printable-character representation of the same bytes.</summary>
    public bool ShowAscii { get; set; } = true;
    /// <summary>Whether the header includes byte positions or repeated bit-position labels.</summary>
    public bool ShowRuler { get; set; } = true;
    /// <summary>Whether field highlighting and the second line of field names are displayed.</summary>
    public bool ShowAnnotations { get; set; } = true;
    /// <summary>Requested offset-column width in WPF's device-independent pixels.</summary>
    public double OffsetWidth { get; set; } = 110;
    /// <summary>Requested data-column width; fixed row sizes can require a larger effective width.</summary>
    public double DataWidth { get; set; } = 600;
    /// <summary>Requested ASCII-column width; the formatter still reserves room for all row characters.</summary>
    public double AsciiWidth { get; set; } = 180;
    /// <summary>Measured monospace glyph advance used to normalize pixel column widths for text export.</summary>
    public double CharacterWidth { get; set; } = 8;

    /// <summary>Makes an independent settings object for tentative edits or a stable export configuration.</summary>
    // Every setting is a value type, so a shallow copy is sufficient here. Adding a
    // mutable list or another reference-type setting would require revisiting this.
    public DisplaySettings Clone() => (DisplaySettings)MemberwiseClone();

    /// <summary>Rejects unknown enum values and dimensions that would make layout calculations invalid.</summary>
    internal void Validate()
    {
        // If it is not the case that Enum.IsDefined(ViewMode) is true, or it is not the case that
        // Enum.IsDefined(ByteBase) is true, or it is not the case that Enum.IsDefined(OffsetBase) is true,
        // or it is not the case that Enum.IsDefined(BitNumbering) is true, stop this normal path by
        // reporting InvalidDataException; the arguments carry the error details.
        if (!Enum.IsDefined(ViewMode) || !Enum.IsDefined(ByteBase) ||
            !Enum.IsDefined(OffsetBase) || !Enum.IsDefined(BitNumbering))
            // Stop this normal path by reporting InvalidDataException; the arguments carry the error
            // details.
            throw new InvalidDataException("The display settings contain an unknown notation or numbering mode.");
        // If BytesPerRow matches < 1 or > 256, stop this normal path by reporting InvalidDataException; the
        // arguments carry the error details.
        if (BytesPerRow is < 1 or > 256)
            // Stop this normal path by reporting InvalidDataException; the arguments carry the error
            // details.
            throw new InvalidDataException("Bytes per row must be between 1 and 256.");
        // NaN and infinity are valid double values but cannot describe a usable
        // column. Bounds also prevent unreasonable text-padding allocations.
        foreach (double width in new[] { OffsetWidth, DataWidth, AsciiWidth })
            // If it is not the case that double.IsFinite(width) is true, or width is less than 24, or width
            // is greater than 16384, stop this normal path by reporting InvalidDataException; the arguments
            // carry the error details.
            if (!double.IsFinite(width) || width < 24 || width > 16384)
                // Stop this normal path by reporting InvalidDataException; the arguments carry the error
                // details.
                throw new InvalidDataException("Column widths must be finite and between 24 and 16384 pixels.");
        // If it is not the case that double.IsFinite(CharacterWidth) is true, or CharacterWidth is less
        // than 1, or CharacterWidth is greater than 64, stop this normal path by reporting
        // InvalidDataException; the arguments carry the error details.
        if (!double.IsFinite(CharacterWidth) || CharacterWidth < 1 || CharacterWidth > 64)
            // Stop this normal path by reporting InvalidDataException; the arguments carry the error
            // details.
            throw new InvalidDataException("The character width must be finite and between 1 and 64 pixels.");
    }
}

/// <summary>OrderedBits lists physical file bit addresses from the interpreted value's MSB to LSB.</summary>
/// <remarks>
/// Physical address zero is byte zero's high bit, independent of the visible bit-numbering convention.
/// A field may cross bytes or combine separated bits. Its list order is meaningful:
/// the first listed source bit becomes the highest-value bit of the interpreted number.
/// </remarks>
public sealed class NamedField
{
    /// <summary>A stable identity used to find this field even after it is renamed or cloned.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>The human-readable label shown beside the data.</summary>
    public string Name { get; set; } = "Field";
    /// <summary>An RGB color written as six hexadecimal digits after a hash character.</summary>
    public string Color { get; set; } = "#54BFA6";
    /// <summary>Optional explanation of what this field means in the user's file format.</summary>
    public string Notes { get; set; } = "";
    /// <summary>Distinct physical source addresses in the field value's most-to-least significant order.</summary>
    /// <example>For bytes B3 6C, addresses 5, 6, 7, 8, 9, 10 produce binary 011011, or decimal 27.</example>
    public List<long> OrderedBits { get; set; } = [];

    /// <summary>Copies both metadata and the bit list so editing a draft cannot alter the live field.</summary>
    // Copying only the NamedField object would leave both objects pointing at the
    // same List. ToList creates the separate list needed by dialogs and undo history.
    public NamedField Clone() => new()
    {
        // Set this new object's Id entry to Id.
        Id = Id, Name = Name, Color = Color, Notes = Notes,
        // Set this new object's OrderedBits entry to a separate list containing OrderedBits's items.
        OrderedBits = OrderedBits.ToList()
    };
}

/// <summary>The four textual pieces of one source row, before column gutters and line breaks are added.</summary>
/// <remarks>A record provides named read-only properties and value-based equality without hand-written boilerplate.</remarks>
public sealed record RowText(string Offset, string Data, string Ascii, string Annotations);

/// <summary>Effective column widths measured in monospaced character cells, including their gutters.</summary>
/// <remarks>A hidden optional column has width zero. A character cell is the horizontal space occupied by one grid digit.</remarks>
public sealed record ColumnCharacterWidths(int Offset, int Data, int Ascii);
