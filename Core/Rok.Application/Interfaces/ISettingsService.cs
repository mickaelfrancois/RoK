using Rok.Application.Options;

namespace Rok.Application.Interfaces;

public interface ISettingsService
{
    AppOptions Current { get; }
    Task InitializeAsync();
}