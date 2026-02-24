using Rok.Application.Interfaces;
using Windows.ApplicationModel.Resources;

namespace Rok.Infrastructure.Localization;

public class ResourceService(ResourceLoader ResourceLoader) : IResourceService
{
    public string GetString(string resourceKey)
    {
        return ResourceLoader.GetString(resourceKey);
    }
}