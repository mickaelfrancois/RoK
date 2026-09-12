using System.Text.Json;

namespace Rok.WebApi.Contracts;

/// <summary>
/// The single serialization contract shared by the Rok web API and its clients.
/// Both sides must use it so that property casing never drifts.
/// </summary>
/// <remarks>
/// The options come from <see cref="RokJsonContext"/> rather than being built by hand: reflection-based
/// options would not survive the trimming applied to the published web companion. Prefer passing the
/// <c>RokJsonContext.Default.&lt;Type&gt;</c> metadata directly, which the compiler can check.
/// </remarks>
public static class RokJson
{
    /// <summary>
    /// Camel-cased, case-insensitive options backed by compile-time metadata, with null members omitted.
    /// </summary>
    public static JsonSerializerOptions Options => RokJsonContext.Default.Options;
}