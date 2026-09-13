using System.Globalization;
using System.Numerics;

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
        string input = text.Trim().Replace("_", "");
        bool prefix = input.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        bool hex = prefix || defaultHex;
        if (prefix) input = input[2..];
        if (input.Length == 0 || input.Length > 4096) throw new ArgumentException("Enter a numeric value.");
        if (hex)
        {
            if (!input.All(char.IsAsciiHexDigit)) throw new ArgumentException("Enter a hexadecimal value using 0–9 and A–F.");
            // BigInteger hex parsing can treat a high leading bit as a negative sign.
            // Prefixing zero makes FF parse as positive 255, rather than negative one.
            return BigInteger.Parse("0" + input, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        }
        if (!BigInteger.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            throw new ArgumentException("Enter a decimal value, or prefix hexadecimal with 0x.");
        return value;
    }

    /// <summary>Formats an unsigned field value with a 0x prefix, removing unnecessary leading zeros.</summary>
    public static string ToHex(BigInteger value)
    {
        string hex = value.ToString("X", CultureInfo.InvariantCulture).TrimStart('0');
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
        return offsets.Length switch
        {
            0 => "no source",
            1 => $"byte {offsets[0]}",
            _ => $"bytes {offsets[0]}–{offsets[^1]}"
        };
    }

    /// <summary>Keeps short values intact and uses an ellipsis for long previews without changing the actual value.</summary>
    private static string Shorten(string text, int length) => text.Length <= length ? text : text[..(length - 3)] + "…";
}
