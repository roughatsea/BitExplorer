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

// Place this file's definitions in the WpfApp1.ViewModels naming group, which prevents clashes with names
// in other groups.
namespace WpfApp1.ViewModels;

/// <summary>
/// Shared numeric text rules for view models. BigInteger can represent values much
/// wider than a normal 32- or 64-bit integer, which is necessary for custom bit fields.
/// Parsing and formatting use an invariant culture to stay consistent across machines.
/// </summary>
public static class NumericText
{
    /// <summary>
    /// Parses decimal by default, or hexadecimal with a 0x prefix/defaultHex option.
    /// Underscores are allowed for readability. Errors explain what the input requires.
    /// </summary>
    public static BigInteger Parse(string text, bool defaultHex = false)
    {
        // Remove spacing at the ends and discard readability underscores anywhere.
        // For example, " 1_024 " becomes "1024" before we interpret its digits.
        string input = text.Trim().Replace("_", "");
        // Recognize an explicit hexadecimal prefix; IgnoreCase accepts both 0x and 0X.
        bool prefix = input.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        // Use hexadecimal either because the user requested it with 0x or because
        // the caller selected hex as the default. Otherwise, use decimal.
        bool hex = prefix || defaultHex;
        // The parser needs the digits, not the prefix. [2..] takes the text from
        // character position two onward: "0xFF" becomes "FF".
        if (prefix) input = input[2..];
        // An empty string has no number to read. The upper bound also stops an
        // excessively long input from requesting unnecessary parsing work.
        if (input.Length == 0 || input.Length > 4096) throw new ArgumentException("Enter a numeric value.");
        // If hex is true, run the following grouped instructions.
        if (hex)
        {
            // All checks every character. Reject the input unless each character
            // is one of 0-9, A-F, or a-f; a hex prefix does not make other text valid.
            if (!input.All(char.IsAsciiHexDigit)) throw new ArgumentException("Enter a hexadecimal value using 0–9 and A–F.");
            // BigInteger hex parsing can treat a high leading bit as a negative sign.
            // Prefixing zero makes FF parse as positive 255, rather than negative one.
            return BigInteger.Parse("0" + input, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        }
        // Try to read an ordinary signed decimal integer. TryParse answers true
        // or false and, through out var value, also gives us the parsed number.
        // The leading ! enters the error branch when that attempt answered false.
        if (!BigInteger.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            // Stop this normal path by reporting ArgumentException; the arguments carry the error details.
            throw new ArgumentException("Enter a decimal value, or prefix hexadecimal with 0x.");
        // Return value to the caller and leave this method.
        return value;
    }

    /// <summary>Formats an unsigned field value with a 0x prefix, removing unnecessary leading zeros.</summary>
    public static string ToHex(BigInteger value)
    {
        // Write uppercase hexadecimal digits, then remove unnecessary leading
        // zeroes. A zero value becomes empty text here, which the next line repairs.
        string hex = value.ToString("X", CultureInfo.InvariantCulture).TrimStart('0');
        // Add the identifying 0x prefix. Keep at least one digit, so zero reads
        // "0x0", never just "0x". The + joins text here rather than adding numbers.
        return "0x" + (hex.Length == 0 ? "0" : hex);
    }

    /// <summary>Produces a short hexadecimal preview for a field-list row; the inspector shows the full value.</summary>
    public static string ShortHex(BigInteger value) => Shorten(ToHex(value), 16);
    /// <summary>Produces a short decimal preview for a field-list row.</summary>
    public static string ShortNumber(BigInteger value) => Shorten(value.ToString(CultureInfo.InvariantCulture), 15);

    /// <summary>Summarizes the first and last source bytes; a span may include gaps in a custom field map.</summary>
    public static string DescribeSource(IEnumerable<long> bits)
    {
        // Integer division maps bit addresses to bytes; Distinct removes repeated byte offsets.
        var offsets = bits.Select(bit => bit / 8).Distinct().Order().ToArray();
        // Return the result selected by matching offsets.Length to one of the cases below to the caller and
        // leave this method.
        return offsets.Length switch
        {
            // For 0, use the text "no source".
            0 => "no source",
            // For 1, use the displayed text below; each {...} inserts a computed value into the text.
            1 => $"byte {offsets[0]}",
            // For any remaining case, use the displayed text below; each {...} inserts a computed value
            // into the text.
            _ => $"bytes {offsets[0]}–{offsets[^1]}"
        };
    }

    /// <summary>Keeps short values intact and uses an ellipsis for long previews without changing the actual value.</summary>
    private static string Shorten(string text, int length) => text.Length <= length ? text : text[..(length - 3)] + "…";
}
