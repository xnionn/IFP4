using System.Text;

namespace IFP4;

// Stores are intended for one writer at a time.
public sealed class TextLogStore : IAttendanceStore
{
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
    private readonly string path;

    public TextLogStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        this.path = Path.GetFullPath(path);
    }

    public void Append(IEnumerable<AttendanceEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        // Finish enumeration and conversion before any existing file is modified.
        string[] lines = entries.Select(AttendanceTextFormat.Serialize).ToArray();
        using (var writer = new StreamWriter(path, append: true, Utf8))
        {
            foreach (string line in lines)
                writer.WriteLine(line);
        }
    }

    public IEnumerable<AttendanceEntry> Load()
    {
        try
        {
            using var reader = new StreamReader(path, Utf8, detectEncodingFromByteOrderMarks: false);
            var entries = new List<AttendanceEntry>();
            while (reader.ReadLine() is { } line)
                entries.Add(AttendanceTextFormat.Deserialize(line));
            return entries;
        }
        catch (FileNotFoundException)
        {
            return Array.Empty<AttendanceEntry>();
        }
        catch (DirectoryNotFoundException)
        {
            return Array.Empty<AttendanceEntry>();
        }
    }
}
