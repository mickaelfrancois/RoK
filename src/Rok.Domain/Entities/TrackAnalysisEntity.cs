using Rok.Domain.Enums;

namespace Rok.Domain.Entities;

/// <summary>
/// Cached Mix analysis of a track: transition cues, tempo and beat phase. The row is keyed by the track
/// and filled window by window (intro, outro) as the track is analysed.
/// </summary>
[Table("trackAnalysis")]
public class TrackAnalysisEntity
{
    /// <summary>Gets or sets the analysed track; also the primary key.</summary>
    [Dapper.Contrib.Extensions.ExplicitKey]
    public long TrackId { get; set; }

    /// <summary>Gets or sets the version of the analysis algorithm that produced the row.</summary>
    public int AlgorithmVersion { get; set; }

    /// <summary>Gets or sets the file date of the track when it was analysed (the track's <c>FileDate</c>, compared by equality).</summary>
    public DateTime FileModifiedUtc { get; set; }

    /// <summary>Gets or sets the file size, in bytes, of the track when it was analysed.</summary>
    public long FileSize { get; set; }

    /// <summary>Gets or sets the position where the music ends, in seconds from the start of the track.</summary>
    public double? MusicEndSeconds { get; set; }

    /// <summary>Gets or sets the length of the closing fade-out, in seconds.</summary>
    public double? FadeOutSeconds { get; set; }

    /// <summary>Gets or sets the position where the music starts, in seconds from the start of the track.</summary>
    public double? MusicStartSeconds { get; set; }

    /// <summary>Gets or sets the tempo of the track, in beats per minute.</summary>
    public double? Bpm { get; set; }

    /// <summary>Gets or sets the confidence of <see cref="Bpm"/>, between 0 and 1.</summary>
    public double? BpmConfidence { get; set; }

    /// <summary>Gets or sets where <see cref="Bpm"/> came from.</summary>
    public BpmSource? BpmSource { get; set; }

    /// <summary>Gets or sets the absolute position, in seconds, of the first beat of the intro window.</summary>
    public double? IntroBeatPhase { get; set; }

    /// <summary>Gets or sets the absolute position, in seconds, of the first beat of the outro window.</summary>
    public double? OutroBeatPhase { get; set; }

    /// <summary>Gets or sets the absolute position, in seconds, of the first bar start of the intro window.</summary>
    public double? IntroDownbeatSeconds { get; set; }

    /// <summary>Gets or sets the absolute position, in seconds, of the first bar start of the outro window.</summary>
    public double? OutroDownbeatSeconds { get; set; }

    /// <summary>Gets or sets the absolute position, in seconds, of the best bar boundary to start a mix on, whatever its score.</summary>
    public double? OutroMixPointSeconds { get; set; }

    /// <summary>Gets or sets the score of the best mix point, between 0 and 1.5.</summary>
    public double? OutroMixPointScore { get; set; }

    /// <summary>Gets or sets a value indicating whether tempo detection ran on the intro window.</summary>
    public bool IntroTempoAnalysed { get; set; }

    /// <summary>Gets or sets a value indicating whether tempo detection ran on the outro window.</summary>
    public bool OutroTempoAnalysed { get; set; }
}