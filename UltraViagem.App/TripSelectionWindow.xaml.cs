using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfButton = System.Windows.Controls.Button;
using WpfBrush = System.Windows.Media.Brush;

namespace UltraViagem.App;

public partial class TripSelectionWindow : Window
{
    private readonly List<TripSelectionItem> _items;

    public TripSelectionWindow(IReadOnlyList<TripSelectionItem> items)
    {
        InitializeComponent();
        _items = items.ToList();
        Render();
    }

    public string? SelectedTripId { get; private set; }
    public bool CreateNewTripRequested { get; private set; }
    public event EventHandler<TripSelectionItem>? FavoriteChanged;

    private void Render()
    {
        var favorites = _items.Where(item => item.IsFavorite).OrderBy(item => item.Title).ToList();
        FavoritesSection.Visibility = favorites.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        FavoritesItems.Items.Clear();
        foreach (var item in favorites)
        {
            FavoritesItems.Items.Add(CreateTripCard(item));
        }

        YearGroupsItems.Items.Clear();
        foreach (var group in _items.GroupBy(item => item.Year).OrderByDescending(group => group.Key))
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
            panel.Children.Add(new TextBlock
            {
                Text = group.Key.ToString(),
                FontSize = 21,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var wrap = new WrapPanel();
            foreach (var item in group.OrderBy(item => item.Title))
            {
                wrap.Children.Add(CreateTripCard(item));
            }

            panel.Children.Add(wrap);
            YearGroupsItems.Items.Add(panel);
        }
    }

    private Border CreateTripCard(TripSelectionItem item)
    {
        var title = new TextBlock
        {
            Text = item.Title,
            FontWeight = FontWeights.SemiBold,
            FontSize = 15,
            TextWrapping = TextWrapping.Wrap
        };

        var favoriteGlyph = new TextBlock
        {
            Text = item.IsFavorite ? "★" : "☆",
            FontSize = 24,
            FontWeight = FontWeights.Normal,
            Foreground = item.IsFavorite ? System.Windows.Media.Brushes.Goldenrod : (WpfBrush)FindResource("MutedTextBrush"),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var favoriteButton = new WpfButton
        {
            Content = favoriteGlyph,
            Width = 32,
            Height = 32,
            Padding = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = item.IsFavorite ? "Remover dos favoritos" : "Marcar como favorita",
            HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        favoriteButton.Click += (_, e) =>
        {
            e.Handled = true;
            item.IsFavorite = !item.IsFavorite;
            FavoriteChanged?.Invoke(this, item);
            Render();
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new Border
        {
            Width = 34,
            Height = 34,
            CornerRadius = new CornerRadius(17),
            Background = (WpfBrush)FindResource("AccentSoftBrush"),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = "✈",
                Foreground = (WpfBrush)FindResource("AccentBrush"),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 16
            }
        };

        var textPanel = new StackPanel();
        textPanel.Children.Add(title);
        textPanel.Children.Add(new TextBlock
        {
            Text = item.DateLabel,
            Foreground = (WpfBrush)FindResource("MutedTextBrush"),
            FontSize = 12,
            Margin = new Thickness(0, 2, 0, 0)
        });

        Grid.SetColumn(icon, 0);
        Grid.SetColumn(textPanel, 1);
        Grid.SetColumn(favoriteButton, 2);
        grid.Children.Add(icon);
        grid.Children.Add(textPanel);
        grid.Children.Add(favoriteButton);

        var card = new Border
        {
            Width = 276,
            MinHeight = 78,
            Margin = new Thickness(0, 0, 10, 10),
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(8),
            Background = (WpfBrush)FindResource("PanelBackground"),
            BorderBrush = (WpfBrush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            Child = grid,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        card.MouseLeftButtonUp += (_, _) =>
        {
            SelectedTripId = item.Id;
            DialogResult = true;
        };

        return card;
    }

    private void NewTrip_Click(object sender, RoutedEventArgs e)
    {
        CreateNewTripRequested = true;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
