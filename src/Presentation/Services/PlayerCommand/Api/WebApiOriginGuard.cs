using System.Net;
using System.Net.Sockets;

namespace Rok.Services.PlayerCommand.Api;

/// <summary>
/// Decides which callers may reach the web API, from the headers a browser attaches on its own.
/// </summary>
/// <remarks>
/// This is not authentication — the API deliberately has none. It closes a different hole: any page the user
/// happens to visit can POST a plain HTML form, or load an image, pointing at the API. Such a request needs no
/// preflight, so the response stays opaque to the attacker while the side effect still happens. Requiring a
/// local origin on state-changing routes blocks that without asking the user for anything, and it holds even
/// when the API is listening on loopback only, since the browser reaches localhost from the host machine.
/// </remarks>
public static class WebApiOriginGuard
{
    private static readonly string[] MutatingLegacyRoutes =
        ["/play", "/pause", "/toggle", "/next", "/previous", "/mute"];

    private static readonly string[] MutatingLegacyPrefixes =
        ["/volume/", "/listen/"];

    /// <summary>
    /// Tells whether a request would change the player state, and therefore has to prove it is not cross-site.
    /// Every non-GET route mutates; among the GET routes only the legacy commands kept for the terminal
    /// companion do, which is precisely why they need the check too.
    /// </summary>
    public static bool IsStateChanging(string method, string path)
    {
        if (!string.Equals(method, "GET", StringComparison.Ordinal))
            return true;

        return MutatingLegacyRoutes.Contains(path, StringComparer.Ordinal)
            || MutatingLegacyPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.Ordinal));
    }


    /// <summary>
    /// Tells whether the request was triggered by another site.
    /// </summary>
    /// <param name="origin">The <c>Origin</c> header, which browsers always attach to a non-GET request.</param>
    /// <param name="secFetchSite">The <c>Sec-Fetch-Site</c> header, which covers the GET routes where no origin is sent.</param>
    /// <remarks>
    /// An absent origin is treated as trustworthy on purpose: a cross-site browser request always carries one,
    /// while a script driving the API through curl or PowerShell never does. Refusing those would break the
    /// terminal companion for no security gain.
    /// </remarks>
    public static bool IsCrossSite(string? origin, string? secFetchSite)
    {
        if (!string.IsNullOrEmpty(origin))
            return !IsLocalOrigin(origin);

        return string.Equals(secFetchSite, "cross-site", StringComparison.OrdinalIgnoreCase);
    }


    /// <summary>
    /// Tells whether an origin belongs to this machine or to a private network.
    /// </summary>
    public static bool IsLocalOrigin(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri))
            return false;

        if (uri.IsLoopback)
            return true;

        return IPAddress.TryParse(uri.Host, out IPAddress? address) && IsPrivateAddress(address);
    }


    private static bool IsPrivateAddress(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork)
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal;

        byte[] bytes = address.GetAddressBytes();

        return bytes[0] switch
        {
            10 => true,
            172 => bytes[1] is >= 16 and <= 31,
            192 => bytes[1] == 168,
            169 => bytes[1] == 254,
            _ => false
        };
    }
}