using System.Globalization;

namespace TaskList.Data;

/// <summary>
/// Every instant inside the app is UTC. Local time is used only at the UI boundary
/// and for calendar-based trigger boundaries (a daily task fires at local wall-clock time).
/// </summary>
public static class UtcTime
{
    public const string IsoFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    public static DateTime Now => DateTime.UtcNow;

    public static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    public static DateTime ToLocal(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();

    public static DateTime FromLocal(DateTime local) =>
        DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();

    public static string Format(DateTime utc, string format) =>
        ToLocal(utc).ToString(format, CultureInfo.CurrentCulture);

    public static string Iso(DateTime utc) =>
        AsUtc(utc).ToString(IsoFormat, CultureInfo.InvariantCulture);

    /// <summary>Parses the exact UTC ISO-8601 format written by this app.</summary>
    public static bool TryParse(string text, out DateTime utc)
    {
        if (DateTime.TryParseExact(text, IsoFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            utc = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            return true;
        }

        utc = default;
        return false;
    }
}
