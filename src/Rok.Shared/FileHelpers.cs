namespace Rok.Shared;

/// <summary>File system helpers shared across layers.</summary>
public static class FileHelpers
{
    private const int FILE_ATTRIBUTE_RECALL_ON_OPEN = 0x00400000;

    /// <summary>Tells whether the file is a cloud placeholder that would be downloaded on open.</summary>
    /// <param name="file">Path of the file.</param>
    /// <returns><c>true</c> when the file content is not stored locally.</returns>
    public static bool IsOnline(string file)
    {
        FileAttributes attributes = File.GetAttributes(file);

        bool isOnline = (((int)attributes) & FILE_ATTRIBUTE_RECALL_ON_OPEN) != 0;

        return isOnline;
    }
}