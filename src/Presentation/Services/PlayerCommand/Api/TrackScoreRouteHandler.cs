namespace Rok.Services.PlayerCommand.Api;

/// <summary>
/// Rates a track from the web companion at <c>POST /api/tracks/{id}/score/{0-5}</c>.
/// The new rating is persisted and mirrored onto the queued copies of the track.
/// </summary>
public sealed class TrackScoreRouteHandler(IPlayerCommandService commandService, Action<Action> dispatch) : IWebApiRouteHandler
{
    private const string Prefix = "/api/tracks/";
    private const string ScoreSegment = "/score/";

    public bool CanHandle(string method, string path) =>
        method == "POST" && path.StartsWith(Prefix, StringComparison.Ordinal) && path.Contains(ScoreSegment, StringComparison.Ordinal);

    public async Task<WebApiResult> HandleAsync(string path)
    {
        string remainder = path[Prefix.Length..];
        int separator = remainder.IndexOf(ScoreSegment, StringComparison.Ordinal);

        if (!long.TryParse(remainder[..separator], out long trackId)
            || !int.TryParse(remainder[(separator + ScoreSegment.Length)..], out int score)
            || score is < 0 or > 5)
        {
            return WebApiResult.BadRequest();
        }

        return await UiDispatch.RunTaskAsync(dispatch, () => commandService.SetScoreAsync(trackId, score))
            ? WebApiResult.Ok()
            : WebApiResult.NotFound($"Track {trackId} could not be rated");
    }
}