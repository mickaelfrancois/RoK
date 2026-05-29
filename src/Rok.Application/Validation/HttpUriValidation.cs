namespace Rok.Application.Validation;

/// <summary>
/// Shared predicates for validating user-supplied URL strings as absolute http(s) URIs.
/// Used by request validators and by Presentation converters that bind to URL strings.
/// </summary>
public static class HttpUriValidation
{
    public static bool IsAbsoluteHttpUri(string? value) =>
        value is not null &&
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public static bool IsAbsoluteHttpUriOrNull(string? value) =>
        string.IsNullOrEmpty(value) || IsAbsoluteHttpUri(value);
}
