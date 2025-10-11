namespace Rok.Application.Dto.NovaApi;

public class ApiAlbumModel
{
    public enum EState { Draft, ServiceUpdate, Valid }

    public int? ID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RealName { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string ArtistRealName { get; set; } = string.Empty;
    public string Biography { get; set; } = string.Empty;
    public string BiographyFR { get; set; } = string.Empty;

    public bool Compilation { get; set; }
    public string Wikipedia { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public int State { get; set; }
    public int RequestCount { get; set; }
    public DateTime CreatDate { get; set; }
    public DateTime? EditDate { get; set; }
    public DateTime? CheckDate { get; set; }
    public string PictureUrl { get; set; } = string.Empty;
    public string MusicBrainzID { get; set; } = string.Empty;
    public string Year { get; set; } = string.Empty;
    public string ReleaseFormat { get; set; } = string.Empty;
    public string Sales { get; set; } = string.Empty;
    public string Mood { get; set; } = string.Empty;
    public string Theme { get; set; } = string.Empty;
    public string Speed { get; set; } = string.Empty;
    public string Score { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public DateTime? ReleaseDate { get; set; }
    public int CoversCount { get; set; } = 0;

    public string GetBiography(string language)
    {
        string biography;

        if (string.Compare(language, "fr", true) == 0)
            biography = !string.IsNullOrEmpty(BiographyFR) ? BiographyFR : Biography;
        else
            biography = !string.IsNullOrEmpty(Biography) ? Biography : BiographyFR;

        return biography;
    }
}
