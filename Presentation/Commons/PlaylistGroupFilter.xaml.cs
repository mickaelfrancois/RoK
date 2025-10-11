using Microsoft.UI.Xaml.Controls;
using System.Globalization;

namespace Rok.Commons;

public sealed partial class PlaylistGroupFilter : UserControl
{
    public event EventHandler<EventArgs>? NewFilterClicked;

    public event EventHandler<EventArgs>? RemoveFilterClicked;

    private readonly List<EntityOption> _entities = [];

    private readonly ResourceLoader _resourceLoader;

    private const string DisplayMemberValuePath = "Label";
    private const string SelectedValuePath = "Key";

    private readonly CompareInfo _compareInfo = CultureInfo.CurrentCulture.CompareInfo;


    public PlaylistFilterDto Filter
    {
        get
        {
            return new PlaylistFilterDto()
            {
                Entity = Entity.Key,
                Field = Field.Key,
                FieldType = Field.FieldType,
                Operator = Operator.Key,
                Value = Value,
                Value2 = Value2
            };
        }
        set
        {
            cbEntities.SelectionChanged -= EntitiesSelectionChanged;
            cbFields.SelectionChanged -= FieldsSelectionChanged;
            cbOperators.SelectionChanged -= OperatorsSelectionChanged;

            cbEntities.SelectedItem = ((IEnumerable<EntityOption>)cbEntities.ItemsSource).FirstOrDefault(f => f.Key == value.Entity);

            LoadFieldsList(value.Entity);
            cbFields.SelectedItem = ((IEnumerable<FieldOption>)cbFields.ItemsSource).FirstOrDefault(f => f.Key == value.Field);

            LoadOperatorList(value.Field);
            cbOperators.SelectedItem = ((IEnumerable<OperatorOption>)cbOperators.ItemsSource).FirstOrDefault(f => f.Key == value.Operator);

            Value = value.Value ?? string.Empty;
            Value2 = value.Value2 ?? string.Empty;

            if (value.Operator == SmartPlaylistOperator.Between)
                nbValue2.Visibility = Visibility.Visible;
            else
                nbValue2.Visibility = Visibility.Collapsed;

            cbFields.SelectionChanged += FieldsSelectionChanged;
            cbEntities.SelectionChanged += EntitiesSelectionChanged;
            cbOperators.SelectionChanged += OperatorsSelectionChanged;
        }
    }

    public EntityOption Entity
    {
        get
        {
            return (EntityOption)cbEntities.SelectedItem;
        }
        set
        {
            cbEntities.SelectedItem = value;
        }
    }

    public FieldOption Field
    {
        get
        {
            return (FieldOption)cbFields.SelectedItem;
        }
        set
        {
            cbFields.SelectedItem = value;
        }
    }

    public OperatorOption Operator
    {
        get
        {
            return (OperatorOption)cbOperators.SelectedItem;
        }
        set
        {
            cbOperators.SelectedItem = value;
        }
    }

    public string Value
    {
        get
        {
            if (nbValue.Visibility == Visibility.Visible)
                return nbValue.Text;
            else if (nbValue2.Visibility == Visibility.Visible)
                return nbValue2.Text;
            else if (cbValue.Visibility == Visibility.Visible)
                return cbValue.SelectedValue?.ToString() ?? string.Empty;
            else if (tbValue.Visibility == Visibility.Visible)
                return tbValue.Text;

            return string.Empty;
        }
        set
        {
            if (nbValue2.Visibility == Visibility.Visible)
                nbValue2.Text = value;
            else if (nbValue.Visibility == Visibility.Visible)
                nbValue.Text = value;
            else if (cbValue.Visibility == Visibility.Visible)
                cbValue.SelectedValue = value;
            else if (tbValue.Visibility == Visibility.Visible)
                tbValue.Text = value;
        }
    }

    public string Value2
    {
        get
        {
            return nbValue2.Text;
        }
        set
        {
            nbValue2.Text = value;
        }
    }


    public PlaylistGroupFilter(ResourceLoader resourceLoader)
    {
        InitializeComponent();

        _resourceLoader = resourceLoader;

        InitEntitiesList();
    }


