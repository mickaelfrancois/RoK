using System.Globalization;
using Microsoft.UI.Xaml.Controls;
using Rok.Application.Features.Albums.Merge;
using Rok.Application.Features.Artists.Rename;

namespace Rok.Dialogs;

/// <summary>Dialog that merges an album into another one: the choice, the preview, the progress and the result.</summary>
public sealed partial class MergeAlbumDialog : ContentDialog
{
    private readonly ResourceLoader _resourceLoader;
    private readonly AlbumMergeDialogActions _actions;
    private readonly AlbumMergeChoice _first;

    private AlbumMergeChoice? _second;
    private AlbumMergePreview? _preview;
    private bool _keepFirst;
    private bool _isRunning;
    private bool _isLocked;
    private bool _isUpdatingChoice;
    private int _previewVersion;

    /// <summary>Creates the dialog in its choice state.</summary>
    /// <param name="request">What the dialog starts with.</param>
    /// <param name="actions">What the dialog calls to preview, run and search.</param>
    /// <param name="resourceLoader">Gives the texts.</param>
    public MergeAlbumDialog(AlbumMergeDialogRequest request, AlbumMergeDialogActions actions, ResourceLoader resourceLoader)
    {
        _resourceLoader = resourceLoader;
        _actions = actions;
        _first = request.First;
        _second = request.Second;
        _keepFirst = request.KeepFirst;

        this.InitializeComponent();

        Closing += OnClosing;
        Opened += OnOpened;

        PrimaryButtonText = Localize(AlbumMergeTextKeys.StartButton);
        CloseButtonText = Localize(AlbumMergeTextKeys.CancelButton);
        IsPrimaryButtonEnabled = false;

        if (!string.IsNullOrEmpty(request.Intro))
        {
            IntroText.Text = request.Intro;
            IntroText.Visibility = Visibility.Visible;
        }

        if (_second is null)
        {
            OtherBox.PlaceholderText = Localize(AlbumMergeTextKeys.OtherPlaceholder);
            OtherBox.Visibility = Visibility.Visible;
        }

        KeepHeader.Text = Localize(AlbumMergeTextKeys.KeepHeader);
        KeepFirstButton.Content = _first.Label;
        ShowKeepChoice();
    }

    /// <summary>Gets a value indicating whether the user asked to move the discs of the merged album after those of the kept one.</summary>
    public bool ShiftDiscs => ShiftBox.Visibility == Visibility.Visible && ShiftBox.IsChecked == true;

    private AlbumMergeChoice? Target => _keepFirst ? _first : _second;

    private AlbumMergeChoice? Source => _keepFirst ? _second : _first;

    /// <summary>Switches between the waiting state and the running state.</summary>
    /// <param name="isRunning">Whether the merge is running.</param>
    public void SetRunning(bool isRunning)
    {
        _isRunning = isRunning;

        ProgressPanel.Visibility = isRunning ? Visibility.Visible : Visibility.Collapsed;
        IsPrimaryButtonEnabled = !isRunning;
        OtherBox.IsEnabled = !isRunning && !_isLocked;
        ShiftBox.IsEnabled = !isRunning && !_isLocked;
        SetKeepEnabled(!isRunning && !_isLocked);

        if (!isRunning)
            return;

        ErrorBar.IsOpen = false;
        MergeProgress.IsIndeterminate = true;
        ProgressText.Text = string.Empty;
    }

    /// <summary>Shows how many files are written.</summary>
    /// <param name="progress">The progress of the merge.</param>
    public void ReportProgress(ArtistRenameProgress progress)
    {
        MergeProgress.IsIndeterminate = false;
        MergeProgress.Maximum = Math.Max(progress.Total, 1);
        MergeProgress.Value = progress.Done;
        ProgressText.Text = string.Create(CultureInfo.CurrentCulture, $"{progress.Done} / {progress.Total}");
    }

    /// <summary>Shows the result of a merge that did not complete and lets the user try again.</summary>
    /// <param name="attempt">The attempt.</param>
    public void ShowAttempt(AlbumTagWriteAttempt attempt)
    {
        CloseButtonText = Localize(AlbumMergeTextKeys.CloseButton);
        IsPrimaryButtonEnabled = true;

        if (attempt.Outcome is null)
        {
            ShowError(attempt.Error ?? Localize(AlbumMergeTextKeys.GenericError));
            return;
        }

        switch (attempt.Outcome.State)
        {
            case EArtistRenameState.Partial:
                LockChoice();
                PrimaryButtonText = Localize(AlbumMergeTextKeys.ResumeButton);
                ShowSection(AlbumMergeTextKeys.FailuresHeader, ArtistRenameTexts.FailureLines(attempt.Outcome.Failures, Localize), clear: true);
                break;

            case EArtistRenameState.Blocked:
                ShowSection(AlbumMergeTextKeys.BlockersHeader, ArtistRenameTexts.BlockerLines(attempt.Outcome.Blockers, Localize), clear: true);
                break;

            default:
                ShowError(Localize(AlbumMergeTextKeys.GenericError));
                break;
        }
    }

