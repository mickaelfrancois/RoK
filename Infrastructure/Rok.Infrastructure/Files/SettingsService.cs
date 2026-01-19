using System.ComponentModel;
using Rok.Application.Interfaces;
using Rok.Application.Options;

namespace Rok.Infrastructure.Files;

public class SettingsService(ISettingsFile settingsFile) : ISettingsService, IDisposable
{
    private readonly AppOptions _current = new();
    public AppOptions Current => _current;

    private bool _disposed;


    public async Task InitializeAsync()
    {
        IAppOptions? loaded = await settingsFile.LoadAsync<AppOptions>();
        if (loaded is AppOptions options)
        {
            _current.CopyFrom(options);
        }

        _current.PropertyChanged += OnOptionsChanged;
    }

    private async void OnOptionsChanged(object? sender, PropertyChangedEventArgs e)
    {
        await settingsFile.SaveAsync(_current);
    }


    #region IDispose

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _current.PropertyChanged -= OnOptionsChanged;
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}