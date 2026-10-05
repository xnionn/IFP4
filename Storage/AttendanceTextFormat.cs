using System.Globalization;
using System.Text.Json;

namespace IFP4;

public static class AttendanceTextFormat
{
    public static string Serialize(AttendanceEntry entry)
    {
        Validate(entry);
        return entry.CheckInTime.ToString("O", CultureInfo.InvariantCulture)
            + "\t" + JsonSerializer.Serialize(entry.Name);
    }

    public static AttendanceEntry Deserialize(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        int separator = line.IndexOf('\t');
        if (separator <= 0 || line.Contains('\r') || line.Contains('\n')
            || !DateTimeOffset.TryParseExact(line[..separator], "O",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            throw new FormatException("Expected an ISO 8601 timestamp, a tab, and a JSON-quoted name.");
        }

        try
        {
            string? name = JsonSerializer.Deserialize<string>(line[(separator + 1)..]);
            var entry = new AttendanceEntry(name!, time);
            Validate(entry);
            return entry;
        }
        catch (Exception error) when (error is JsonException or ArgumentException)
        {
            throw new FormatException("The attendance name must be a nonblank JSON string.", error);
        }
    }

    internal static void Validate(AttendanceEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Name);
    }
}
