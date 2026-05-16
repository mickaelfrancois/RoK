using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace Rok.Commons;

public sealed partial class ReviewPromptControl : UserControl
{
    private TaskCompletionSource<bool?>? _tcs;

    public ReviewPromptControl()
    {
        this.InitializeComponent();
    }

    /// <summary>
    /// Shows the dialog and waits for a user interaction.
    /// Returns true (primary), false (secondary), or null (dismissed).
    /// </summary>
    public Task<bool?> ShowAsync(string icon, string title, string body, string primaryText, string secondaryText)
    {
        IconGlyph.Glyph = icon;
        TitleBlock.Text = title;
        BodyBlock.Text = body;
        PrimaryButton.Content = primaryText;
        SecondaryButton.Content = secondaryText;

        this.Visibility = Visibility.Visible;
        ((Storyboard)Resources["ShowStoryboard"]).Begin();

        _tcs = new TaskCompletionSource<bool?>();
        return _tcs.Task;
    }

    private async Task HideAsync()
    {
        Storyboard hide = (Storyboard)Resources["HideStoryboard"];
        TaskCompletionSource<object?> animCompleted = new();
        hide.Completed += (_, _) => animCompleted.TrySetResult(null);
        hide.Begin();
        await animCompleted.Task;
        this.Visibility = Visibility.Collapsed;
    }

    private async void PrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        TaskCompletionSource<bool?> tcs = _tcs!;
        await HideAsync();
        tcs.TrySetResult(true);
    }

    private async void SecondaryButton_Click(object sender, RoutedEventArgs e)
    {
        TaskCompletionSource<bool?> tcs = _tcs!;
        await HideAsync();
        tcs.TrySetResult(false);
    }
}