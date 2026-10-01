namespace Rok.Application.Player.Mix;

/// <summary>Role of a track in the bass swap of a Mix transition.</summary>
public enum EBassSwapRole
{
    /// <summary>The track that fades in: its bass is cut at first, then restored.</summary>
    Incoming,

    /// <summary>The track that fades out: its bass is kept at first, then cut.</summary>
    Outgoing
}