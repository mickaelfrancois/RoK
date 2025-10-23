using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using Rok.Logic.ViewModels.Tracks;

namespace Rok.Pages;

internal class TracksGroupByMenuBuilder
{
    private readonly ResourceManager _manager = new();

    private static readonly string[] GroupByOptions =
    [
        "CREATDATE",
        "TITLE",
        "YEAR",
        "DECADE",
        "COUNTRY",
        "ARTISTNAME",
        "ALBUMNAME",
        "GENRENAME",
        "LASTLISTEN",
        "LISTENCOUNT",
    ];


    public void PopulateGroupByMenu(MenuFlyout menu, TracksViewModel viewModel)
    {
        menu.Items.Clear();

        foreach (string option in GroupByOptions)
        {
            ToggleMenuFlyoutItem menuItem = new()
            {
                Text = _manager.MainResourceMap.GetValue("Resources/groupBy" + option.ToLower()).ValueAsString,
                IsChecked = viewModel.GroupById == option,
                Command = viewModel.GroupByCommand,
                CommandParameter = option
            };

            menu.Items.Add(menuItem);
        }
    }
}
