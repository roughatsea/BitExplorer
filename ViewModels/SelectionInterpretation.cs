using System.Globalization;
using System.Numerics;
using BitExplorer.Core;

namespace WpfApp1.ViewModels;

/// <summary>
/// An immutable result containing the inspector's unsigned decimal, signed decimal,
/// hexadecimal, significance description and endpoint addresses. A record groups
/// these related strings so they are computed together from one selection snapshot.
/// Value assembly follows session order; it never changes the selected source addresses.
/// </summary>
internal sealed record SelectionInterpretation(string Decimal, string Signed, string Hex, string Significance, string Ends)
{
    /// <summary>Builds all interpretation readouts, including clear messages for empty or oversized selections.</summary>
    public static SelectionInterpretation From(ExplorerSession session)
    {
        var bits = session.GetInterpretationBits();
        if (bits.Count == 0) return new("—", "—", "—", "Select bits to interpret a value.", "");
        if (bits.Count > DocumentModel.MaxFieldBits)
            return new("Selection too large", "Selection too large", "Selection too large",
                $"Select up to {DocumentModel.MaxFieldBits:N0} bits for a numeric interpretation.", "");
        // A temporary field lets arbitrary selected bits reuse the model's tested
        // value assembly. It is not added to the document's named fields.
        var value = session.Document.ReadField(new NamedField { Name = "Selection", OrderedBits = bits.ToList() });
        // Two's-complement interpretation: if the high bit is set, subtract 2^width.
        // For example, eight selected bits with unsigned value 255 represent signed -1.
        var signed = ((value >> (bits.Count - 1)) & BigInteger.One) == 1 ? value - (BigInteger.One << bits.Count) : value;
        // MS/LS byte labels only make sense when each eight-bit chunk is a complete
        // source byte in normal within-byte order. Packed or shuffled bits use field labels.
        bool wholeBytes = bits.Count % 8 == 0 && bits.GroupBy(bit => bit / 8).All(group => group.Count() == 8) &&
            bits.Chunk(8).All(group => group.Select(bit => bit / 8).Distinct().Count() == 1 && group.SequenceEqual(group.Order()));
        string significance = wholeBytes
            ? $"MS byte: offset {bits[0] / 8} · LS byte: offset {bits[^1] / 8}\nWithin each byte: MS bit → LS bit"
            : "Packed field · significance follows the assembly order; source bytes may contribute partial bits.";
        // This local helper translates a physical address into the ruler convention.
        // % 8 gives the position within its byte; / 8 gives that byte's offset.
        string Address(long bit) => $"byte {bit / 8}, bit {(session.Document.Settings.BitNumbering == BitNumbering.LsbZero ? 7 - bit % 8 : bit % 8)}";
        return new(value.ToString(CultureInfo.InvariantCulture), signed.ToString(CultureInfo.InvariantCulture),
            NumericText.ToHex(value), significance, $"Field MSB → {Address(bits[0])}\nField LSB → {Address(bits[^1])}");
    }
}
