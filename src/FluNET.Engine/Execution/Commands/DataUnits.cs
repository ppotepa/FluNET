using System.Globalization;

namespace FluNET.Execution.Commands;

internal static class DataUnits
{
    public static bool TryParse(string value, out decimal number)
    {
        number = default;
        string text = value.Trim().ToUpperInvariant();
        string[] units = ["TIB", "GIB", "MIB", "KIB", "TB", "GB", "MB", "KB", "B"];
        string? unit = units.FirstOrDefault(text.EndsWith);
        if (unit is null || !decimal.TryParse(text[..^unit.Length], NumberStyles.Float, CultureInfo.InvariantCulture, out decimal raw))
            return false;
        decimal multiplier = unit switch
        {
            "B" => 1,
            "KB" => 1_000m,
            "MB" => 1_000_000m,
            "GB" => 1_000_000_000m,
            "TB" => 1_000_000_000_000m,
            "KIB" => 1_024m,
            "MIB" => 1_048_576m,
            "GIB" => 1_073_741_824m,
            "TIB" => 1_099_511_627_776m,
            _ => 0
        };
        try { number = raw * multiplier; }
        catch (OverflowException) { number = default; return false; }
        return raw >= 0;
    }
}
