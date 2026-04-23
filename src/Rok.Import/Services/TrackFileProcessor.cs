using Microsoft.Extensions.Logging;
using Rok.Application.Tag;

namespace Rok.Import.Services;

public class TrackFileProcessor(ITagService _tagService, ILogger<TrackFileProcessor> _logger)
{
    public void ReadMusicProperties(List<TrackFile> files)
    {
        foreach (TrackFile file in files)
        {
            try
            {
                _tagService.FillMusicProperties(file.FullPath, file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An exception occurred while reading music properties '{File}'", file.FullPath);
            }
        }
    }

    public void DetectCompilations(List<TrackFile> files)
    {
        IEnumerable<IGrouping<string, TrackFile>> albumGroups = files.GroupBy(
            file => file.Album,
            StringComparer.OrdinalIgnoreCase);

        foreach (IGrouping<string, TrackFile> albumGroup in albumGroups)
        {
            bool isCompilation = albumGroup
                .Select(file => file.Artist)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() > 1;

            if (isCompilation)
                _logger.LogInformation("Album '{Album}' is marked as a compilation.", albumGroup.Key);

            foreach (TrackFile file in albumGroup)
            {
                file.IsCompilation = isCompilation;
            }
        }
    }
}