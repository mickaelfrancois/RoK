namespace Rok.Application.Services.Grouping;

public interface IGroupable
{
    string? CountryCode { get; }
    DateTime CreatDate { get; }
    DateTime? LastListen { get; }
    int ListenCount { get; }
}