    /// <summary>Gives the albums the next merge uses.</summary>
    /// <returns>The merged album and the kept album, null when the user has not chosen the other album yet.</returns>
    public (long SourceId, long TargetId)? CurrentPair()
    {
        AlbumMergeChoice? source = Source;
        AlbumMergeChoice? target = Target;

        return source is null || target is null ? null : (source.Id, target.Id);
    }

    private void LockChoice()
    {
        _isLocked = true;
        OtherBox.IsEnabled = false;
        ShiftBox.IsEnabled = false;
        SetKeepEnabled(false);
    }

    private void SetKeepEnabled(bool isEnabled)
    {
        KeepFirstButton.IsEnabled = isEnabled;
        KeepSecondButton.IsEnabled = isEnabled;
    }

    private void ShowKeepChoice()
    {
        if (_second is null)
        {
            KeepPanel.Visibility = Visibility.Collapsed;
            return;
        }

        _isUpdatingChoice = true;

        KeepSecondButton.Content = _second.Label;
        KeepFirstButton.IsChecked = _keepFirst;
        KeepSecondButton.IsChecked = !_keepFirst;
        KeepPanel.Visibility = Visibility.Visible;

        _isUpdatingChoice = false;
    }

    private async void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        if (_second is not null)
            await RefreshPreviewAsync();
    }

    private async void OtherBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
            return;

        IReadOnlyList<AlbumMergeChoice> choices = sender.Text.Trim().Length == 0
            ? []
            : await _actions.SearchAsync(sender.Text);

        sender.ItemsSource = choices;
    }

    private async void OtherBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is not AlbumMergeChoice choice)
            return;

        _second = choice;
        ShowKeepChoice();
        await RefreshPreviewAsync();
    }

    private async void KeepButton_Checked(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingChoice || _second is null)
            return;

        bool keepFirst = ReferenceEquals(sender, KeepFirstButton);

        if (keepFirst == _keepFirst)
            return;

        _keepFirst = keepFirst;
        await RefreshPreviewAsync();
    }

    private void ShiftBox_Changed(object sender, RoutedEventArgs e)
    {
        UpdatePrimaryButton();
    }

    private async Task RefreshPreviewAsync()
    {
        (long SourceId, long TargetId)? pair = CurrentPair();

        _preview = null;
        ErrorBar.IsOpen = false;
        ClearDetails();
        UpdatePrimaryButton();

        if (pair is null)
            return;

        int version = ++_previewVersion;
        AlbumMergePreviewAttempt attempt = await _actions.PreviewAsync(pair.Value.SourceId, pair.Value.TargetId);

        if (version != _previewVersion)
            return;

        if (attempt.Preview is null)
        {
            ShowError(attempt.Error ?? Localize(AlbumMergeTextKeys.GenericError));
            return;
        }

        _preview = attempt.Preview;
        ShowPreview(attempt.Preview);
        UpdatePrimaryButton();
    }

    private void UpdatePrimaryButton()
    {
        if (_preview is null)
        {
            IsPrimaryButtonEnabled = false;
            return;
        }

        bool needsShift = _preview.DiscConflicts.Count > 0;

        IsPrimaryButtonEnabled = _preview.Blockers.Count == 0 && (!needsShift || ShiftBox.IsChecked == true);
    }

    private void ShowPreview(AlbumMergePreview preview)
    {
        SummaryText.Text = AlbumMergeTexts.Summary(preview, Localize);
        CountsText.Text = AlbumMergeTexts.Counts(preview, Localize);

        ShowSection(AlbumMergeTextKeys.BlockersHeader, ArtistRenameTexts.BlockerLines(preview.Blockers, Localize), clear: true);
        ShowSection(AlbumMergeTextKeys.DiscConflictsHeader, AlbumMergeTexts.DiscConflictLines(preview, Localize), clear: false);
        ShowSection(AlbumMergeTextKeys.ConflictsHeader, AlbumMergeTexts.ConflictLines(preview, Localize), clear: false);

        foreach (string note in AlbumMergeTexts.NoteLines(preview, Localize))
            AddLine(note, isHeader: false);

        if (preview.DiscConflicts.Count > 0)
        {
            ShiftBox.Content = AlbumMergeTexts.ShiftDiscs(preview, Localize);
            ShiftBox.Visibility = Visibility.Visible;
        }
    }

    private void ClearDetails()
    {
        SummaryText.Text = string.Empty;
        CountsText.Text = string.Empty;
        DetailPanel.Children.Clear();
        DetailScroll.Visibility = Visibility.Collapsed;
        ShiftBox.Visibility = Visibility.Collapsed;
    }

    private void ShowSection(string headerKey, IReadOnlyList<string> lines, bool clear)
    {
        if (clear)
            DetailPanel.Children.Clear();

        if (lines.Count > 0)
        {
            AddLine(Localize(headerKey), isHeader: true);

            foreach (string line in lines)
                AddLine(line, isHeader: false);
        }

        DetailScroll.Visibility = DetailPanel.Children.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void AddLine(string text, bool isHeader)
    {
        TextBlock block = new()
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
            Margin = new Thickness(0, isHeader ? 8 : 0, 0, 2)
        };

        if (isHeader)
            block.Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["BodyStrongTextBlockStyle"];

        DetailPanel.Children.Add(block);
        DetailScroll.Visibility = Visibility.Visible;
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