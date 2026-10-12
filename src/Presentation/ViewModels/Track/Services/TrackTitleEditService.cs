using Rok.Application.Features.Tracks.Requests;

namespace Rok.ViewModels.Track.Services;

/// <summary>Lets the user change the title of a track and writes it through the mediator.</summary>
public class TrackTitleEditService(IMediator mediator, IDialogService dialogService, IStringResourceProvider strings, ILogger<TrackTitleEditService> logger)
{
    /// <summary>Opens the edit dialog and saves the typed title.</summary>
    /// <param name="trackId">Id of the track.</param>
    /// <param name="currentTitle">Title the track has now.</param>
    /// <returns>The saved title, or <see langword="null"/> when the dialog was dismissed.</returns>
    public Task<string?> EditTitleAsync(long trackId, string currentTitle)
    {
        return dialogService.ShowEditTrackTitleAsync(currentTitle, title => TrySaveAsync(trackId, title));
    }

    /// <summary>Saves a title and turns a refusal into a message.</summary>
    /// <param name="trackId">Id of the track.</param>
    /// <param name="title">The new title.</param>
    /// <returns><see langword="null"/> when saved, otherwise the translated message to show to the user.</returns>
    public async Task<string?> TrySaveAsync(long trackId, string title)
    {
        try
        {
            Result<string> result = await mediator.Send(new UpdateTrackTitleRequest(trackId, title));

            if (result.IsSuccess)
                return null;

            string? code = result.Errors.FirstOrDefault()?.Code;

            return GetMessage(code);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update the title of track {TrackId}", trackId);

            return GetMessage(null);
        }
    }

    private string GetMessage(string? errorCode)
    {
        string message = strings.GetString(TrackEditTextKeys.Error(errorCode));

        if (!string.IsNullOrEmpty(message))
            return message;

        return strings.GetString(TrackEditTextKeys.GenericError);
    }
}