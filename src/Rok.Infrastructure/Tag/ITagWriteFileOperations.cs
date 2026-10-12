namespace Rok.Infrastructure.Tag;

/// <summary>
/// File operations used by the tag writer. Exists so tests can inject failures between two steps.
/// </summary>
internal interface ITagWriteFileOperations
{
    /// <summary>Copies a file. The destination must not exist.</summary>
    void Copy(string source, string destination);

    /// <summary>Atomically replaces the destination with the source, without keeping a backup.</summary>
    void Replace(string source, string destination);

    /// <summary>Moves a file. The destination must not exist.</summary>
    void Move(string source, string destination);

    /// <summary>Deletes a file.</summary>
    void Delete(string path);
}