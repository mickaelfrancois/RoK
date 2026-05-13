namespace Rok.Application.Tag;

public interface ITagService
{
    void FillProperties(string file, TrackFile track);

    void FillBasicProperties(string file, TrackFile track);

    void FillMusicProperties(string file, TrackFile track);

}
