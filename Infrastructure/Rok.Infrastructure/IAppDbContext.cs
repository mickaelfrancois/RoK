namespace Rok.Infrastructure;

public interface IAppDbContext
{
    bool IsFirstStart { get; }

    IDbConnection Connection { get; }

    IDbConnection BackgroundConnection { get; }

    IDbConnection GetOpenConnection();

    void EnsureCreated();
}