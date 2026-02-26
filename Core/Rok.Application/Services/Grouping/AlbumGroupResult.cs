namespace Rok.Application.Services.Grouping;

public class AlbumGroupResult : IGroupCategory<IGroupableAlbum>
{
    public string Title { get; set; } = string.Empty;

    public List<IGroupableAlbum> Items { get; set; } = [];
}
