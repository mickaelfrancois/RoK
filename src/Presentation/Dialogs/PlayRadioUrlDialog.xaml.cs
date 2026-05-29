using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rok.Application.Features.Radios.Requests;

namespace Rok.Dialogs;

public sealed partial class PlayRadioUrlDialog : ContentDialog
{
    private readonly IMediator _mediator;
    private readonly ResourceLoader _resourceLoader;

    public PlayRadioUrlDialog(IMediator mediator)
    {
        _mediator = mediator;
        _resourceLoader = App.ServiceProvider.GetRequiredService<ResourceLoader>();
        InitializeComponent();
    }

    public bool Played { get; private set; }

    private async void OnPlayClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ContentDialogButtonClickDeferral deferral = args.GetDeferral();

        try
        {
            Result<bool> result = await _mediator.Send(new PlayRadioUrlRequest { Url = UrlBox.Text.Trim() });

            if (result.IsSuccess)
            {
                Played = true;
                return;
            }

            string code = result.Errors.FirstOrDefault()?.Code ?? "unknown";
            ErrorText.Text = code switch
            {
                "radio.hls_unsupported" => _resourceLoader.GetString("radioErrorHlsUnsupported"),
                "radio.no_stream_in_playlist" => _resourceLoader.GetString("radioErrorNoStreamInPlaylist"),
                "radio.fetch_failed" => _resourceLoader.GetString("radioErrorFetchFailed"),
                "radio.unsupported_format" => _resourceLoader.GetString("radioErrorUnsupportedFormat"),
                "radio.invalid_url" => _resourceLoader.GetString("radioErrorInvalidUrl"),
                _ => result.Errors.FirstOrDefault()?.Message ?? _resourceLoader.GetString("radioErrorUnknown")
            };

            ErrorText.Visibility = Visibility.Visible;
            args.Cancel = true;
        }
        finally
        {
            deferral.Complete();
        }
    }
}