using System.Globalization;
using Microsoft.UI.Xaml.Controls;
using Rok.Application.Features.Artists.Rename;

namespace Rok.Dialogs;

/// <summary>Dialog that confirms the rename of an artist, shows its progress and its result.</summary>
public sealed partial class RenameArtistDialog : ContentDialog
{
    private readonly ResourceLoader _resourceLoader;
    private bool _isRunning;

    /// <summary>Creates the dialog in its confirmation state.</summary>
    /// <param name="preview">What the rename would do.</param>
    /// <param name="resourceLoader">Gives the texts.</param>
    public RenameArtistDialog(ArtistRenamePreview preview, ResourceLoader resourceLoader)
    {
        _resourceLoader = resourceLoader;

        this.InitializeComponent();

        Closing += OnClosing;

        SummaryText.Text = ArtistRenameTexts.Summary(preview);
        CountsText.Text = ArtistRenameTexts.Counts(preview, Localize);

        ShowBlockers(preview.Blockers);
        PrimaryButtonText = Localize(preview.IsResume ? ArtistRenameTextKeys.ResumeButton : ArtistRenameTextKeys.StartButton);
        IsPrimaryButtonEnabled = ArtistRenameRules.CanStart(preview);
    }

    /// <summary>Switches between the waiting state and the running state.</summary>
    /// <param name="isRunning">Whether the rename is running.</param>
    public void SetRunning(bool isRunning)
    {
        _isRunning = isRunning;

        ProgressPanel.Visibility = isRunning ? Visibility.Visible : Visibility.Collapsed;
        IsPrimaryButtonEnabled = !isRunning;

        if (!isRunning)
            return;

        ErrorBar.IsOpen = false;
        RenameProgress.IsIndeterminate = true;
        ProgressText.Text = string.Empty;
    }

    /// <summary>Shows how many files are written.</summary>
    /// <param name="progress">The progress of the rename.</param>
    public void ReportProgress(ArtistRenameProgress progress)
    {
        RenameProgress.IsIndeterminate = false;
        RenameProgress.Maximum = Math.Max(progress.Total, 1);
        RenameProgress.Value = progress.Done;
        ProgressText.Text = string.Create(CultureInfo.CurrentCulture, $"{progress.Done} / {progress.Total}");
    }

    /// <summary>Shows the result of a rename that did not complete and lets the user try again.</summary>
    /// <param name="attempt">The attempt.</param>
    public void ShowAttempt(ArtistRenameAttempt attempt)
    {
        PrimaryButtonText = Localize(ArtistRenameTextKeys.RetryButton);
        IsPrimaryButtonEnabled = true;

        if (attempt.Outcome is null)
        {
            ShowError(attempt.Error ?? Localize(ArtistRenameTextKeys.GenericError));
            return;
        }

        switch (attempt.Outcome.State)
        {
            case EArtistRenameState.Partial:
                ShowDetails(ArtistRenameTextKeys.FailuresHeader, ArtistRenameTexts.FailureLines(attempt.Outcome.Failures, Localize));
                break;

            case EArtistRenameState.Blocked:
                ShowBlockers(attempt.Outcome.Blockers);
                break;

            default:
                ShowError(Localize(ArtistRenameTextKeys.GenericError));
                break;
        }
    }

    private void ShowBlockers(IReadOnlyList<ArtistRenameBlocker> blockers)
    {
        ShowDetails(ArtistRenameTextKeys.BlockersHeader, ArtistRenameTexts.BlockerLines(blockers, Localize));
    }

    private void ShowDetails(string headerKey, IReadOnlyList<string> lines)
    {
        Visibility visibility = lines.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        DetailHeader.Text = Localize(headerKey);
        DetailList.ItemsSource = lines;
        DetailHeader.Visibility = visibility;
        DetailScroll.Visibility = visibility;
    }

    private void ShowError(string message)
    {
        ErrorBar.Message = message;
        ErrorBar.IsOpen = true;
    }

    private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
    {
        if (_isRunning)
            args.Cancel = true;
    }

    private string Localize(string key) => _resourceLoader.GetString(key);
}