using Microsoft.Extensions.DependencyInjection;

namespace Rok.Infrastructure;

public class AppDbContext(IDbConnection connection, [FromKeyedServices("BackgroundConnection")] IDbConnection backgroundDb, IMigrationService migrationService) : IAppDbContext, IDisposable
{
    public IDbConnection Connection { get; init; } = Guard.Against.Null(connection);

    public IDbConnection BackgroundConnection { get; init; } = Guard.Against.Null(backgroundDb);

    private readonly IMigrationService _migrationService = Guard.Against.Null(migrationService);
    private bool disposedValue;

    public bool IsFirstStart { get; private set; }

    public void EnsureCreated()
    {
        int version = _migrationService.GetDatabaseVersion();
        if (version == 0)
        {
            _migrationService.Initial();
            IsFirstStart = true;
        }
    }


    public IDbConnection GetOpenConnection()
    {
        if (Connection.State != ConnectionState.Open)
            Connection.Open();

        return Connection;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                Connection?.Close();
                Connection?.Dispose();
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
