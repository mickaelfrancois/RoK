using Rok.Infrastructure.Tag;

namespace Rok.Infrastructure.UnitTests.TestData;

/// <summary>
/// Real file operations with hooks to corrupt a step or simulate a process crash.
/// Once crashed, every operation fails, as if the process were gone.
/// </summary>
internal sealed class FaultInjectingFileOperations : ITagWriteFileOperations
{
    private readonly DefaultTagWriteFileOperations _inner = new();

    public Action<string, string>? AfterCopy { get; set; }

    public Action<string, string>? ReplaceOverride { get; set; }

    public Action<string, string>? BeforeMove { get; set; }

    public Action<string>? DeleteOverride { get; set; }

    public bool Crashed { get; private set; }

    public void Crash()
    {
        Crashed = true;
    }

    public void Copy(string source, string destination)
    {
        ThrowIfCrashed();
        _inner.Copy(source, destination);
        AfterCopy?.Invoke(source, destination);
    }

    public void Replace(string source, string destination)
    {
        ThrowIfCrashed();

        if (ReplaceOverride is not null)
            ReplaceOverride(source, destination);
        else
            _inner.Replace(source, destination);
    }

    public void Move(string source, string destination)
    {
        ThrowIfCrashed();
        BeforeMove?.Invoke(source, destination);
        ThrowIfCrashed();
        _inner.Move(source, destination);
    }

    public void Delete(string path)
    {
        ThrowIfCrashed();

        if (DeleteOverride is not null)
            DeleteOverride(path);
        else
            _inner.Delete(path);
    }

    private void ThrowIfCrashed()
    {
        if (Crashed)
            throw new IOException("Simulated crash.");
    }
}