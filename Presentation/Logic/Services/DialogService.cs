using Microsoft.UI.Xaml.Controls;
using Windows.UI.Text;

namespace Rok.Logic.Services;


public sealed class DialogService : IDialogService
{
    public async Task ShowTextAsync(string title, string content, string closeButtonText)
    {
        Func<Task> show = async Task () =>
        {
            FrameworkElement? root = App.MainWindow.Content as FrameworkElement;
            ElementTheme theme = root?.ActualTheme ?? ElementTheme.Default;

            ContentDialog dialog = new()
            {
                XamlRoot = root?.XamlRoot ?? App.MainWindow.Content.XamlRoot,
                RequestedTheme = theme,
                Title = title,
                MinWidth = 800,
                Width = 800,
                Content = new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text = content ?? string.Empty,
                        TextWrapping = TextWrapping.Wrap,
                        IsTextSelectionEnabled = true,
                        FontStyle = FontStyle.Normal,
                    },
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    MaxHeight = 500,
                    MinWidth = 800
                },
                CloseButtonText = closeButtonText,
                DefaultButton = ContentDialogButton.Close
            };

            await dialog.ShowAsync();
        };

        if (App.MainWindow.DispatcherQueue.HasThreadAccess)
        {
            await show();
        }
        else
        {
            TaskCompletionSource tcs = new();

            _ = App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    await show();
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            await tcs.Task;
        }
    }
}