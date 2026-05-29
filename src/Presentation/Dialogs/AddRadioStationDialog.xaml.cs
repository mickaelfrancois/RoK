using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;
using CleanArch.DevKit.Mediator.Validation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rok.Application.Errors;
using Rok.Application.Features.Radios.Requests;

namespace Rok.Dialogs;

public sealed partial class AddRadioStationDialog : ContentDialog
{
    private readonly IMediator _mediator;
    private readonly ResourceLoader _resourceLoader;

    public AddRadioStationDialog(IMediator mediator)
    {
        _mediator = mediator;
        _resourceLoader = App.ServiceProvider.GetRequiredService<ResourceLoader>();
        InitializeComponent();
    }

    public bool Saved { get; private set; }

    private async void OnSaveClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ContentDialogButtonClickDeferral deferral = args.GetDeferral();

        try
        {
            AddRadioStationRequest request = new()
            {
                Name = NameBox.Text.Trim(),
                StreamUrl = UrlBox.Text.Trim(),
                HomepageUrl = string.IsNullOrWhiteSpace(HomepageBox.Text) ? null : HomepageBox.Text.Trim()
            };

            Result<long> result = await _mediator.Send(request);

            if (result.IsSuccess)
            {
                Saved = true;
                return;
            }

            string message = result.Errors.FirstOrDefault() switch
            {
                ConflictError => _resourceLoader.GetString("radioErrorDuplicate"),
                ValidationError ve => string.Join('\n', ve.Failures.Select(f => f.Message)),
                Error e => e.Message,
                _ => _resourceLoader.GetString("radioErrorUnknown")
            };

            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
            args.Cancel = true;
        }
        finally
        {
            deferral.Complete();
        }
    }
}