    private void InitEntitiesList()
    {
        _entities.Add(new EntityOption(SmartPlaylistEntity.Artists, _resourceLoader.GetString("playlistGroupEntityArtists")));
        _entities.Add(new EntityOption(SmartPlaylistEntity.Albums, _resourceLoader.GetString("playlistGroupEntityAlbums")));
        _entities.Add(new EntityOption(SmartPlaylistEntity.Tracks, _resourceLoader.GetString("playlistGroupEntityTracks")));
        _entities.Add(new EntityOption(SmartPlaylistEntity.Genres, _resourceLoader.GetString("playlistGroupEntityGenres")));
        _entities.Add(new EntityOption(SmartPlaylistEntity.Countries, _resourceLoader.GetString("playlistGroupEntityCountries")));

        _entities.Sort((a, b) => _compareInfo.Compare(a.Label, b.Label, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace));

        cbEntities.ItemsSource = _entities;
        cbEntities.DisplayMemberPath = DisplayMemberValuePath;
        cbEntities.SelectedValuePath = SelectedValuePath;
    }


    private void EntitiesSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cbEntities.SelectedItem == null)
            return;

        if (e.AddedItems.Count == 1 && e.RemovedItems.Count == 1 && ((EntityOption)e.AddedItems[0]).Key == ((EntityOption)e.RemovedItems[0]).Key)
            return;

        SmartPlaylistEntity entity = ((EntityOption)cbEntities.SelectedItem).Key;

        LoadFieldsList(entity);
    }


    private void LoadFieldsList(SmartPlaylistEntity entity)
    {
        List<FieldOption> fields = [];

        switch (entity)
        {
            case SmartPlaylistEntity.Tracks:
                fields.Add(new FieldOption(SmartPlaylistField.Score, SmartPlaylistFieldType.Int, _resourceLoader.GetString("playlistGroupFieldScore")));
                fields.Add(new FieldOption(SmartPlaylistField.ListenCount, SmartPlaylistFieldType.Int, _resourceLoader.GetString("playlistGroupFieldListenCount")));
                fields.Add(new FieldOption(SmartPlaylistField.IsLive, SmartPlaylistFieldType.Bool, _resourceLoader.GetString("playlistGroupFieldLive")));
                fields.Add(new FieldOption(SmartPlaylistField.CreatDate, SmartPlaylistFieldType.Day, _resourceLoader.GetString("playlistGroupFieldAddDate")));
                fields.Add(new FieldOption(SmartPlaylistField.Bitrate, SmartPlaylistFieldType.Int, _resourceLoader.GetString("playlistGroupFieldBitrate")));
                fields.Add(new FieldOption(SmartPlaylistField.LastListen, SmartPlaylistFieldType.Int, _resourceLoader.GetString("playlistGroupFieldLastListen")));
                fields.Add(new FieldOption(SmartPlaylistField.SkipCount, SmartPlaylistFieldType.Int, _resourceLoader.GetString("playlistGroupFieldSkipCount")));
                fields.Add(new FieldOption(SmartPlaylistField.Name, SmartPlaylistFieldType.String, _resourceLoader.GetString("playlistGroupFieldName")));
                break;

            case SmartPlaylistEntity.Artists:
                fields.Add(new FieldOption(SmartPlaylistField.IsFavorite, SmartPlaylistFieldType.Bool, _resourceLoader.GetString("playlistGroupFieldFavorite")));
                fields.Add(new FieldOption(SmartPlaylistField.ListenCount, SmartPlaylistFieldType.Int, _resourceLoader.GetString("playlistGroupFieldListenCount")));
                fields.Add(new FieldOption(SmartPlaylistField.CreatDate, SmartPlaylistFieldType.Day, _resourceLoader.GetString("playlistGroupFieldAddDate")));
                fields.Add(new FieldOption(SmartPlaylistField.AlbumCount, SmartPlaylistFieldType.Int, _resourceLoader.GetString("playlistGroupFieldAlbumCount")));
                fields.Add(new FieldOption(SmartPlaylistField.BestofCount, SmartPlaylistFieldType.Int, _resourceLoader.GetString("playlistGroupFieldBestofCount")));
                fields.Add(new FieldOption(SmartPlaylistField.CompilationCount, SmartPlaylistFieldType.Int, _resourceLoader.GetString("playlistGroupFieldCompilationCount")));
                fields.Add(new FieldOption(SmartPlaylistField.TrackCount, SmartPlaylistFieldType.Int, _resourceLoader.GetString("playlistGroupFieldTrackCount")));
                fields.Add(new FieldOption(SmartPlaylistField.LastListen, SmartPlaylistFieldType.Int, _resourceLoader.GetString("playlistGroupFieldLastListen")));
                fields.Add(new FieldOption(SmartPlaylistField.SkipCount, SmartPlaylistFieldType.Int, _resourceLoader.GetString("playlistGroupFieldSkipCount")));
                fields.Add(new FieldOption(SmartPlaylistField.Name, SmartPlaylistFieldType.String, _resourceLoader.GetString("playlistGroupFieldName")));
                break;

            case SmartPlaylistEntity.Albums:
                fields.Add(new FieldOption(SmartPlaylistField.IsFavorite, SmartPlaylistFieldType.Bool, _resourceLoader.GetString("playlistGroupFieldFavorite")));
                fields.Add(new FieldOption(SmartPlaylistField.ListenCount, SmartPlaylistFieldType.Int, _resourceLoader.GetString("playlistGroupFieldListenCount")));
                fields.Add(new FieldOption(SmartPlaylistField.CreatDate, SmartPlaylistFieldType.Day, _resourceLoader.GetString("playlistGroupFieldAddDate")));
                fields.Add(new FieldOption(SmartPlaylistField.TrackCount, SmartPlaylistFieldType.Int, _resourceLoader.GetString("playlistGroupFieldTrackCount")));
                fields.Add(new FieldOption(SmartPlaylistField.LastListen, SmartPlaylistFieldType.Int, _resourceLoader.GetString("playlistGroupFieldLastListen")));
                fields.Add(new FieldOption(SmartPlaylistField.SkipCount, SmartPlaylistFieldType.Int, _resourceLoader.GetString("playlistGroupFieldSkipCount")));
                fields.Add(new FieldOption(SmartPlaylistField.Year, SmartPlaylistFieldType.Int, _resourceLoader.GetString("playlistGroupFieldYear")));
                fields.Add(new FieldOption(SmartPlaylistField.ReleaseDate, SmartPlaylistFieldType.Day, _resourceLoader.GetString("playlistGroupFieldReleaseDate")));
                fields.Add(new FieldOption(SmartPlaylistField.IsBestof, SmartPlaylistFieldType.Bool, _resourceLoader.GetString("playlistGroupFieldBestof")));
                fields.Add(new FieldOption(SmartPlaylistField.IsCompilation, SmartPlaylistFieldType.Bool, _resourceLoader.GetString("playlistGroupFieldCompilation")));
                fields.Add(new FieldOption(SmartPlaylistField.IsLive, SmartPlaylistFieldType.Bool, _resourceLoader.GetString("playlistGroupFieldLive")));
                fields.Add(new FieldOption(SmartPlaylistField.Name, SmartPlaylistFieldType.String, _resourceLoader.GetString("playlistGroupFieldName")));
                break;

            case SmartPlaylistEntity.Genres:
                fields.Add(new FieldOption(SmartPlaylistField.IsFavorite, SmartPlaylistFieldType.Bool, _resourceLoader.GetString("playlistGroupFieldFavorite")));
                fields.Add(new FieldOption(SmartPlaylistField.ListenCount, SmartPlaylistFieldType.Int, _resourceLoader.GetString("playlistGroupFieldListenCount")));
                fields.Add(new FieldOption(SmartPlaylistField.AlbumCount, SmartPlaylistFieldType.Int, _resourceLoader.GetString("playlistGroupFieldAlbumCount")));
                fields.Add(new FieldOption(SmartPlaylistField.BestofCount, SmartPlaylistFieldType.Int, _resourceLoader.GetString("playlistGroupFieldBestofCount")));
                fields.Add(new FieldOption(SmartPlaylistField.CompilationCount, SmartPlaylistFieldType.Int, _resourceLoader.GetString("playlistGroupFieldCompilationCount")));
                fields.Add(new FieldOption(SmartPlaylistField.TrackCount, SmartPlaylistFieldType.Int, _resourceLoader.GetString("playlistGroupFieldTrackCount")));
                fields.Add(new FieldOption(SmartPlaylistField.ArtistCount, SmartPlaylistFieldType.Int, _resourceLoader.GetString("playlistGroupFieldArtistCount")));
                fields.Add(new FieldOption(SmartPlaylistField.Name, SmartPlaylistFieldType.String, _resourceLoader.GetString("playlistGroupFieldName")));
                break;

            case SmartPlaylistEntity.Countries:
                fields.Add(new FieldOption(SmartPlaylistField.Name, SmartPlaylistFieldType.String, _resourceLoader.GetString("playlistGroupFieldName")));
                break;
        }

        fields.Sort((a, b) => _compareInfo.Compare(a.Label, b.Label, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace));

        cbFields.ItemsSource = fields;
        cbFields.DisplayMemberPath = DisplayMemberValuePath;

        cbFields.SelectedIndex = 0;
        cbFields.SelectedIndex = 0;
    }


    private void FieldsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cbFields.SelectedItem == null)
            return;

        if (e.AddedItems.Count == 1 && e.RemovedItems.Count == 1 && ((FieldOption)e.AddedItems[0]).Key == ((FieldOption)e.RemovedItems[0]).Key)
            return;

        SmartPlaylistField field = ((FieldOption)cbFields.SelectedItem).Key;

        LoadOperatorList(field);
    }


    private void LoadOperatorList(SmartPlaylistField field)
    {
        List<OperatorOption> operators = [];

        switch (field)
        {
            case SmartPlaylistField.CreatDate:
            case SmartPlaylistField.ReleaseDate:
                operators = LoadDateTimeOperators();
                DisplayValueNumberBox();
                break;

            case SmartPlaylistField.Score:
            case SmartPlaylistField.ListenCount:
            case SmartPlaylistField.Bitrate:
            case SmartPlaylistField.LastListen:
            case SmartPlaylistField.Year:
            case SmartPlaylistField.AlbumCount:
            case SmartPlaylistField.ArtistCount:
            case SmartPlaylistField.TrackCount:
            case SmartPlaylistField.SkipCount:
            case SmartPlaylistField.LiveCount:
            case SmartPlaylistField.CompilationCount:
            case SmartPlaylistField.BestofCount:
            case SmartPlaylistField.Duration:
            case SmartPlaylistField.Size:
                operators = LoadIntegerOperators();
                DisplayValueNumberBox();
                break;

            case SmartPlaylistField.IsLive:
            case SmartPlaylistField.IsBestof:
            case SmartPlaylistField.IsCompilation:
            case SmartPlaylistField.IsFavorite:
                operators = LoadBoolOperators();
                DisplayValueComboBox();
                break;

            case SmartPlaylistField.Name:
                operators = LoadStringOperators();
                DisplayValueTextBox();
                break;
        }

        operators.Sort((a, b) => _compareInfo.Compare(a.Label, b.Label, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace));

        cbOperators.ItemsSource = operators;
        cbOperators.DisplayMemberPath = DisplayMemberValuePath;

        cbOperators.SelectedIndex = 0;
    }


    private void OperatorsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cbOperators.SelectedValue == null)
            return;

        if (e.AddedItems.Count == 1 && e.RemovedItems.Count == 1 && ((OperatorOption)e.AddedItems[0]).Key == ((OperatorOption)e.RemovedItems[0]).Key)
            return;

        OperatorOption operatorOption = (OperatorOption)cbOperators.SelectedItem;

        if (operatorOption.Key == SmartPlaylistOperator.Between)
            nbValue2.Visibility = Visibility.Visible;
        else
            nbValue2.Visibility = Visibility.Collapsed;
    }


    private List<OperatorOption> LoadStringOperators()
    {
        List<OperatorOption> operators =
        [
            new OperatorOption(SmartPlaylistOperator.Equals, _resourceLoader.GetString("playlistGroupOperatorEquals")),
            new OperatorOption(SmartPlaylistOperator.NotEquals, _resourceLoader.GetString("playlistGroupOperatorNotEquals")),
            new OperatorOption(SmartPlaylistOperator.Contains, _resourceLoader.GetString("playlistGroupOperatorContains")),
            new OperatorOption(SmartPlaylistOperator.NotContains, _resourceLoader.GetString("playlistGroupOperatorNotContains")),
            new OperatorOption(SmartPlaylistOperator.StartsWith, _resourceLoader.GetString("playlistGroupOperatorStartWith")),
            new OperatorOption(SmartPlaylistOperator.EndsWith, _resourceLoader.GetString("playlistGroupOperatorEndsWith")),
        ];

        return operators;
    }

    private List<OperatorOption> LoadIntegerOperators()
    {
        List<OperatorOption> operators =
        [
            new OperatorOption(SmartPlaylistOperator.Equals, _resourceLoader.GetString("playlistGroupOperatorEquals")),
            new OperatorOption(SmartPlaylistOperator.NotEquals,_resourceLoader.GetString("playlistGroupOperatorNotEquals")),
            new OperatorOption(SmartPlaylistOperator.GreaterThan, _resourceLoader.GetString("playlistGroupOperatorGreaterThan")),
            new OperatorOption(SmartPlaylistOperator.LessThan,_resourceLoader.GetString("playlistGroupOperatorLessThan")),
            new OperatorOption(SmartPlaylistOperator.Between, _resourceLoader.GetString("playlistGroupOperatorBetween")),
        ];

        return operators;
    }

    private List<OperatorOption> LoadBoolOperators()
    {
        List<OperatorOption> operators =
        [
            new OperatorOption(SmartPlaylistOperator.Equals,_resourceLoader.GetString("playlistGroupOperatorEquals")),
            new OperatorOption(SmartPlaylistOperator.NotEquals, _resourceLoader.GetString("playlistGroupOperatorNotEquals")),
        ];

        return operators;
    }

    private List<OperatorOption> LoadDateTimeOperators()
    {
        List<OperatorOption> operators =
        [
            new OperatorOption(SmartPlaylistOperator.LessThan,_resourceLoader.GetString("playlistGroupOperatorLessThan")),
            new OperatorOption(SmartPlaylistOperator.GreaterThan,_resourceLoader.GetString("playlistGroupOperatorGreaterThan")),
        ];

        return operators;
    }

    private List<KeyValuePair<string, string>> LoadBoolValues()
    {
        List<KeyValuePair<string, string>> values =
        [
            new KeyValuePair<string, string>("true", _resourceLoader.GetString("playlistGroupValueTrue")),
            new KeyValuePair<string, string>("false", _resourceLoader.GetString("playlistGroupValueFalse")),
        ];

        return values;
    }


    private void DisplayValueTextBox()
    {
        tbValue.Visibility = Visibility.Visible;
        cbValue.Visibility = Visibility.Collapsed;
        nbValue.Visibility = Visibility.Collapsed;
        nbValue2.Visibility = Visibility.Collapsed;
    }

    private void DisplayValueNumberBox()
    {
        nbValue.Visibility = Visibility.Visible;
        tbValue.Visibility = Visibility.Collapsed;
        cbValue.Visibility = Visibility.Collapsed;
    }

    private void DisplayValueComboBox()
    {
        cbValue.Visibility = Visibility.Visible;
        tbValue.Visibility = Visibility.Collapsed;
        nbValue.Visibility = Visibility.Collapsed;
        nbValue2.Visibility = Visibility.Collapsed;

        cbValue.ItemsSource = LoadBoolValues();
        cbValue.DisplayMemberPath = DisplayMemberValuePath;
        cbValue.SelectedValuePath = SelectedValuePath;

        cbValue.SelectedIndex = 0;
    }


    private void FilterNew_Click(object? sender, RoutedEventArgs e)
    {
        NewFilterClicked?.Invoke(this, EventArgs.Empty);
    }

    private void FilterRemove_Click(object? sender, RoutedEventArgs e)
    {
        RemoveFilterClicked?.Invoke(this, EventArgs.Empty);
    }


    public sealed class EntityOption(SmartPlaylistEntity key, string label)
    {
        public SmartPlaylistEntity Key { get; init; } = key;

        public string Label { get; init; } = label;
    }

    public sealed class FieldOption(SmartPlaylistField key, SmartPlaylistFieldType fieldType, string label)
    {
        public SmartPlaylistField Key { get; init; } = key;
        public SmartPlaylistFieldType FieldType { get; init; } = fieldType;
        public string Label { get; init; } = label;
    }

    public sealed class OperatorOption(SmartPlaylistOperator key, string label)
    {
        public SmartPlaylistOperator Key { get; init; } = key;
        public string Label { get; init; } = label;
    }
}
