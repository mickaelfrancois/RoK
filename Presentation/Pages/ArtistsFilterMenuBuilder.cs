using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using Rok.ViewModels.Artists;

namespace Rok.Pages;

internal class ArtistsFilterMenuBuilder
{
    private readonly ResourceManager _manager = new();

    private static readonly string[] FavoriteFilterOptions =
    [
        "ARTISTFAVORITE",
        "GENREFAVORITE",
    ];


    public void PopulateFilterMenu(MenuFlyout menu, ArtistsViewModel viewModel)
    {
        menu.Items.Clear();

        CreateAllMenuItem(menu, viewModel);
        PopulateFavoriteSubMenu(menu, viewModel);

        if (viewModel?.Genres?.Count > 0)
            PopulateGenreSubMenu(menu, viewModel);

        if (viewModel?.Tags?.Count > 0)
            PopulateTagSubMenu(menu, viewModel);
    }


    private void CreateAllMenuItem(MenuFlyout menu, ArtistsViewModel viewModel)
    {
        MenuFlyoutItem menuAll = new()
        {
            Text = _manager.MainResourceMap.GetValue("Resources/filterall").ValueAsString,
            Command = viewModel.FilterByCommand,
        };
        menu.Items.Add(menuAll);
    }


    private void PopulateGenreSubMenu(MenuFlyout menu, ArtistsViewModel viewModel)
    {
        string check = viewModel.SelectedGenreFilters.Count > 0 ? "\u2713 " : string.Empty;

        MenuFlyoutSubItem genreSubItem = new()
        {
            Text = check + _manager.MainResourceMap.GetValue("Resources/filtergenre").ValueAsString,
        };
        menu.Items.Add(genreSubItem);

        MenuFlyoutItem genreAllMenu = new()
        {
            Text = _manager.MainResourceMap.GetValue("Resources/filtergenreall").ValueAsString,
            Command = viewModel.FilterByCommand,
        };
        genreSubItem.Items.Add(genreAllMenu);


        foreach (GenreDto genre in viewModel.Genres)
        {
            ToggleMenuFlyoutItem menuItem = new()
            {
                Text = genre.IsFavorite ? $"{genre.Name} \u2665" : genre.Name,
                IsChecked = viewModel.SelectedGenreFilters.Contains(genre.Id),
                Command = viewModel.FilterByGenreCommand,
                CommandParameter = genre.Id,
            };

            genreSubItem.Items.Add(menuItem);
        }
    }


    private void PopulateFavoriteSubMenu(MenuFlyout menu, ArtistsViewModel viewModel)
    {
        bool hasAny = FavoriteFilterOptions.Any(opt => viewModel.SelectedFilters.Contains(opt));
        string check = hasAny ? "\u2713 " : string.Empty;

        MenuFlyoutSubItem subItem = new()
        {
            Text = check + _manager.MainResourceMap.GetValue("Resources/filterfavorite").ValueAsString,
        };
        menu.Items.Add(subItem);

        foreach (string option in FavoriteFilterOptions)
        {
            ToggleMenuFlyoutItem menuItem = new()
            {
                Text = _manager.MainResourceMap.GetValue("Resources/filter" + option.ToLower()).ValueAsString,
                IsChecked = viewModel.SelectedFilters.Contains(option),
                Command = viewModel.FilterByCommand,
                CommandParameter = option
            };

            subItem.Items.Add(menuItem);
        }
    }

    private void PopulateTagSubMenu(MenuFlyout menu, ArtistsViewModel viewModel)
    {
        string check = viewModel.SelectedTagFilters.Count > 0 ? "\u2713 " : string.Empty;

        MenuFlyoutSubItem tagSubItem = new()
        {
            Text = check + "Tags",
        };
        menu.Items.Add(tagSubItem);

        MenuFlyoutItem tagAllMenu = new()
        {
            Text = _manager.MainResourceMap.GetValue("Resources/filterall").ValueAsString,
            Command = viewModel.FilterByTagCommand,
        };
        tagSubItem.Items.Add(tagAllMenu);

        foreach (string tag in viewModel.Tags)
        {
            ToggleMenuFlyoutItem menuItem = new()
            {
                Text = tag,
                IsChecked = viewModel.SelectedTagFilters.Contains(tag),
                Command = viewModel.FilterByTagCommand,
                CommandParameter = tag,
            };

            tagSubItem.Items.Add(menuItem);
        }
    }
}