using System.Text;

namespace Rok.Application.PlayerCommand.Terminal;

public static class CliArgumentParser
{
    public static string[] Parse(string arguments)
    {
        string[] parts = ParseArguments(arguments);

        return parts.Skip(1).ToArray();
    }


    private static string[] ParseArguments(string arguments)
    {
        List<string> result = new();
        StringBuilder current = new();
        bool inQuotes = false;

        foreach (char c in arguments.Trim())
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ' ' && !inQuotes)
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
            result.Add(current.ToString());

        return result.ToArray();
    }
}
