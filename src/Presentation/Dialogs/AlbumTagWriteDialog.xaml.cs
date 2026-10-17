using System.Globalization;
using Microsoft.UI.Xaml.Controls;
using Rok.Application.Features.Artists.Rename;

namespace Rok.Dialogs;

/// <summary>Dialog that confirms the write of the tags of an album, shows its progress and its result.</summary>
public sealed partial class AlbumTagWriteDialog : ContentDialog
{
    private readonly ResourceLoader _resourceLoader;
    private bool _isRunning;

    /// <summary>Creates the dialog in its confirmation state.</summary>
    /// <param name="prompt">What the write would do.</param>
    /// <param name="resourceLoader">Gives the texts.</param>
    public AlbumTagWriteDialog(AlbumTagWritePrompt prompt, ResourceLoader resourceLoader)
    {
        _resourceLoader = resourceLoader;

        this.InitializeComponent();

        Closing += OnClosing;

        ChangeList.ItemsSource = prompt.Changes;
        NoteList.ItemsSource = prompt.Notes;

        ShowDetails(AlbumEditTextKeys.BlockersHeader, prompt.Blockers);
        PrimaryButtonText = Localize(prompt.IsResume ? AlbumEditTextKeys.ResumeButton : AlbumEditTextKeys.StartButton);
        IsPrimaryButtonEnabled = prompt.CanStart;
    }

    /// <summary>Switches between the waiting state and the running state.</summary>
    /// <param name="isRunning">Whether the write is running.</param>
    public void SetRunning(bool isRunning)
    {
        _isRunning = isRunning;

        ProgressPanel.Visibility = isRunning ? Visibility.Visible : Visibility.Collapsed;
        IsPrimaryButtonEnabled = !isRunning;

        if (!isRunning)
            return;

        ErrorBar.IsOpen = false;
        WriteProgress.IsIndeterminate = true;
        ProgressText.Text = string.Empty;
    }

    /// <summary>Shows how many files are written.</summary>
    /// <param name="progress">The progress of the write.</param>
    public void ReportProgress(ArtistRenameProgress progress)
    {
        WriteProgress.IsIndeterminate = false;
        WriteProgress.Maximum = Math.Max(progress.Total, 1);
        WriteProgress.Value = progress.Done;
        ProgressText.Text = string.Create(CultureInfo.CurrentCulture, $"{progress.Done} / {progress.Total}");
    }

    /// <summary>Shows the result of a write that did not complete and lets the user try again.</summary>
    /// <param name="attempt">The attempt.</param>
    public void ShowAttempt(AlbumTagWriteAttempt attempt)
    {
        PrimaryButtonText = Localize(AlbumEditTextKeys.RetryButton);
        IsPrimaryButtonEnabled = true;

        if (attempt.Outcome is null)
        {
            ShowError(attempt.Error ?? Localize(AlbumEditTextKeys.GenericError));
            return;
        }

        switch (attempt.Outcome.State)
        {
            case EArtistRenameState.Partial:
                ShowDetails(AlbumEditTextKeys.FailuresHeader, ArtistRenameTexts.FailureLines(attempt.Outcome.Failures, Localize));
                break;

            case EArtistRenameState.Blocked:
                ShowDetails(AlbumEditTextKeys.BlockersHeader, ArtistRenameTexts.BlockerLines(attempt.Outcome.Blockers, Localize));
                break;

            default:
                ShowError(Localize(AlbumEditTextKeys.GenericError));
                break;
        }
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