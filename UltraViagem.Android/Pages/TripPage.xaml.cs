using UltraViagem.Android.ViewModels;

namespace UltraViagem.Android.Pages;

public partial class TripPage : ContentPage
{
    private readonly TripViewModel _vm;
    private bool _drawerOpen;
    private int _currentSection;
    private (BoxView bar, Label label)[] _navItems = null!;

    private static readonly string[] SectionTitles =
        ["Visão Geral", "Roteiro", "Gastos", "Dicas", "Tarefas", "Arquivos"];

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
            (Bar5, Label5),
        ];

        ShowSection(0);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.SectionRequested -= ShowSection;
        _vm.SectionRequested += ShowSection;
        _vm.TripUpdated -= OnTripUpdated;
        _vm.TripUpdated += OnTripUpdated;
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
        RefreshOfflineUi();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.SectionRequested -= ShowSection;
        _vm.TripUpdated -= OnTripUpdated;
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
    }

    private void OnTripUpdated() => DrawerTripName.Text = _vm.Trip.Title;

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
        => MainThread.BeginInvokeOnMainThread(RefreshOfflineUi);

    /// <summary>Atualiza a faixa de somente leitura e o item "Baixar para uso offline".</summary>
    private void RefreshOfflineUi()
    {
        var notice = _vm.ReadOnlyNotice;
        ReadOnlyBanner.Text      = notice ?? "";
        ReadOnlyBanner.IsVisible = notice != null;

        OfflineItem.IsVisible   = _vm.IsCloud;
        OfflineStatusLabel.Text = _vm.OfflineStatus;
    }

    private async void OnDownloadOfflineClicked(object? sender, TappedEventArgs e)
    {
        await CloseDrawer();
        if (!Services.OfflineStore.IsOnline)
        {
            await DisplayAlert("Sem internet", "Conecte-se à internet para baixar os anexos.", "OK");
            return;
        }

        BusyLabel.Text = "Baixando para uso offline…";
        BusyOverlay.IsVisible = true;
        try
        {
            var progress = new Progress<(int Done, int Total)>(p =>
                BusyLabel.Text = $"Baixando anexos {p.Done} de {p.Total}…");
            var (ok, total) = await _vm.DownloadForOfflineAsync(progress);

            var msg = total == 0     ? "A viagem foi salva no celular (não há anexos)."
                    : ok == total    ? $"Pronto: a viagem e os {total} anexos estão disponíveis offline."
                                     : $"{ok} de {total} anexos foram baixados. Tente de novo para completar.";
            await DisplayAlert("Uso offline", msg, "OK");
        }
        finally
        {
            BusyOverlay.IsVisible = false;
            RefreshOfflineUi();
        }
    }

    protected override bool OnBackButtonPressed()
    {
        if (_drawerOpen)
        {
            _ = CloseDrawer();
            return true;
        }
        // Em uma seção interna, o back volta para a Visão Geral
        if (_currentSection != 0)
        {
            ShowSection(0);
            return true;
        }
        // Na Visão Geral, deixa o comportamento padrão (fecha a viagem → lista)
        return base.OnBackButtonPressed();
    }

    private void ShowSection(int index)
    {
        _currentSection = index;

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

        // Visão Geral: ✕ fecha a viagem. Telas internas: seta volta para a Visão Geral.
        CloseGlyph.IsVisible = index == 0;
        BackIcon.IsVisible   = index != 0;

        ContentPage page = index switch
        {
            1 => new ItineraryPage(),
            2 => new ExpensesPage(),
            3 => new LinksPage(),
            4 => new TasksPage(),
            5 => new FilesPage(),
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

    private async void OnCloseOrBackTapped(object? sender, TappedEventArgs e)
    {
        if (_currentSection != 0)
            ShowSection(0);                         // tela interna → Visão Geral
        else
            await Navigation.PopModalAsync();       // Visão Geral → fecha a viagem
    }

    private async void OnExportPdfClicked(object? sender, TappedEventArgs e)
    {
        await CloseDrawer();
        BusyLabel.Text = "Gerando PDF…";
        BusyOverlay.IsVisible = true;
        try
        {
            var path = await _vm.ExportPdfAsync();
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Exportar PDF",
                File  = new ShareFile(path)
            });
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Debug("UVDBG", $"ExportPdf erro: {ex}");
            await DisplayAlert("Erro ao exportar", "Não foi possível gerar o PDF.", "OK");
        }
        finally
        {
            BusyOverlay.IsVisible = false;
        }
    }
}
