using CleanArch.DevKit.Mediator.Results;

namespace Rok.Application.Features.Radios.Services;

public interface IRadioStreamUrlResolver
{
    Task<Result<string>> ResolveAsync(string url, CancellationToken cancellationToken);
}