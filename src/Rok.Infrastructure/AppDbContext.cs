using Microsoft.Extensions.DependencyInjection;

namespace Rok.Infrastructure;

public class AppDbContext(IDbConnection connection, [FromKeyedServices("BackgroundConnection")] IDbConnection backgroundDb, IMigrationService migrationService) : IAppDbContext, IDisposable
{
    public IDbConnection Connection { get; init; } = Guard.NotNull(connection);

    public IDbConnection BackgroundConnection { get; init; } = Guard.NotNull(backgroundDb);

    private readonly IMigrationService _migrationService = Guard.NotNull(migrationService);
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

        _migrationService.MigrateToLatest();
    }

    public IDbConnection GetOpenConnection()
    {
        if (Connection.State != ConnectionState.Open)
            Connection.Open();

        if (BackgroundConnection.State != ConnectionState.Open)
            BackgroundConnection.Open();

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