namespace Rok.ViewModels.Start;

public class AlbumImportedModel
{
    public string Name { get; set; } = string.Empty;

    public string ArtistName { get; set; } = string.Empty;

    public string AlbumPath { get; set; } = string.Empty;

    public BitmapImage? Picture { get; set; }
}