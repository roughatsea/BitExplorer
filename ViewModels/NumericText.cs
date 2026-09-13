using System.Globalization;
using System.Numerics;

namespace WpfApp1.ViewModels;

public static class NumericText
{
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
            return BigInteger.Parse("0" + input, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        }
        if (!BigInteger.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            throw new ArgumentException("Enter a decimal value, or prefix hexadecimal with 0x.");
        return value;
    }

    public static string ToHex(BigInteger value)
    {
        string hex = value.ToString("X", CultureInfo.InvariantCulture).TrimStart('0');
        return "0x" + (hex.Length == 0 ? "0" : hex);
    }

    public static string ShortHex(BigInteger value) => Shorten(ToHex(value), 16);
    public static string ShortNumber(BigInteger value) => Shorten(value.ToString(CultureInfo.InvariantCulture), 15);

    public static string DescribeSource(IEnumerable<long> bits)
    {
        var offsets = bits.Select(bit => bit / 8).Distinct().Order().ToArray();
        return offsets.Length switch
        {
            0 => "no source",
            1 => $"byte {offsets[0]}",
            _ => $"bytes {offsets[0]}–{offsets[^1]}"
        };
    }

    private static string Shorten(string text, int length) => text.Length <= length ? text : text[..(length - 3)] + "…";
}
