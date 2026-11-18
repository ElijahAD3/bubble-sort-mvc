using System.Globalization;
using System.Text.RegularExpressions;

namespace BubbleSortMvc.Services;

public static partial class SortInputValidator
{
    [GeneratedRegex(@"^[+-]?[0-9]+(?:\.[0-9]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberPattern();

    public static bool IsValid(string type, string[] values) => type switch
    {
        "number" => values.All(value => NumberPattern().IsMatch(value) &&
            decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out _)),
        "date" => values.All(value => DateOnly.TryParseExact(value, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out _)),
        "text" => true,
        _ => false
    };
}
