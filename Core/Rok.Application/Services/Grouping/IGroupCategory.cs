namespace Rok.Application.Services.Grouping;

public interface IGroupCategory<T>
{
    string Title { get; set; }

    List<T> Items { get; set; }
}
