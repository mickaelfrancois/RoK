using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Rok.Application.Dto;
using Rok.ViewModels.Radio;
using Windows.System;

namespace Rok.Dialogs;

public sealed partial class SearchRadioStationsDialog : ContentDialog
{
    public SearchRadioStationsViewModel ViewModel { get; }

    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _feedbackTimer;

    public SearchRadioStationsDialog(SearchRadioStationsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        _feedbackTimer = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().CreateTimer();
        _feedbackTimer.Interval = TimeSpan.FromSeconds(2.5);
        _feedbackTimer.IsRepeating = false;
        _feedbackTimer.Tick += (_, _) => ViewModel.ClearFeedback();

        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.FeedbackMessage) && ViewModel.HasFeedback)
            {
                if (ViewModel.FeedbackKind != SearchFeedbackKind.Error)
                {
                    _feedbackTimer.Stop();
                    _feedbackTimer.Start();
                }

                FeedbackBar.Severity = ViewModel.FeedbackKind switch
                {
                    SearchFeedbackKind.Success => InfoBarSeverity.Success,
                    SearchFeedbackKind.Info => InfoBarSeverity.Informational,
                    SearchFeedbackKind.Error => InfoBarSeverity.Error,
                    _ => InfoBarSeverity.Informational
                };
            }
        };
    }

    private void OnQueryKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.SearchCommand.CanExecute(null))
        {
            _ = ViewModel.SearchCommand.ExecuteAsync(null);
            e.Handled = true;
        }
    }

    private void OnResultItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is RadioSearchResultDto r)
            _ = ViewModel.PlayCommand.ExecuteAsync(r);
    }

    private void OnPlayButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: RadioSearchResultDto r })
            _ = ViewModel.PlayCommand.ExecuteAsync(r);
    }

    private void OnAddFavoriteButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: RadioSearchResultDto r })
            _ = ViewModel.AddToFavoritesCommand.ExecuteAsync(r);
    }

    private void OnFeedbackCloseClick(InfoBar sender, object args) =>
        ViewModel.ClearFeedback();
}