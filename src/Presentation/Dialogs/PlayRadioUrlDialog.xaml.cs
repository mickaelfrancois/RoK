using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rok.Application.Features.Radios.Requests;

namespace Rok.Dialogs;

public sealed partial class PlayRadioUrlDialog : ContentDialog
{
    private readonly IMediator _mediator;

    public PlayRadioUrlDialog(IMediator mediator)
    {
        _mediator = mediator;
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
                "radio.hls_unsupported" => "HLS streams are not supported.",
                "radio.no_stream_in_playlist" => "No usable stream URL found in this playlist.",
                "radio.fetch_failed" => "Cannot reach this URL.",
                "radio.unsupported_format" => "This URL format is not supported.",
                "radio.invalid_url" => "Invalid URL.",
                _ => result.Errors.FirstOrDefault()?.Message ?? "Unknown error."
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
