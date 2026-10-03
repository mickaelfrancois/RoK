namespace Rok.MetadataTool;

internal sealed record MixScanOptions(string DatabasePath, bool Write, int? Limit, int Parallel)
{
    public static bool TryParse(string[] args, int processorCount, out MixScanOptions? options, out string? error)
    {
        options = null;
        error = null;

        string? databasePath = null;
        var write = false;
        int? limit = null;
        var parallel = Math.Max(1, processorCount - 1);

        for (var i = 0; i < args.Length; i++)
        {
            var argument = args[i];

            switch (argument)
            {
                case "--write":
                    write = true;
                    break;

                case "--limit":
                    if (!TryReadPositive(args, ref i, argument, out var limitValue, out error))
                    {
                        return false;
                    }

                    limit = limitValue;
                    break;

                case "--parallel":
                    if (!TryReadPositive(args, ref i, argument, out parallel, out error))
                    {
                        return false;
                    }

                    break;

                default:
                    if (argument.StartsWith("--", StringComparison.Ordinal))
                    {
                        error = $"Unknown option '{argument}'.";
                        return false;
                    }

                    if (databasePath is not null)
                    {
                        error = $"Unexpected argument '{argument}'.";
                        return false;
                    }

                    databasePath = argument;
                    break;
            }
        }

        if (databasePath is null)
        {
            error = "The database path is required.";
            return false;
        }

        options = new MixScanOptions(databasePath, write, limit, parallel);

        return true;
    }

    private static bool TryReadPositive(string[] args, ref int index, string name, out int value, out string? error)
    {
        value = 0;
        error = null;

        if (index + 1 >= args.Length)
        {
            error = $"Option '{name}' requires a value.";
            return false;
        }

        index++;

        if (!int.TryParse(args[index], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out value) || value <= 0)
        {
            error = $"Option '{name}' expects an integer greater than 0, got '{args[index]}'.";
            return false;
        }

        return true;
    }
}