using System.Text.Json.Serialization;

namespace Rok.WebApi.Contracts;

/// <summary>
/// Compile-time serialization metadata for every shape crossing the wire.
/// </summary>
/// <remarks>
/// The web companion is published trimmed, which strips the members reflection-based serialization needs: the
/// records deserialize to nothing and the page dies before its first render. Source generation is what makes
/// the contract survive trimming, so every type serialized or deserialized has to be declared here, including
/// the list forms actually sent over the wire.
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(PlayerStatus))]
[JsonSerializable(typeof(NowPlaying))]
[JsonSerializable(typeof(QueueEntry))]
[JsonSerializable(typeof(List<QueueEntry>))]
[JsonSerializable(typeof(PlaylistSummary))]
[JsonSerializable(typeof(List<PlaylistSummary>))]
[JsonSerializable(typeof(LibraryTrack))]
[JsonSerializable(typeof(List<LibraryTrack>))]
[JsonSerializable(typeof(SurprisePick))]
public sealed partial class RokJsonContext : JsonSerializerContext;