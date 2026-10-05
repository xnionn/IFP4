using System.Globalization;
using System.Text.Json;

namespace IFP4;

public static class AssignmentChecks
{
    private static readonly DateTimeOffset Timestamp =
        new DateTimeOffset(2026, 10, 3, 9, 15, 20, TimeSpan.FromHours(5)).AddTicks(1234567);

    private static readonly AttendanceEntry[] Entries =
    [
        new("Alimzhan Almukhambetov", Timestamp),
        new("Ақбота, \"A\"\tB\nC\\D 🙂", Timestamp.AddMinutes(1))
    ];

    public static int Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "IFP4-checks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        int passed = 0, failed = 0;

        void Check(string name, Action action)
        {
            try
            {
                action();
                passed++;
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception error)
            {
                failed++;
                Console.WriteLine($"FAIL {name}: {error.GetType().Name}: {error.Message}");
            }
        }

        try
        {
            Check("Pure text conversion preserves special characters and timestamp offset", () =>
            {
                string[] names = ["Alice", "A, B", "\"Quoted\"", "Қазақша", "Tab\tName",
                    "Line\nBreak", "Carriage\rReturn", "Slash\\Name", "🙂 Student"];
                foreach (string name in names)
                {
                    var entry = new AttendanceEntry(name, Timestamp);
                    string line = AttendanceTextFormat.Serialize(entry);
                    Assert(!line.Contains('\r') && !line.Contains('\n'), "One entry must occupy one physical line.");
                    SameEntries([entry], [AttendanceTextFormat.Deserialize(line)]);
                }
            });
            Check("Text format is invariant across cultures", () =>
            {
                CultureInfo originalCulture = CultureInfo.CurrentCulture;
                CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
                try
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                    string french = AttendanceTextFormat.Serialize(Entries[0]);
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
                    CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
                    Assert(french == AttendanceTextFormat.Serialize(Entries[0]), "Culture changed the serialized text.");
                    Assert(french.StartsWith("2026-10-03T09:15:20.1234567+05:00\t", StringComparison.Ordinal),
                        "Expected an invariant round-trip timestamp followed by a tab.");
                    SameEntries([Entries[0]], [AttendanceTextFormat.Deserialize(french)]);
                }
                finally
                {
                    CultureInfo.CurrentCulture = originalCulture;
                    CultureInfo.CurrentUICulture = originalUiCulture;
                }
            });
            Check("Malformed text records are rejected", () =>
            {
                const string prefix = "2026-10-03T09:15:20.1234567+05:00\t";
                string[] invalid = ["", "no separator", "not-a-date\t\"Alice\"", prefix + "Alice",
                    prefix + "null", prefix + "123", prefix + "\"\"", prefix + "\"   \"",
                    prefix + "\"Alice\"\textra", prefix + "\"unterminated"];
                foreach (string line in invalid)
                    Throws<FormatException>(() => AttendanceTextFormat.Deserialize(line));
            });
            Check("Invalid entries are rejected before serialization", () =>
            {
                Throws<ArgumentException>(() => AttendanceTextFormat.Serialize(new AttendanceEntry(null!, Timestamp)));
                Throws<ArgumentException>(() => AttendanceTextFormat.Serialize(new AttendanceEntry("  ", Timestamp)));
            });

