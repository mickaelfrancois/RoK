using System.Diagnostics;

namespace Rok.MetadataTool;

/// <summary>What the probe found about a running Rok.</summary>
internal enum RokInstanceState
{
    None,
    RokProcess,
    DatabaseInUse,
}

/// <summary>Detects whether Rok (or another program) is using the database, before the tool opens any connection.</summary>
internal static class RokInstanceProbe
{
    private const string RokProcessName = "Rok";

    /// <summary>Checks the process list first, then tries to lock the WAL shared-memory file, which every open connection keeps open.</summary>
    public static RokInstanceState Detect(string databasePath, Func<IReadOnlyCollection<string>>? runningProcessNames = null)
    {
        IReadOnlyCollection<string> names = (runningProcessNames ?? RunningProcessNames)();

        if (names.Any(name => string.Equals(name, RokProcessName, StringComparison.OrdinalIgnoreCase)))
        {
            return RokInstanceState.RokProcess;
        }

        return IsSharedMemoryFileLocked($"{databasePath}-shm") ? RokInstanceState.DatabaseInUse : RokInstanceState.None;
    }

    internal static IReadOnlyCollection<string> RunningProcessNames()
    {
        Process[] processes = Process.GetProcesses();

        try
        {
            return processes.Select(p => p.ProcessName).ToList();
        }
        finally
        {
            foreach (Process process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static bool IsSharedMemoryFileLocked(string sharedMemoryPath)
    {
        if (!File.Exists(sharedMemoryPath))
        {
            return false;
        }

        try
        {
            using FileStream stream = File.Open(sharedMemoryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }
}