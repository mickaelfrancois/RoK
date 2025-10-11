namespace Rok.Application.Dto.NovaApi;

public class ApiArtistModel
{
    public enum EState { Draft, ServiceUpdate, Valid }

    public int? ID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RealName { get; set; } = string.Empty;
    public string Biography { get; set; } = string.Empty;
    public string BiographyFR { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string Wikipedia { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public int State { get; set; }
    public int RequestCount { get; set; }
    public DateTime CreatDate { get; set; }
    public DateTime? EditDate { get; set; }
    public DateTime? CheckDate { get; set; }
    public string PictureUrl { get; set; } = string.Empty;
    public string Facebook { get; set; } = string.Empty;
    public string Twitter { get; set; } = string.Empty;
    public string FanartUrl { get; set; } = string.Empty;
    public string Fanart2Url { get; set; } = string.Empty;
    public string Fanart3Url { get; set; } = string.Empty;
    public string BannerUrl { get; set; } = string.Empty;
    public string LogoUrl { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string MusicBrainzID { get; set; } = string.Empty;
    public int? FormedYear { get; set; }
    public int? BornYear { get; set; }
    public int? DiedYear { get; set; }
    public string Disbanded { get; set; } = string.Empty;
    public string Style { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string Mood { get; set; } = string.Empty;
    public int FanartsCount { get; set; } = 0;
    public int PicturesCount { get; set; } = 0;
    public int LogosCount { get; set; } = 0;
    public int BannersCount { get; set; } = 0;
    public bool IsDisbanded { get { return Disbanded == "Disbanded"; } }

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
