using System.Threading;
using Rok.Application.Features.Tags.Query;

namespace Rok.Logic.Services;


public partial class TagsProvider : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly IMediator _mediator;
    private List<string> _tags = new();
    private bool _isLoaded;
    private bool _disposed;

    public TagsProvider(IMediator mediator)
    {
        _mediator = mediator;

        Messenger.Subscribe<TagUpdatedMessage>(async _ => await LoadTagsAsync());
    }


    public async Task<List<string>> GetTagsAsync()
    {
        if (!_isLoaded)
            await LoadTagsAsync();

        return _tags;
    }


    private async Task LoadTagsAsync()
    {
        await _semaphore.WaitAsync();

        try
        {
            IEnumerable<TagDto> tags = await _mediator.SendMessageAsync(new GetAllTagsQuery());

            _tags = tags.Select(v => v.Name)
                       .Distinct()
                       .OrderBy(t => t)
                       .ToList();
            _isLoaded = true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _semaphore.Dispose();
            }
            _disposed = true;
        }
    }
}
