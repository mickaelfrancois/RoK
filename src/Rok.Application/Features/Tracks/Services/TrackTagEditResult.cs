using Rok.Application.Tag;

namespace Rok.Application.Features.Tracks.Services;

/// <summary>
/// Outcome of a tag edit on one track.
/// </summary>
/// <param name="TrackId">Id of the track.</param>
/// <param name="Title">Title requested for the track.</param>
/// <param name="Outcome">Final state.</param>
/// <param name="Reason">Reason when not succeeded, otherwise <see cref="ETagWriteReason.None"/>.</param>
/// <param name="ErrorCode">Stable error code when not succeeded, otherwise null.</param>
public sealed record TrackTagEditResult(long TrackId, string Title, ETagWriteOutcome Outcome, ETagWriteReason Reason, string? ErrorCode)
{
    /// <summary>
    /// Error code returned when the tag writing option is turned off.
    /// </summary>
    public const string DisabledCode = "track.tag_write.disabled";

    /// <summary>
    /// Gets whether the track file and the database are both up to date.
    /// </summary>
    public bool Succeeded => Outcome == ETagWriteOutcome.Succeeded;

    /// <summary>
    /// Builds the stable error code of a reason, for example <c>track.tag_write.import_in_progress</c>.
    /// </summary>
    /// <param name="reason">The reason of the refusal or failure.</param>
    /// <returns>The error code.</returns>
    public static string CodeFor(ETagWriteReason reason)
    {
        return reason switch
        {
            ETagWriteReason.NotFound => "track.tag_write.not_found",
            ETagWriteReason.ReadOnly => "track.tag_write.read_only",
            ETagWriteReason.CloudPlaceholder => "track.tag_write.cloud_placeholder",
            ETagWriteReason.UnsupportedFormat => "track.tag_write.unsupported_format",
            ETagWriteReason.Locked => "track.tag_write.locked",
            ETagWriteReason.InsufficientDiskSpace => "track.tag_write.insufficient_disk_space",
            ETagWriteReason.UnsupportedTagVersion => "track.tag_write.unsupported_tag_version",
            ETagWriteReason.Playing => "track.tag_write.playing",
            ETagWriteReason.ImportInProgress => "track.tag_write.import_in_progress",
            ETagWriteReason.Cancelled => "track.tag_write.cancelled",
            ETagWriteReason.VerificationFailed => "track.tag_write.verification_failed",
            _ => "track.tag_write.io_error"
        };
    }

    internal static TrackTagEditResult Succeed(long trackId, string title)
    {
        return new TrackTagEditResult(trackId, title, ETagWriteOutcome.Succeeded, ETagWriteReason.None, null);
    }

    internal static TrackTagEditResult Skip(long trackId, string title, ETagWriteReason reason)
    {
        return new TrackTagEditResult(trackId, title, ETagWriteOutcome.Skipped, reason, CodeFor(reason));
    }

    internal static TrackTagEditResult Fail(long trackId, string title, ETagWriteReason reason)
    {
        return new TrackTagEditResult(trackId, title, ETagWriteOutcome.Failed, reason, CodeFor(reason));
    }

    internal static TrackTagEditResult Disabled(long trackId, string title)
    {
        return new TrackTagEditResult(trackId, title, ETagWriteOutcome.Skipped, ETagWriteReason.None, DisabledCode);
    }
}