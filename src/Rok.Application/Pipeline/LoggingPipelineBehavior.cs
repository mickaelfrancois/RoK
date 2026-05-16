using CleanArch.DevKit.Mediator;
using Microsoft.Extensions.Logging;

namespace Rok.Application.Pipeline;

public sealed class LoggingPipelineBehavior<TRequest, TResponse>(
    ILogger<LoggingPipelineBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing message: {Message}", typeof(TRequest).Name);

        try
        {
            return await next(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Processing message error: {Message}", typeof(TRequest).Name);
            throw;
        }
    }
}
