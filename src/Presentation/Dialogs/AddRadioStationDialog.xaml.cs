using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;
using CleanArch.DevKit.Mediator.Validation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rok.Application.Dto;
using Rok.Application.Errors;
using Rok.Application.Features.Radios.Requests;

namespace Rok.Dialogs;

public sealed partial class AddRadioStationDialog : ContentDialog
{
    private readonly IMediator _mediator;
    private readonly ResourceLoader _resourceLoader;
    private readonly RadioStationDto? _existing;

    public AddRadioStationDialog(IMediator mediator, RadioStationDto? existing = null)
    {
        _mediator = mediator;
        _resourceLoader = App.ServiceProvider.GetRequiredService<ResourceLoader>();
        _existing = existing;
        InitializeComponent();

        if (existing is not null)
        {
            Title = _resourceLoader.GetString("editRadioDialog.Title");
            PrimaryButtonText = _resourceLoader.GetString("editRadioDialog.PrimaryButtonText");
            NameBox.Text = existing.Name;
            UrlBox.Text = existing.StreamUrl;
            HomepageBox.Text = existing.HomepageUrl ?? string.Empty;
        }
    }

    public bool Saved { get; private set; }

    private async void OnSaveClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ContentDialogButtonClickDeferral deferral = args.GetDeferral();

        try
        {
            string name = NameBox.Text.Trim();
            string streamUrl = UrlBox.Text.Trim();
            string? homepageUrl = string.IsNullOrWhiteSpace(HomepageBox.Text) ? null : HomepageBox.Text.Trim();

            Result result = _existing is null
                ? await SendAddAsync(name, streamUrl, homepageUrl)
                : await SendUpdateAsync(_existing.Id, name, streamUrl, homepageUrl);

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

    private async Task<Result> SendAddAsync(string name, string streamUrl, string? homepageUrl)
    {
        AddRadioStationRequest request = new()
        {
            Name = name,
            StreamUrl = streamUrl,
            HomepageUrl = homepageUrl
        };
        Result<long> addResult = await _mediator.Send(request);
        return addResult.IsSuccess ? Result.Ok() : Result.Fail(addResult.Errors);
    }

    private async Task<Result> SendUpdateAsync(long id, string name, string streamUrl, string? homepageUrl)
    {
        UpdateRadioStationRequest request = new()
        {
            Id = id,
            Name = name,
            StreamUrl = streamUrl,
            HomepageUrl = homepageUrl
        };
        Result<bool> updateResult = await _mediator.Send(request);
        return updateResult.IsSuccess ? Result.Ok() : Result.Fail(updateResult.Errors);
    }
}