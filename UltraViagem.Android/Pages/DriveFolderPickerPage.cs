using System.Collections.ObjectModel;
using UltraViagem.Android.Services;

namespace UltraViagem.Android.Pages;

/// <summary>
/// Navegador simples de pastas de um provedor de nuvem (Google Drive, OneDrive), modal. O usuário entra nas
/// subpastas e toca em "Usar esta pasta" para escolher a pasta raiz das viagens.
/// </summary>
public sealed class DriveFolderPickerPage : ContentPage
{
    private readonly ICloudFolderBrowser _drive;
    private readonly TaskCompletionSource<(string Id, string Name)?> _tcs = new();
    private readonly Stack<(string Id, string Name)> _path = new();
    private readonly ObservableCollection<DriveFolder> _folders = [];

    private readonly Label _crumb = new() { FontSize = 13, TextColor = Colors.Gray, Margin = new Thickness(16, 8) };
    private readonly ActivityIndicator _spinner = new() { IsVisible = false, IsRunning = false, HorizontalOptions = LayoutOptions.Center, Margin = 24 };
    private readonly Label _empty = new() { Text = "Nenhuma subpasta aqui", TextColor = Colors.Gray, HorizontalOptions = LayoutOptions.Center, Margin = 24, IsVisible = false };

    public Task<(string Id, string Name)?> Result => _tcs.Task;

    public DriveFolderPickerPage(ICloudFolderBrowser drive)
    {
        _drive = drive;
        Title = "Escolher pasta de viagens";
        _path.Push(("root", drive.RootName));

        var list = new CollectionView
        {
            ItemsSource = _folders,
            SelectionMode = SelectionMode.None,
            ItemTemplate = new DataTemplate(() =>
            {
                var name = new Label { FontSize = 16, VerticalOptions = LayoutOptions.Center };
                name.SetBinding(Label.TextProperty, "Name");
                var row = new Grid
                {
                    Padding = new Thickness(16, 14),
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
                    ColumnSpacing = 12,
                    Children =
                    {
                        new Label { Text = "\U0001F4C1", FontSize = 18, VerticalOptions = LayoutOptions.Center },
                        name
                    }
                };
                Grid.SetColumn((Label)row.Children[1], 1);
                var tap = new TapGestureRecognizer();
                tap.Tapped += OnFolderTapped;
                row.GestureRecognizers.Add(tap);
                return row;
            })
        };

        var useButton = new Button
        {
            Text = "Usar esta pasta",
            Margin = new Thickness(16, 8, 16, 16),
            BackgroundColor = Color.FromArgb("#0F766E"),
            TextColor = Colors.White
        };
        useButton.Clicked += OnUseCurrentClicked;

        Content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            Children = { _crumb, _spinner, _empty, list, useButton }
        };
        Grid.SetRow(_crumb, 0);
        Grid.SetRow(_spinner, 1);
        Grid.SetRow(_empty, 1);
        Grid.SetRow(list, 1);
        Grid.SetRow(useButton, 2);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_folders.Count == 0) await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _spinner.IsVisible = _spinner.IsRunning = true;
        _empty.IsVisible = false;
        _crumb.Text = string.Join("  ›  ", _path.Reverse().Select(p => p.Name));

        var current = _path.Peek();
        var folders = await _drive.ListFoldersAsync(current.Id);

        _folders.Clear();
        if (folders != null)
            foreach (var f in folders) _folders.Add(f);

        _spinner.IsVisible = _spinner.IsRunning = false;
        _empty.IsVisible = _folders.Count == 0;
    }

    private async void OnFolderTapped(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not DriveFolder folder) return;
        _path.Push((folder.Id, folder.Name));
        await LoadAsync();
    }

    private void OnUseCurrentClicked(object? sender, EventArgs e)
    {
        var current = _path.Peek();
        _tcs.TrySetResult((current.Id, current.Name));
        Navigation.PopModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        if (_path.Count > 1)
        {
            _path.Pop();
            _ = LoadAsync();
            return true;
        }
        _tcs.TrySetResult(null);
        Navigation.PopModalAsync();
        return true;
    }
}
