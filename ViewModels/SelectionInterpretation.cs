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

// Make names from System.Globalization available here without writing their full prefix each time. This
// does not run that library's code.
using System.Globalization;
// Make names from System.Numerics available here without writing their full prefix each time. This does not
// run that library's code.
using System.Numerics;
// Make names from BitExplorer.Core available here without writing their full prefix each time. This does
// not run that library's code.
using BitExplorer.Core;

// Place this file's definitions in the WpfApp1.ViewModels naming group, which prevents clashes with names
// in other groups.
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
        // Remember the result returned by session.GetInterpretationBits() as bits.
        var bits = session.GetInterpretationBits();
        // If bits.Count equals 0, return a new object of the required type using the inputs in parentheses
        // to the caller and leave this method.
        if (bits.Count == 0) return new("—", "—", "—", "Select bits to interpret a value.", "");
        // If bits.Count is greater than DocumentModel.MaxFieldBits, return a new object of the required
        // type using the inputs in parentheses to the caller and leave this method.
        if (bits.Count > DocumentModel.MaxFieldBits)
            // Return a new object of the required type using the inputs in parentheses to the caller and
            // leave this method.
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
        // Remember the displayed text below; each {...} inserts a computed value into the text when
        // wholeBytes is true; otherwise the text written below as significance.
        string significance = wholeBytes
            ? $"MS byte: offset {bits[0] / 8} · LS byte: offset {bits[^1] / 8}\nWithin each byte: MS bit → LS bit"
            : "Packed field · significance follows the assembly order; source bytes may contribute partial bits.";
        // This local helper translates a physical address into the ruler convention.
        // % 8 gives the position within its byte; / 8 gives that byte's offset.
        string Address(long bit) => $"byte {bit / 8}, bit {(session.Document.Settings.BitNumbering == BitNumbering.LsbZero ? 7 - bit % 8 : bit % 8)}";
        // Return a new object of the required type using the inputs in parentheses to the caller and leave
        // this method.
        return new(value.ToString(CultureInfo.InvariantCulture), signed.ToString(CultureInfo.InvariantCulture),
            NumericText.ToHex(value), significance, $"Field MSB → {Address(bits[0])}\nField LSB → {Address(bits[^1])}");
    }
}
