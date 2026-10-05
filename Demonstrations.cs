namespace IFP4;

public static class Demonstrations
{
    private static readonly AttendanceEntry[] FirstRun =
    [
        new("Alimzhan Almukhambetov", At(9, 0)),
        new("Aigerim Sadykova", At(9, 2))
    ];
    private static readonly AttendanceEntry[] SecondRun =
    [
        new("Dias Omarov", At(10, 0)),
        new("Dana, \"D\" Kim", At(10, 3))
    ];
    private static DateTimeOffset At(int hour, int minute) =>
        new(2026, 10, 3, hour, minute, 0, TimeSpan.FromHours(5));

    // The single client function used unchanged with either store.
    public static AttendanceEntry[] AppendAndShow(
        IAttendanceStore store, IEnumerable<AttendanceEntry> entries)
    {
        store.Append(entries);
        AttendanceEntry[] loaded = store.Load().ToArray();
        Console.WriteLine($"Count: {loaded.Length}");
        foreach (AttendanceEntry entry in loaded)
            Console.WriteLine(AttendanceTextFormat.Serialize(entry));
        return loaded;
    }

    public static void RunAll(string directory)
    {
        RequireFreshDirectory(directory);
        Console.WriteLine("Assignment 4 | Week 4");
        Console.WriteLine("Alimzhan Almukhambetov | IT-2510");
        Console.WriteLine($"Data directory: {directory}");
        Console.WriteLine();
        StarterDiagnosis.Run(Path.Combine(directory, "task1"));

        Console.WriteLine("TASK 2: pure in-memory conversion");
        var special = new AttendanceEntry("Dana, \"D\" Kim\nIT-2510", At(9, 5));
        string line = AttendanceTextFormat.Serialize(special);
        AttendanceEntry restored = AttendanceTextFormat.Deserialize(line);
        Require(restored == special, "In-memory round trip failed.");
        Require(!line.Contains('\n') && !line.Contains('\r'),
            "A text record must occupy one physical line.");
        Console.WriteLine(line);
        Console.WriteLine("Equal values: True; physical lines: 1");
        Console.WriteLine();

        Console.WriteLine("TASK 3: one function, two implementations");
        string demo = Path.Combine(directory, "task3");
        Directory.CreateDirectory(demo);
        string textPath = Path.Combine(demo, "attendance.txt");
        string jsonPath = Path.Combine(demo, "attendance.json");
        File.WriteAllText(textPath, "");
        File.WriteAllText(jsonPath, "");
        Console.WriteLine("TextLogStore");
        var text = AppendAndShow(new TextLogStore(textPath), FirstRun);
        Console.WriteLine("JsonStore");
        var json = AppendAndShow(new JsonStore(jsonPath), FirstRun);
        Verify(text, json, FirstRun);
        Console.WriteLine();

        Console.WriteLine("TASK 4: two simulated runs, new store objects each run");
        string pipeline = Path.Combine(directory, "task4");
        RunPipeline(1, pipeline);
        RunPipeline(2, pipeline);
    }

    public static void RunPipeline(int run, string directory)
    {
        if (run is not (1 or 2))
            throw new ArgumentOutOfRangeException(nameof(run));
        string textPath = Path.Combine(directory, "attendance.txt");
        string jsonPath = Path.Combine(directory, "attendance.json");
        if (run == 1)
        {
            RequireFreshDirectory(directory);
            File.WriteAllText(textPath, "");
            File.WriteAllText(jsonPath, "");
        }
        IAttendanceStore textStore = new TextLogStore(textPath);
        IAttendanceStore jsonStore = new JsonStore(jsonPath);
        AttendanceEntry[] previous = run == 1 ? [] : FirstRun;
        Require(textStore.Load().SequenceEqual(previous)
            && jsonStore.Load().SequenceEqual(previous),
            "Unexpected prior data. Run 1 once, then run 2 once in the same directory.");
        Console.WriteLine($"Run {run} | TextLogStore");
        AttendanceEntry[] batch = run == 1 ? FirstRun : SecondRun;
        var text = AppendAndShow(textStore, batch);
        Console.WriteLine($"Run {run} | JsonStore");
        var json = AppendAndShow(jsonStore, batch);
        Verify(text, json, previous.Concat(batch).ToArray());
        Console.WriteLine("Earlier entries survived: True");
        Console.WriteLine();
    }

    private static void Verify(AttendanceEntry[] text,
        AttendanceEntry[] json, AttendanceEntry[] expected)
    {
        Require(text.SequenceEqual(expected), "Text values do not match expected.");
        Require(json.SequenceEqual(expected), "JSON values do not match expected.");
        Require(text.Distinct().Count() == text.Length
            && json.Distinct().Count() == json.Length,
            "An unexpected duplicate exists in the demonstration.");
        Console.WriteLine($"MATCH: {text.Length} entries; identical count, values and order.");
        Console.WriteLine("Duplicate entries: 0");
    }
    private static void RequireFreshDirectory(string directory)
    {
        if (Directory.Exists(directory)
            && Directory.EnumerateFileSystemEntries(directory).Any())
            throw new IOException("Choose a new or empty output directory.");
        Directory.CreateDirectory(directory);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
