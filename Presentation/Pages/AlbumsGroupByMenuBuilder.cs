using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using Rok.Logic.ViewModels.Albums;

namespace Rok.Pages;

internal class AlbumsGroupByMenuBuilder
{
    private readonly ResourceManager _manager = new();

    private static readonly string[] GroupByOptions =
    [
        "CREATDATE",
        "YEAR",
        "DECADE",
        "COUNTRY",
        "ARTISTNAME",
        "ALBUMNAME",
        "LASTLISTEN",
        "LISTENCOUNT",
    ];


    public void PopulateGroupByMenu(MenuFlyout menu, AlbumsViewModel viewModel)
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
