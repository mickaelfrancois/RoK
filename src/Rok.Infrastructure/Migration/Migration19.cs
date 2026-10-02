namespace Rok.Infrastructure.Migration;

/// <summary>
/// Adds the bar start (downbeat) of the intro and outro windows and the best mix point (position and score)
/// of the outro to <c>trackAnalysis</c>. No back-fill: the analysis version change makes the rows be recomputed.
/// </summary>
public class Migration19 : IMigration
{
    public int TargetVersion => 19;

    public void Apply(IDbConnection connection)
    {
        connection.Execute("ALTER TABLE trackAnalysis ADD COLUMN introDownbeatSeconds REAL NULL;");
        connection.Execute("ALTER TABLE trackAnalysis ADD COLUMN outroDownbeatSeconds REAL NULL;");
        connection.Execute("ALTER TABLE trackAnalysis ADD COLUMN outroMixPointSeconds REAL NULL;");
        connection.Execute("ALTER TABLE trackAnalysis ADD COLUMN outroMixPointScore REAL NULL;");
    }
}