using UltraViagem.Android.ViewModels;

namespace UltraViagem.Android.Pages;

public partial class TripPage : ContentPage
{
    private readonly TripViewModel _vm;
    private bool _drawerOpen;
    private (BoxView bar, Label label)[] _navItems = null!;

    private static readonly string[] SectionTitles =
        ["Visão Geral", "Roteiro", "Gastos", "Dicas", "Tarefas"];

    public TripPage()
    {
        InitializeComponent();

        _vm = TripViewModel.Current!;
        BindingContext = _vm;
        DrawerTripName.Text = _vm.Trip.Title;

        _navItems =
        [
            (Bar0, Label0),
            (Bar1, Label1),
            (Bar2, Label2),
            (Bar3, Label3),
            (Bar4, Label4),
        ];

        ShowSection(0);
    }

    private void ShowSection(int index)
    {
        var accent  = (Color)Application.Current!.Resources["Accent"];
        var primary = (Color)Application.Current!.Resources["TextPrimary"];

        for (int i = 0; i < _navItems.Length; i++)
        {
            bool active = i == index;
            _navItems[i].bar.IsVisible = active;
            _navItems[i].label.FontAttributes = active ? FontAttributes.Bold : FontAttributes.None;
            _navItems[i].label.TextColor = active ? accent : primary;
        }

        ToolbarTitle.Text = SectionTitles[index];

        ContentPage page = index switch
        {
            1 => new ItineraryPage(),
            2 => new ExpensesPage(),
            3 => new LinksPage(),
            4 => new TasksPage(),
            _ => new OverviewPage(),
        };
        ContentArea.Content = page.Content;
    }

    private async void OnHamburgerClicked(object sender, EventArgs e)
    {
        if (_drawerOpen) await CloseDrawer();
        else             await OpenDrawer();
    }

    private async Task OpenDrawer()
    {
        DrawerScrim.IsVisible = true;
        DrawerScrim.Opacity = 0;
        await Task.WhenAll(
            DrawerScrim.FadeTo(1, 200),
            DrawerPanel.TranslateTo(0, 0, 200, Easing.CubicOut));
        _drawerOpen = true;
    }

    private async Task CloseDrawer()
    {
        await Task.WhenAll(
            DrawerScrim.FadeTo(0, 200),
            DrawerPanel.TranslateTo(-280, 0, 200, Easing.CubicIn));
        DrawerScrim.IsVisible = false;
        _drawerOpen = false;
    }

    private async void OnScrimTapped(object? sender, TappedEventArgs e)
        => await CloseDrawer();

    private async void OnNavItemTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is string s && int.TryParse(s, out int index))
        {
            await CloseDrawer();
            ShowSection(index);
        }
    }

    private async void OnCloseClicked(object sender, EventArgs e)
        => await Navigation.PopModalAsync();
}
