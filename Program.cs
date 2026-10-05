using IFP4;

try
{
    if (args.Length == 1 && args[0] == "--checks")
        return AssignmentChecks.Run();
    if (args.Length == 3 && args[0] == "--run"
        && int.TryParse(args[1], out int run) && run is 1 or 2)
    {
        Demonstrations.RunPipeline(run, Path.GetFullPath(args[2]));
        return 0;
    }
    if (args.Length > 2 || (args.Length > 0 && args[0] != "--demo"))
    {
        Console.Error.WriteLine("Usage: dotnet run -- [--demo [new-directory]"
            + " | --checks | --run 1|2 directory]");
        return 2;
    }
    string directory = args.Length == 2 ? Path.GetFullPath(args[1])
        : Path.Combine(Environment.CurrentDirectory, "artifacts", "runs",
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-"
            + Guid.NewGuid().ToString("N")[..8]);
    Demonstrations.RunAll(directory);
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"{exception.GetType().Name}: {exception.Message}");
    return 1;
}
ope