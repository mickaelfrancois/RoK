using Rok.Application.Interfaces;

namespace Rok.Infrastructure.FileSystem;

public class DefaultFileSystem : IFileSystem
{
    public string Combine(string path1, string path2)
    {
        return Path.Join(path1, path2);
    }


    public string? GetDirectoryName(string path)
    {
        return Path.GetDirectoryName(path);
    }

    public string GetFileNameWithoutExtension(string path)
    {
        return Path.GetFileNameWithoutExtension(path);
    }

    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void DirectoryCreate(string path) => Directory.CreateDirectory(path);

    public async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        ValidateFile(path);

        return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
    {
        ValidateFile(path);

        using StreamReader sr = new(File.OpenRead(path));
        cancellationToken.ThrowIfCancellationRequested();

        return await sr.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default)
    {
        ValidateFile(path);

        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);

        return Task.FromResult(stream);
    }

    public async Task WriteAllBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        await EnsureDirectoryInternalAsync(Path.GetDirectoryName(path));
        await File.WriteAllBytesAsync(path, data, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        await EnsureDirectoryInternalAsync(Path.GetDirectoryName(path));
        await File.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Stream> OpenWriteAsync(string path, bool overwrite = true, CancellationToken cancellationToken = default)
    {
        await EnsureDirectoryInternalAsync(Path.GetDirectoryName(path));
        cancellationToken.ThrowIfCancellationRequested();

        FileMode mode = overwrite ? FileMode.Create : FileMode.CreateNew;

        return new FileStream(path, mode, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous);
    }

    public async Task CopyFileAsync(string sourcePath, string destinationPath, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        ValidateFile(sourcePath);

        await EnsureDirectoryInternalAsync(Path.GetDirectoryName(destinationPath));

        cancellationToken.ThrowIfCancellationRequested();
        File.Copy(sourcePath, destinationPath, overwrite);
    }

    public async Task MoveFileAsync(string sourcePath, string destinationPath, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        ValidateFile(sourcePath);

        if (overwrite && File.Exists(destinationPath))
            File.Delete(destinationPath);

        await EnsureDirectoryInternalAsync(Path.GetDirectoryName(destinationPath));

        cancellationToken.ThrowIfCancellationRequested();
        File.Move(sourcePath, destinationPath);
    }

    public Task DeleteFileAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (File.Exists(path))
            File.Delete(path);

        return Task.CompletedTask;
    }

    public async Task EnsureDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        await EnsureDirectoryInternalAsync(directoryPath);
        cancellationToken.ThrowIfCancellationRequested();
    }

    public Task DeleteDirectoryAsync(string directoryPath, bool recursive = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Directory.Exists(directoryPath))
            Directory.Delete(directoryPath, recursive);

        return Task.CompletedTask;
    }

    public Task<IEnumerable<string>> GetFilesAsync(string directoryPath, string searchPattern = "*", bool recursive = false, CancellationToken cancellationToken = default)
    {
        ValidateDirectory(directoryPath);
        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<string> files = Directory.EnumerateFiles(directoryPath, searchPattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

        return Task.FromResult(files);
    }

    public Task<IEnumerable<string>> GetDirectoriesAsync(string directoryPath, string searchPattern = "*", bool recursive = false, CancellationToken cancellationToken = default)
    {
        ValidateDirectory(directoryPath);
        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<string> dirs = Directory.EnumerateDirectories(directoryPath, searchPattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

        return Task.FromResult(dirs);
    }

    private static void ValidateFile(string? path)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
            throw new FileNotFoundException("File not found", path);
    }

    private static void ValidateDirectory(string? path)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(path);

        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Directory not found: {path}");
    }

    private static Task EnsureDirectoryInternalAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Task.CompletedTask;

        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        return Task.CompletedTask;
    }
}