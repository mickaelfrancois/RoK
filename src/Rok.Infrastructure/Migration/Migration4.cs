namespace Rok.Infrastructure.Migration;

public class Migration4 : IMigration
{
    public int TargetVersion => 4;

    public void Apply(IDbConnection connection)
    {
        connection.Execute("ALTER TABLE Countries ADD COLUMN name TEXT NULL;");

        connection.Execute("UPDATE Countries SET name = english;");
    }
}
