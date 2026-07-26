using System.IO;

namespace Rok.Services.PlayerCommand.Api;

/// <summary>
/// Maps an image file path to its HTTP <c>Content-Type</c> based on the file extension.
/// </summary>
public static class ImageContentType
{
    /// <summary>
    /// Returns the MIME content type for the given image file path, or <c>application/octet-stream</c>
    /// when the extension is not a recognized image format.
    /// </summary>
    /// <param name="path">The image file path whose extension determines the content type.</param>
    public static string FromPath(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();

        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}