using System.Globalization;
using System.Numerics;
using BitExplorer.Core;

namespace WpfApp1.ViewModels;

/// <summary>Formats a value's significance without conflating its assembly order with source selection.</summary>
internal sealed record SelectionInterpretation(string Decimal, string Signed, string Hex, string Significance, string Ends)
{
    public static SelectionInterpretation From(ExplorerSession session)
    {
        var bits = session.GetInterpretationBits();
        if (bits.Count == 0) return new("—", "—", "—", "Select bits to interpret a value.", "");
        if (bits.Count > DocumentModel.MaxFieldBits)
            return new("Selection too large", "Selection too large", "Selection too large",
                $"Select up to {DocumentModel.MaxFieldBits:N0} bits for a numeric interpretation.", "");
        var value = session.Document.ReadField(new NamedField { Name = "Selection", OrderedBits = bits.ToList() });
        var signed = ((value >> (bits.Count - 1)) & BigInteger.One) == 1 ? value - (BigInteger.One << bits.Count) : value;
        bool wholeBytes = bits.Count % 8 == 0 && bits.GroupBy(bit => bit / 8).All(group => group.Count() == 8) &&
            bits.Chunk(8).All(group => group.Select(bit => bit / 8).Distinct().Count() == 1 && group.SequenceEqual(group.Order()));
        string significance = wholeBytes
            ? $"MS byte: offset {bits[0] / 8} · LS byte: offset {bits[^1] / 8}\nWithin each byte: MS bit → LS bit"
            : "Packed field · significance follows the assembly order; source bytes may contribute partial bits.";
        string Address(long bit) => $"byte {bit / 8}, bit {(session.Document.Settings.BitNumbering == BitNumbering.LsbZero ? 7 - bit % 8 : bit % 8)}";
        return new(value.ToString(CultureInfo.InvariantCulture), signed.ToString(CultureInfo.InvariantCulture),
            NumericText.ToHex(value), significance, $"Field MSB → {Address(bits[0])}\nField LSB → {Address(bits[^1])}");
    }
}
