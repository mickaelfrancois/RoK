using System.Globalization;
using Windows.System.UserProfile;

namespace Rok.Services;

public sealed class WindowsRegionProvider(ILogger<WindowsRegionProvider> logger) : IRegionProvider
{
    public string? GetCountryCode()
    {
        try
        {
            string region = GlobalizationPreferences.HomeGeographicRegion;

            if (!string.IsNullOrWhiteSpace(region))
                return region;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to read the Windows home geographic region.");
        }

        try
        {
            return RegionInfo.CurrentRegion.TwoLetterISORegionName;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}