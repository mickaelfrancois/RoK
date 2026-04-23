namespace Rok.Application.Interfaces;

public interface IFileSystem
{
    string Combine(string path1, string path2);
    string? GetDirectoryName(string path);
    string GetFileNameWithoutExtension(string path);

    bool FileExists(string path);
    bool DirectoryExists(string path);

    void DirectoryCreate(string path);

    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default);
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default);

    Task WriteAllBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default);
    Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default);
    Task<Stream> OpenWriteAsync(string path, bool overwrite = true, CancellationToken cancellationToken = default);

    Task CopyFileAsync(string sourcePath, string destinationPath, bool overwrite = false, CancellationToken cancellationToken = default);
    Task MoveFileAsync(string sourcePath, string destinationPath, bool overwrite = false, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string path, CancellationToken cancellationToken = default);

    Task EnsureDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default);
    Task DeleteDirectoryAsync(string directoryPath, bool recursive = false, CancellationToken cancellationToken = default);

    Task<IEnumerable<string>> GetFilesAsync(string directoryPath, string searchPattern = "*", bool recursive = false, CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> GetDirectoriesAsync(string directoryPath, string searchPattern = "*", bool recursive = false, CancellationToken cancellationToken = default);
}
