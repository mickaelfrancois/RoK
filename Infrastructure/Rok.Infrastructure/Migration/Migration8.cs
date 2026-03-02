namespace Rok.Infrastructure.Migration;

public class Migration8 : IMigration
{
    public int TargetVersion => 8;

    public void Apply(IDbConnection connection)
    {
        connection.Execute("ALTER TABLE Artists ADD COLUMN pictureDominantColor INTEGER NULL;");
        connection.Execute("ALTER TABLE Albums ADD COLUMN pictureDominantColor INTEGER NULL;");
    }
}