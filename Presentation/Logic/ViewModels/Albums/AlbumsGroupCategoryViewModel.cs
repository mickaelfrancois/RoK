﻿namespace Rok.Logic.ViewModels.Albums;

public class AlbumsGroupCategoryViewModel
{
    public string Title { get; set; } = string.Empty;

    public List<AlbumViewModel> Items { get; set; } = [];

    public override string ToString() => Title;
}
