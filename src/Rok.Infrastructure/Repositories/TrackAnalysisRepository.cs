using Microsoft.Data.Sqlite;
using Rok.Application.Interfaces.Repositories;

namespace Rok.Infrastructure.Repositories;

/// <summary>
/// Dapper repository of <c>trackAnalysis</c>. It opens its own short-lived connection per call instead of
/// sharing the app's singleton connections, so it is safe to use from the analysis thread without any dispatch.
/// </summary>
public sealed class TrackAnalysisRepository : ITrackAnalysisRepository
{
    private const int SqliteConstraintForeignKey = 787;

    private const string SelectSql = """
        SELECT trackId, algorithmVersion, fileModifiedUtc, fileSize, musicEndSeconds, fadeOutSeconds, musicStartSeconds,
               bpm, bpmConfidence, bpmSource, introBeatPhase, outroBeatPhase, introTempoAnalysed, outroTempoAnalysed
        FROM trackAnalysis WHERE trackId = @trackId
        """;

    private const string UpsertSql = """
        INSERT INTO trackAnalysis (trackId, algorithmVersion, fileModifiedUtc, fileSize, musicEndSeconds, fadeOutSeconds, musicStartSeconds,
                                   bpm, bpmConfidence, bpmSource, introBeatPhase, outroBeatPhase, introTempoAnalysed, outroTempoAnalysed)
        VALUES (@trackId, @algorithmVersion, @fileModifiedUtc, @fileSize, @musicEndSeconds, @fadeOutSeconds, @musicStartSeconds,
                @bpm, @bpmConfidence, @bpmSource, @introBeatPhase, @outroBeatPhase, @introTempoAnalysed, @outroTempoAnalysed)
        ON CONFLICT(trackId) DO UPDATE SET
            algorithmVersion = excluded.algorithmVersion,
            fileModifiedUtc = excluded.fileModifiedUtc,
            fileSize = excluded.fileSize,
            musicEndSeconds = excluded.musicEndSeconds,
            fadeOutSeconds = excluded.fadeOutSeconds,
            musicStartSeconds = excluded.musicStartSeconds,
            bpm = excluded.bpm,
            bpmConfidence = excluded.bpmConfidence,
            bpmSource = excluded.bpmSource,
            introBeatPhase = excluded.introBeatPhase,
            outroBeatPhase = excluded.outroBeatPhase,
            introTempoAnalysed = excluded.introTempoAnalysed,
            outroTempoAnalysed = excluded.outroTempoAnalysed
        """;

    private readonly Func<IDbConnection> _connectionFactory;

    /// <summary>Initializes a new instance of the <see cref="TrackAnalysisRepository"/> class.</summary>
    /// <param name="connectionFactory">Creates a new, not yet opened, connection to the app database on each call.</param>
    public TrackAnalysisRepository(Func<IDbConnection> connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task<TrackAnalysisEntity?> GetAsync(long trackId, CancellationToken ct)
    {
        using var connection = _connectionFactory();
        connection.Open();

        var command = new CommandDefinition(SelectSql, new { trackId }, cancellationToken: ct);

        return await connection.QuerySingleOrDefaultAsync<TrackAnalysisEntity>(command).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpsertAsync(TrackAnalysisEntity entity, CancellationToken ct)
    {
        using var connection = _connectionFactory();
        connection.Open();

        var parameters = new
        {
            trackId = entity.TrackId,
            algorithmVersion = entity.AlgorithmVersion,
            fileModifiedUtc = entity.FileModifiedUtc,
            fileSize = entity.FileSize,
            musicEndSeconds = entity.MusicEndSeconds,
            fadeOutSeconds = entity.FadeOutSeconds,
            musicStartSeconds = entity.MusicStartSeconds,
            bpm = entity.Bpm,
            bpmConfidence = entity.BpmConfidence,
            bpmSource = (int?)entity.BpmSource,
            introBeatPhase = entity.IntroBeatPhase,
            outroBeatPhase = entity.OutroBeatPhase,
            introTempoAnalysed = entity.IntroTempoAnalysed,
            outroTempoAnalysed = entity.OutroTempoAnalysed
        };

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(UpsertSql, parameters, cancellationToken: ct)).ConfigureAwait(false);
        }
        catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == SqliteConstraintForeignKey)
        {
            throw new InvalidOperationException($"Track {entity.TrackId} does not exist.", ex);
        }
    }
}