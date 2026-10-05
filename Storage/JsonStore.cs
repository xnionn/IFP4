using System.Text.Json;

namespace IFP4;

// A read/merge/replace operation requires one writer at a time.
public sealed class JsonStore : IAttendanceStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        RespectRequiredConstructorParameters = true
    };
    private readonly string path;

    public JsonStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        this.path = Path.GetFullPath(path);
    }

    public void Append(IEnumerable<AttendanceEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        AttendanceEntry[] incoming = entries.ToArray();
        foreach (AttendanceEntry entry in incoming)
            AttendanceTextFormat.Validate(entry);

        var combined = Load().Concat(incoming).ToArray();
        string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write))
            {
                JsonSerializer.Serialize(stream, combined, Options);
            }
            // Replace the destination only after serialization and disposal succeed.
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public IEnumerable<AttendanceEntry> Load()
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length == 0)
                return Array.Empty<AttendanceEntry>();

            AttendanceEntry[] entries = JsonSerializer.Deserialize<AttendanceEntry[]>(stream, Options)
                ?? throw new JsonException("The attendance file must contain a JSON array, not null.");
            foreach (AttendanceEntry entry in entries)
            {
                try
                {
                    AttendanceTextFormat.Validate(entry);
                }
                catch (ArgumentException error)
                {
                    throw new JsonException("Each attendance entry must have a nonblank name.", error);
                }
            }
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
