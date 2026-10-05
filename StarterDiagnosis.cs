using System.Globalization;

namespace IFP4;

public static class StarterDiagnosis
{
    public static void Run(string directory)
    {
        Directory.CreateDirectory(directory);
        Console.WriteLine("TASK 1: reproducible defects in the original code");
        string overwritten = Path.Combine(directory, "overwritten.txt");
        AttendanceLog.Record(overwritten, "First Student");
        AttendanceLog.Record(overwritten, "Second Student");
        string remaining = AttendanceLog.ReadAll(overwritten);
        Console.WriteLine("BUG 1 | Expected: both names after two check-ins.");
        Console.WriteLine($"Actual: first present={remaining.Contains("First Student")}; "
            + $"second present={remaining.Contains("Second Student")}.");
        Console.WriteLine("Cause: File.WriteAllText(path, line) truncates the file.");

        Console.WriteLine("BUG 2 | Expected: empty result for a missing log.");
        try
        {
            AttendanceLog.ReadAll(Path.Combine(directory, "missing.txt"));
            throw new InvalidOperationException("Expected missing-file failure.");
        }
        catch (FileNotFoundException exception)
        {
            Console.WriteLine($"Actual: {exception.GetType().Name}.");
        }
        string multiline = Path.Combine(directory, "multiline.txt");
        AttendanceLog.Record(multiline, "Alimzhan\nAlmukhambetov");
        Console.WriteLine("BUG 3 | Expected: one physical record per check-in.");
        Console.WriteLine($"Actual: {File.ReadAllLines(multiline).Length} physical lines "
            + "from one call because the name is not escaped.");

        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            foreach (string culture in new[] { "en-US", "en-GB" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                string path = Path.Combine(directory, culture + ".txt");
                AttendanceLog.Record(path, "Same Student");
                Console.WriteLine($"BUG 4 | {culture}: {AttendanceLog.ReadAll(path)}");
            }
            Console.WriteLine("Expected: one stable timestamp format across cultures.");
            Console.WriteLine("Actual: month/day vs day/month and different clock formats.");
        }
        finally { CultureInfo.CurrentCulture = original; }
        Console.WriteLine();
    }
}
