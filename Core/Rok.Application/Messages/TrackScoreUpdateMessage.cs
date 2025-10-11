namespace Rok.Application.Messages;

public class TrackScoreUpdateMessage(long trackId, int score)
{
    public long TrackId { get; set; } = trackId;
    public int Score { get; set; } = score;
}