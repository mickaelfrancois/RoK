namespace Rok.Infrastructure.Migration;

public interface IMigration
{
    int TargetVersion { get; }

    void Apply(IDbConnection connection);
}