            (string Name, Func<string, IAttendanceStore> Create)[] stores =
            [
                ("Text", path => new TextLogStore(path)),
                ("JSON", path => new JsonStore(path))
            ];
            foreach (var (name, create) in stores)
            {
                string FileFor(string test) => Path.Combine(root, name + "-" + test);
                Check($"{name}: missing and zero-byte files load empty", () =>
                {
                    string path = FileFor("empty");
                    SameEntries([], create(path).Load());
                    File.WriteAllText(path, "");
                    SameEntries([], create(path).Load());
                    AssertUnlocked(path);
                });
                Check($"{name}: first append creates a file and round-trips one entry", () =>
                {
                    string path = FileFor("singleton");
                    create(path).Append([Entries[0]]);
                    Assert(File.Exists(path), "Append did not create the file.");
                    AssertUnlocked(path);
                    SameEntries([Entries[0]], create(path).Load());
                    AssertUnlocked(path);
                });
                Check($"{name}: repeated calls preserve order and values across fresh instances", () =>
                {
                    string path = FileFor("runs");
                    create(path).Append(Entries);
                    SameEntries(Entries, create(path).Load());
                    AttendanceEntry[] second = [new("Second run", Timestamp.AddHours(1)),
                        new("Final student", Timestamp.AddHours(2))];
                    create(path).Append(second);
                    SameEntries(Entries.Concat(second), create(path).Load());
                });
                Check($"{name}: empty append is harmless", () =>
                {
                    string path = FileFor("empty-batch");
                    create(path).Append([]);
                    SameEntries([], create(path).Load());
                    create(path).Append(Entries);
                    create(path).Append([]);
                    SameEntries(Entries, create(path).Load());
                });
                Check($"{name}: equal input entries are stored once per occurrence", () =>
                {
                    string path = FileFor("multiplicity");
                    create(path).Append([Entries[0], Entries[0]]);
                    create(path).Append([Entries[0]]);
                    SameEntries([Entries[0], Entries[0], Entries[0]], create(path).Load());
                });
                Check($"{name}: lazy input is enumerated exactly once and disposed", () =>
                {
                    string path = FileFor("lazy");
                    int enumerations = 0, disposals = 0;
                    IEnumerable<AttendanceEntry> Input()
                    {
                        enumerations++;
                        try { foreach (var entry in Entries) yield return entry; }
                        finally { disposals++; }
                    }
                    create(path).Append(Input());
                    Assert(enumerations == 1 && disposals == 1, "Input must be enumerated and disposed once.");
                    SameEntries(Entries, create(path).Load());
                });
                Check($"{name}: input enumeration failure preserves previous bytes", () =>
                {
                    string path = FileFor("throwing-input");
                    create(path).Append(Entries);
                    byte[] before = File.ReadAllBytes(path);
                    bool disposed = false;
                    IEnumerable<AttendanceEntry> Input()
                    {
                        try
                        {
                            yield return new AttendanceEntry("Must not be added", Timestamp);
                            throw new InvalidOperationException("Deliberate input failure.");
                        }
                        finally { disposed = true; }
                    }
                    Throws<InvalidOperationException>(() => create(path).Append(Input()));
                    Assert(disposed, "Failed input enumeration was not disposed.");
                    SameBytes(before, path);
                    AssertUnlocked(path);
                });
                Check($"{name}: invalid batch does not partially append", () =>
                {
                    string path = FileFor("invalid-batch");
                    create(path).Append(Entries);
                    byte[] before = File.ReadAllBytes(path);
                    Throws<ArgumentException>(() => create(path).Append([Entries[0], new(" ", Timestamp)]));
                    SameBytes(before, path);
                    AssertUnlocked(path);
                });
                Check($"{name}: malformed load throws and releases the file", () =>
                {
                    string path = FileFor("corrupt");
                    File.WriteAllText(path, "{ corrupted data");
                    byte[] before = File.ReadAllBytes(path);
                    if (name == "Text") Throws<FormatException>(() => create(path).Load().ToArray());
                    else Throws<JsonException>(() => create(path).Load().ToArray());
                    SameBytes(before, path);
                    AssertUnlocked(path);
                });
                Check($"{name}: locked destination failure preserves data and releases handles", () =>
                {
                    string path = FileFor("locked");
                    create(path).Append(Entries);
                    byte[] before = File.ReadAllBytes(path);
                    using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                        Throws<IOException>(() => create(path).Append([Entries[0]]));
                    SameBytes(before, path);
                    AssertUnlocked(path);
                    create(path).Append([Entries[0]]);
                    SameEntries(Entries.Append(Entries[0]), create(path).Load());
                });
            }
            Check("JSON: appending to malformed data rejects it without overwriting", () =>
            {
                string path = Path.Combine(root, "json-corrupt-append");
                File.WriteAllText(path, "[ broken JSON");
                byte[] before = File.ReadAllBytes(path);
                Throws<JsonException>(() => new JsonStore(path).Append(Entries));
                SameBytes(before, path);
                AssertUnlocked(path);
            });
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch (Exception error)
            {
                failed++;
                Console.WriteLine($"FAIL temporary-file cleanup: {error.Message}");
            }
        }

        Console.WriteLine($"Checks: {passed} passed, {failed} failed.");
        return failed == 0 ? 0 : 1;
    }

    private static void SameEntries(IEnumerable<AttendanceEntry> expected, IEnumerable<AttendanceEntry> actual)
    {
        AttendanceEntry[] left = expected.ToArray(), right = actual.ToArray();
        Assert(left.Length == right.Length, $"Expected {left.Length} entries; got {right.Length}.");
        for (int i = 0; i < left.Length; i++)
            Assert(left[i].Name == right[i].Name && left[i].CheckInTime.EqualsExact(right[i].CheckInTime),
                $"Entry {i} differs in name, timestamp, or offset.");
    }

    private static void SameBytes(byte[] expected, string path) =>
        Assert(expected.SequenceEqual(File.ReadAllBytes(path)), "Existing file bytes changed after failure.");

    private static void AssertUnlocked(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Throws<TException>(Action action) where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }
}
