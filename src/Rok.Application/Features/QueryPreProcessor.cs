using Microsoft.Extensions.Logging;

namespace Rok.Application.Features;

public class QueryPreProcessor<TMessage, TResponse>(ILogger<TMessage> _logger) : IRequestPreProcessor<TMessage, TResponse> where TMessage : IRequest<TResponse>
{
    public async Task<TResponse> RunAsync(TMessage message, HandleRequestDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing message: {Message}", message.GetType().Name);

        try
        {
            return await next.Invoke(message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Processing message error: {Message}", message.GetType().Name);
            return default!;
        }
    }
}