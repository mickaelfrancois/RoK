namespace Rok.Infrastructure.Tag;

internal sealed class DefaultTagWriteFileOperations : ITagWriteFileOperations
{
    public void Copy(string source, string destination)
    {
        File.Copy(source, destination, overwrite: false);
    }

    public void Replace(string source, string destination)
    {
        File.Replace(source, destination, destinationBackupFileName: null);
    }

    public void Move(string source, string destination)
    {
        File.Move(source, destination, overwrite: false);
    }

    public void Delete(string path)
    {
        File.Delete(path);
    }
}