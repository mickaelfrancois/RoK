using Microsoft.Extensions.Logging;
using MiF.Guard;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Rok.Shared;

public class PerfLogger : IDisposable
{
    private readonly ILogger _logger;
    private readonly Stopwatch _timer = new();
    private readonly string _caller;
    private string[]? _parameters;
    private bool disposedValue;

    public PerfLogger(ILogger logger, [CallerMemberName] string caller = "")
    {
        _logger = Guard.Against.Null(logger);
        _caller = caller;
        _timer.Start();
    }

    public PerfLogger Parameters(params string[] parameters)
    {
        _parameters = parameters;
        return this;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                _timer.Stop();

                if (_parameters == null)
                    _logger.LogInformation("{Caller}() -> {ElapsedMilliseconds}ms", _caller, _timer.ElapsedMilliseconds);
                else
                    _logger.LogInformation("{Caller}() -> {Parameters} -> {ElapsedMilliseconds}ms", _caller, string.Join(',', _parameters), _timer.ElapsedMilliseconds);

            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
