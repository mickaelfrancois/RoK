namespace Rok.Services;

/// <summary>
/// Provides the user's country, as configured in Windows.
/// </summary>
public interface IRegionProvider
{
    /// <summary>
    /// Returns the two-letter country code of the Windows "Country or region" setting, or <c>null</c> when unknown.
    /// </summary>
    string? GetCountryCode();
}