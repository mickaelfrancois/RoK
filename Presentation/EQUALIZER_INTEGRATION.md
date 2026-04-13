# Intégration de l'Égaliseur

Les modifications suivantes ont été appliquées :

## 1. Classes créées

### `EqualizerViewModel` (Presentation/ViewModels/Player/EqualizerViewModel.cs)
- ViewModel pour l'égaliseur avec 10 bandes de fréquences
- Applique les modifications en temps réel via `IPlayerEngine.SetEqualizerBand()`

### `FloatToStringConverter` (Presentation/Converters/FloatToStringConverter.cs)
- Convertisseur pour afficher les valeurs en dB (ex: +5.0 dB, -3.0 dB)

### `EqualizerBand` et `Equalizer` (Infrastructure/Rok.Infrastructure/Player/)
- Implémentation de l'égaliseur avec NAudio
- Utilise des filtres BiQuad pour chaque bande de fréquence

## 2. Modifications apportées

### `IPlayerEngine` (Core/Rok.Application/Interfaces/IPlayerEngine.cs)
- Ajout de la méthode `SetEqualizerBand(int bandIndex, float gain)`

### `NAudioMediaPlayer` (Infrastructure/Rok.Infrastructure/Player/NAudioMediaPlayer.cs)
- Intégration de l'égaliseur dans la chaîne audio
- Création de 10 bandes : 32Hz, 64Hz, 125Hz, 250Hz, 500Hz, 1kHz, 2kHz, 4kHz, 8kHz, 16kHz

### `WinUIMediaPlayer` (Infrastructure/Rok.Infrastructure/Player/MediaPlayerEngine.cs)
- Ajout d'une implémentation vide de `SetEqualizerBand()` pour la compatibilité

### `PlayerViewModel` (Presentation/ViewModels/Player/PlayerViewModel.cs)
- Injection de `EqualizerViewModel`
- Ajout de la commande `ToggleEqualizerCommand`

### `DependencyInjection` (Presentation/DependencyInjection.cs)
- Enregistrement de `EqualizerViewModel` comme singleton

## 3. Intégration de l'interface utilisateur

Pour intégrer l'égaliseur dans votre player, vous devez créer le contrôle XAML suivant :

### Créer `EqualizerControl.xaml` dans `Presentation/Controls/`

```xaml
<?xml version="1.0" encoding="utf-8"?>
<UserControl x:Class="Rok.Controls.EqualizerControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d"
             Width="520"
             Height="420">

    <Grid Background="{ThemeResource LayerFillColorDefaultBrush}"
          CornerRadius="8"
          BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}"
          BorderThickness="1"
          Padding="24,16">
        <Grid.Shadow>
            <ThemeShadow />
        </Grid.Shadow>

        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <!-- Header -->
        <Grid Grid.Row="0" Margin="0,0,0,16">
            <TextBlock Text="Equalizer"
                       FontSize="20"
                       FontWeight="SemiBold"
                       VerticalAlignment="Center" />

            <Button Content="Reset"
                    HorizontalAlignment="Right"
                    Style="{StaticResource AccentButtonStyle}"
                    Click="ResetButton_Click" />
        </Grid>

        <!-- Equalizer bands -->
        <Grid Grid.Row="1">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="*" />
            </Grid.ColumnDefinitions>

            <!-- Répétez ce pattern pour chaque bande (32Hz, 64Hz, 125Hz, etc.) -->
            <StackPanel Grid.Column="0" Spacing="8">
                <Slider Orientation="Vertical"
                        Minimum="-15"
                        Maximum="15"
                        Value="{x:Bind ViewModel.Band32Hz, Mode=TwoWay}"
                        Height="260"
                        HorizontalAlignment="Center" />
                <TextBlock Text="{x:Bind ViewModel.Band32Hz, Mode=OneWay, Converter={StaticResource FloatToStringConverter}}"
                           FontSize="11"
                           FontWeight="SemiBold"
                           HorizontalAlignment="Center" />
                <TextBlock Text="32Hz"
                           FontSize="10"
                           Opacity="0.7"
                           HorizontalAlignment="Center" />
            </StackPanel>

            <!-- ... Répéter pour les autres bandes ... -->
        </Grid>

        <!-- Zero line indicator -->
        <Border Grid.Row="1"
                Height="1"
                Background="{ThemeResource DividerStrokeColorDefaultBrush}"
                Opacity="0.5"
                VerticalAlignment="Center"
                Margin="0,0,0,36" />
    </Grid>
</UserControl>
```

### Créer `EqualizerControl.xaml.cs`

```csharp
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rok.ViewModels.Player;

namespace Rok.Controls;

public sealed partial class EqualizerControl : UserControl
{
    public EqualizerViewModel ViewModel { get; set; }

    public EqualizerControl()
    {
        InitializeComponent();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.Reset();
    }
}
```

### Intégrer dans votre page de player

```xaml
<!-- Ajouter dans les ressources -->
<Page.Resources>
    <converters:FloatToStringConverter x:Key="FloatToStringConverter" />
</Page.Resources>

<!-- Bouton pour ouvrir l'égaliseur (zone droite du player) -->
<Button Command="{x:Bind PlayerViewModel.ToggleEqualizerCommand}"
        ToolTipService.ToolTip="Equalizer">
    <FontIcon Glyph="&#xE9E9;" FontSize="16" />
</Button>

<!-- Popup de l'égaliseur -->
<Popup IsOpen="{x:Bind PlayerViewModel.EqualizerViewModel.IsOpen, Mode=TwoWay}"
       IsLightDismissEnabled="True">
    <controls:EqualizerControl ViewModel="{x:Bind PlayerViewModel.EqualizerViewModel}" />
</Popup>
```

## 4. Utilisation

- Cliquez sur le bouton d'égaliseur dans le player
- Ajustez les sliders verticaux pour chaque bande (-15dB à +15dB)
- Les modifications sont appliquées en temps réel sur la lecture en cours
- Cliquez sur "Reset" pour réinitialiser toutes les bandes à 0dB

## 5. Notes techniques

- L'égaliseur utilise des filtres BiQuad PeakingEQ de NAudio
- Les modifications sont appliquées immédiatement via `SetEqualizerBand()`
- Les réglages ne sont pas persistés entre les sessions (à ajouter si nécessaire)
- Compatible uniquement avec `NAudioMediaPlayer` (pas avec `WinUIMediaPlayer`)
