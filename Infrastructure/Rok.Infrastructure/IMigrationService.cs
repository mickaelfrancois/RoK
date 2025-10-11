namespace Rok.Infrastructure;

public interface IMigrationService
{
    void Initial();

    int GetDatabaseVersion();

    void MigrateToLatest();
}
