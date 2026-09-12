using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rok.WebApi.Contracts;

/// <summary>
/// The single serialization contract shared by the Rok web API and its clients.
/// Both sides must use <see cref="Options"/> so that property casing never drifts.
/// </summary>
public static class RokJson
{
    /// <summary>
    /// Camel-cased options with enums written as strings and null members omitted.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };
}