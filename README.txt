ASSIGNMENT 4 | WEEK 4
Working with Files, Streams, and Serialization
Student: Alimzhan Almukhambetov
Group: IT-2510

REQUIREMENTS

Install the .NET 10 SDK. This is a net10.0 C# console application with nullable
reference types and implicit usings enabled. It uses only the standard library;
there are no external NuGet packages. Run the commands below from the folder
containing IFP4.csproj. The commands work in PowerShell or a terminal.

BUILD, DEMONSTRATE, AND CHECK

1. Build the application:
   dotnet build

2. Run all four assignment tasks:
   dotnet run --no-build -- --demo

   The default demonstration creates a unique directory under artifacts/runs/.
   Its full path is printed. Task 1 keeps evidence of the original defects;
   Task 2 demonstrates string conversion in memory; Task 3 compares two stores;
   Task 4 performs two simulated runs with fresh store objects.

   To select the output directory yourself:
   dotnet run --no-build -- --demo artifacts/my-demo

   Select a new or empty directory. The demonstration deliberately refuses to
   reset a nonempty directory, so another run needs another directory name.

3. Run the automated normal, boundary, invalid-data, and failure checks:
   dotnet run --no-build -- --checks

   Every successful check prints PASS. The summary must report zero failures.
   The process exit code is 0 on success and 1 if a check fails. Checks use a
   unique temporary directory and remove their own test data afterwards.

4. Demonstrate persistence across two actual application processes:
   dotnet run --no-build -- --run 1 artifacts/my-pipeline
   dotnet run --no-build -- --run 2 artifacts/my-pipeline

   Run 1 requires a new or empty directory and writes two check-ins to each
   store. Run 2 uses the same directory and adds two different check-ins.
   Expected output: MATCH: 2 entries after run 1; MATCH: 4 entries after run 2;
   identical count, values and order; Duplicate entries: 0. Both files retain
   the first two entries. Execute each run once and in this order. To repeat
   the experiment, choose a different directory name; there is no reset flag.

   Recorded execution logs for this submission are under artifacts/evidence/.
   These logs supplement the source code and can be regenerated with the
   commands above. --demo and --run use separate files for Tasks 3 and 4.

PROJECT LAYOUT

IFP4.csproj                       .NET project configuration
Program.cs                        Command-line entry point and error handling
Models/AttendanceEntry.cs          Immutable Name/CheckInTime record
Storage/IAttendanceStore.cs        Shared Append and Load contract
Storage/AttendanceTextFormat.cs    Pure Serialize and Deserialize functions
Storage/TextLogStore.cs            UTF-8 line-oriented append implementation
Storage/JsonStore.cs               JSON load/merge/replace implementation
Starter/AttendanceLog.cs           Original supplied implementation
StarterDiagnosis.cs               Four reproducible starter defects
Demonstrations.cs                 Shared client and Tasks 2-4 demonstrations
Checks/AssignmentChecks.cs         Automated verification harness
DEFENSE_RU.txt                     Russian oral-defense preparation

THE FOUR STARTER DEFECTS

1. Two Record calls leave only the second entry. File.WriteAllText(path, line)
   truncates the existing file. Expected: both earlier and new entries survive.
2. ReadAll on a nonexistent log throws FileNotFoundException. Expected: an
   empty attendance result before the first check-in.
3. A name containing a newline becomes multiple physical lines. Expected: one
   encoded record for one check-in. The original concatenation never escapes
   the name; it also leaves commas in names ambiguous to a CSV-style reader.
4. Implicit DateTime.Now string conversion depends on CurrentCulture. The
   en-US and en-GB reproductions show different date/clock conventions.
   Expected: a stable representation that a later run can interpret reliably.

These are defects in the supplied behavior. An interface, record model, and
JSON support are later requirements, not additional starter-code defects.

STORAGE DESIGN AND FORMAT

AttendanceEntry is a sealed positional record containing a string Name and a
DateTimeOffset CheckInTime. Positional properties are init-only; updating a
value with a with-expression creates another record. Dates include their UTC
offset and use seven fractional-second digits in the text representation.

One text record is:
2026-10-03T09:00:00.0000000+05:00<TAB>"Alimzhan Almukhambetov"

<TAB> above denotes one literal tab character. The timestamp uses the invariant
"O" round-trip format. The name is a JSON string, so quotes, backslashes, tabs,
and newlines are escaped. AttendanceTextFormat.Serialize and Deserialize are
plain functions with no file access. TextLogStore uses them for every written
and read line. The same formatted values are printed by the shared client.

TextLogStore snapshots and serializes its incoming batch before opening a
StreamWriter in append mode. It appends only new lines and disposes the writer
with using. Load uses a strict UTF-8 StreamReader, fully reads the entries, and
disposes the reader before returning the collection.

JsonStore snapshots and validates its incoming batch, loads the existing JSON
array, concatenates old and new entries, and uses System.Text.Json to serialize
the combined array into a unique temporary file in the same directory. It
disposes the stream before replacing the destination. A finally block cleans
up a remaining temporary file. JSON is indented for readability.

Both Load methods return an empty collection for a missing or zero-byte file.
Append creates a missing file when its parent directory exists. Nonblank names
are required. Corrupt text raises FormatException; malformed JSON, null roots,
null entries, missing required properties, and blank names are rejected.
Permission errors and other I/O failures are reported rather than treated as
empty logs. Text Append does not parse existing lines; Text Load validates them.

AppendAndShow(IAttendanceStore store, IEnumerable<AttendanceEntry> entries) is
the unchanged client function used for either concrete implementation. Its
Append and Load calls dispatch through the interface to the selected store.

LIMITS AND CONTRACT DETAILS

The stores assume one writer at a time. They do not provide multi-process
transactions or coordinate concurrent writers. Parent directories must exist;
the demonstrations create them before using the stores.

Every input occurrence is appended once. The stores do not silently deduplicate
equal entries. Calling Append twice with the same batch intentionally records
the batch twice: Append is not idempotent. Demonstration batches contain distinct
check-ins and are submitted once, so the demonstrated files have no duplicates.

Input enumeration or validation failure occurs before writes start. using
releases stream handles even when an operation throws. A physical disk/write
failure during text append can still leave a partially appended batch; disposal
is not rollback. JSON replaces the previous destination only after successful
serialization and stream disposal. The design does not promise recovery from
power loss or filesystem/hardware corruption.

For N bytes of new data and M bytes already stored, text append takes O(N)
time and O(N) batch memory; text load takes O(M) time and O(M) result memory.
JSON append takes O(M + N) time and memory because it rewrites the full array;
JSON load takes O(M) time and memory.
