using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

namespace Rok.Logic.Services;

public sealed class DialogService(ResourceLoader resourceLoader, ITranslateService translateService) : IDialogService
{
    public async Task ShowTextAsync(string title, string content, bool showTranslateButton = false, string targetLanguage = "fr")
    {
        string closeButtonText = resourceLoader.GetString("Close");
        string translateButtonText = resourceLoader.GetString("Translate");
        string originalButtonText = resourceLoader.GetString("Original");

        Func<Task> show = async () =>
        {
            FrameworkElement? root = App.MainWindow.Content as FrameworkElement;
            ElementTheme theme = root?.ActualTheme ?? ElementTheme.Default;

            string originalText = content ?? string.Empty;
            string? translatedText = null;
            bool isShowingTranslated = false;
            bool translating = false;

            TextBlock textBlock = CreateMainTextBlock(originalText);
            ScrollViewer scrollViewer = CreateScrollViewer(textBlock);

            Grid contentGrid = new();
            contentGrid.Children.Add(scrollViewer);

            (Grid? overlay, ProgressRing? progressRing) = CreateOverlay(resourceLoader.GetString("Translating") ?? "Translating...");
            contentGrid.Children.Add(overlay);

            ContentDialog dialog = CreateDialog(root, theme, title, contentGrid, closeButtonText);

            contentGrid.MinWidth = dialog.MinWidth;
            contentGrid.MinHeight = dialog.MinHeight;
            scrollViewer.HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch;
            scrollViewer.MinWidth = contentGrid.MinWidth;

            TypedEventHandler<ContentDialog, ContentDialogButtonClickEventArgs>? secondaryHandler = null;

            void ShowOverlay()
            {
                overlay.Visibility = Visibility.Visible;
                progressRing.IsActive = true;
            }

            void HideOverlay()
            {
                progressRing.IsActive = false;
                overlay.Visibility = Visibility.Collapsed;
            }

            if (showTranslateButton)
            {
                string translateLabel = translateButtonText;
                string originalLabel = originalButtonText;

                dialog.SecondaryButtonText = translateLabel;
                dialog.IsSecondaryButtonEnabled = true;

                secondaryHandler = async (ContentDialog sender, ContentDialogButtonClickEventArgs args) =>
                {
                    if (translating)
                    {
                        args.Cancel = true;
                        return;
                    }

                    if (translatedText != null)
                    {
                        args.Cancel = true;

                        if (isShowingTranslated)
                        {
                            RunOnUi(() =>
                            {
                                textBlock.Text = originalText;
                                dialog.SecondaryButtonText = translateLabel;
                            });

                            isShowingTranslated = false;
                            return;
                        }

                        RunOnUi(() =>
                        {
                            textBlock.Text = translatedText!;
                            dialog.SecondaryButtonText = originalLabel;
                        });

                        isShowingTranslated = true;
                        return;
                    }

                    if (!translateService.IsEnable)
                    {
                        args.Cancel = true;
                        return;
                    }

                    translating = true;
                    args.Cancel = true;

                    try
                    {
                        RunOnUi(() =>
                        {
                            ShowOverlay();
                            dialog.IsSecondaryButtonEnabled = false;
                        });

                        string? result = await translateService.TranslateAsync(originalText, targetLanguage).ConfigureAwait(false);

                        if (!string.IsNullOrEmpty(result))
                        {
                            translatedText = result;

                            RunOnUi(() =>
                            {
                                textBlock.Text = translatedText;
                                dialog.SecondaryButtonText = originalLabel;
                            });

                            isShowingTranslated = true;
                        }
                        else
                        {
                            RunOnUi(() => dialog.IsSecondaryButtonEnabled = true);
                        }
                    }
                    catch
                    {
                        RunOnUi(() => dialog.IsSecondaryButtonEnabled = true);
                    }
                    finally
                    {
                        RunOnUi(() =>
                        {
                            HideOverlay();
                            dialog.IsSecondaryButtonEnabled = true;
                        });

                        translating = false;
                    }
                };

                dialog.SecondaryButtonClick += secondaryHandler;
            }

            try
            {
                await dialog.ShowAsync();
            }
            finally
            {
                if (secondaryHandler != null)
                    dialog.SecondaryButtonClick -= secondaryHandler;
            }
        };

        await RunOnUiAsync(show);
    }


    private static TextBlock CreateMainTextBlock(string text)
    {
        return new TextBlock
        {
            Text = text ?? string.Empty,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
            FontStyle = FontStyle.Normal
        };
    }


    private static ScrollViewer CreateScrollViewer(UIElement content)
    {
        return new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 500,
            MinWidth = 760
        };
    }


    private static (Grid overlay, ProgressRing progressRing) CreateOverlay(string translatingLabelText)
    {
        ProgressRing progressRing = new()
        {
            IsActive = false,
            Width = 36,
            Height = 36,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        TextBlock translatingLabel = new()
        {
            Text = translatingLabelText,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Colors.White)
        };

        StackPanel overlayContent = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        overlayContent.Children.Add(progressRing);
        overlayContent.Children.Add(translatingLabel);

        Grid overlay = new()
        {
            Background = new SolidColorBrush(Color.FromArgb(96, 0, 0, 0)),
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = true
        };
        overlay.Children.Add(overlayContent);

        return (overlay, progressRing);
    }


    private static ContentDialog CreateDialog(FrameworkElement? root, ElementTheme theme, string title, object contentGrid, string closeButtonText)
    {
        return new ContentDialog
        {
            XamlRoot = root?.XamlRoot ?? App.MainWindow.Content.XamlRoot,
            RequestedTheme = theme,
            Title = title,
            MinHeight = 300,
            MinWidth = 300,
            Content = contentGrid,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Close
        };
    }


    private static void RunOnUi(Action action)
    {
        if (App.MainWindow.DispatcherQueue.HasThreadAccess)
        {
            action();
            return;
        }

        _ = App.MainWindow.DispatcherQueue.TryEnqueue(() => action());
    }

    private static Task RunOnUiAsync(Func<Task> func)
    {
        if (App.MainWindow.DispatcherQueue.HasThreadAccess)
            return func();

        TaskCompletionSource<object?> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        _ = App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            Task task = func();
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                    tcs.SetException(t.Exception!.Flatten());
                else if (t.IsCanceled)
                    tcs.SetCanceled();
                else
                    tcs.SetResult(null);
            }, TaskScheduler.Default);
        });

        return tcs.Task;
    }
}