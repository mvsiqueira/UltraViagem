using UltraViagem.Android.Services;
using UltraViagem.Android.ViewModels;

namespace UltraViagem.Android.Pages;

public partial class TripsPage : ContentPage
{
    private TripsViewModel? _vm;
    private TripViewModel?  _tripVm;
    private bool _initialized;

    public TripsPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_vm == null)
        {
            var svc = IPlatformApplication.Current!.Services;
            _vm    = svc.GetRequiredService<TripsViewModel>();
            _tripVm = svc.GetRequiredService<TripViewModel>();
            BindingContext = _vm;
            _vm.PropertyChanged += OnVmPropertyChanged;
        }

        if (!_initialized)
        {
            _initialized = true;

            // Preparo rápido (cache + última viagem), sem esperar a varredura da nuvem.
            _vm.PrepareRepo();

            // Abre direto na última viagem (do repositório atual), pulando a lista.
            // Só na primeira vez (guardado por _initialized): ao voltar da viagem,
            // a lista fica visível e não reabre em loop.
            if (_vm.LastTrip is TripEntry last)
                _vm.OpenTripCommand.Execute(last);

            // Varre a lista em segundo plano (não bloqueia a abertura da viagem).
            _ = _vm.RescanAsync();
        }
    }

    private async void OnSelectRepoClicked(object? sender, EventArgs e)
    {
        var choice = await DisplayActionSheet(
            "Onde estão suas viagens?", "Cancelar", null,
            "Google Drive", "Armazenamento interno");

        if (choice == "Armazenamento interno")
            await _vm!.SelectSafFolderAsync();
        else if (choice == "Google Drive")
            await ConnectGoogleAsync();
    }

    private async Task ConnectGoogleAsync()
    {
        bool ok;
        try { ok = await _vm!.SignInGoogleAsync(); }
        catch { ok = false; }

        if (!ok)
        {
            await DisplayAlert("Google Drive",
                "Não foi possível entrar na sua conta Google.", "OK");
            return;
        }

        var picker = new DriveFolderPickerPage(_vm!.Drive);
        await Navigation.PushModalAsync(picker);
        var picked = await picker.Result;
        if (picked == null) return;

        await _vm.ConnectGoogleAsync(picked.Value.Id, picked.Value.Name);
    }

    private void OnLastTripTapped(object? sender, TappedEventArgs e)
    {
        global::Android.Util.Log.Debug("UVDBG", $"OnLastTripTapped: lastTrip={_vm?.LastTrip?.Title ?? "null"}");
        if (_vm?.LastTrip is TripEntry entry)
            _vm.OpenTripCommand.Execute(entry);
    }

    private void OnTripTapped(object? sender, TappedEventArgs e)
    {
        var bc = (sender as BindableObject)?.BindingContext;
        global::Android.Util.Log.Debug("UVDBG", $"OnTripTapped: sender={sender?.GetType().Name}, bc={bc?.GetType().Name}={bc}");
        if (bc is TripEntry entry)
            _vm!.OpenTripCommand.Execute(entry);
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TripsViewModel.LoadedTrip) && _vm!.LoadedTrip is not null)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                _tripVm!.Load(_vm.LoadedTrip, _vm.LoadedTripUri, _vm.LoadedTripFolderUri, _vm.LoadedTripStorage);
                TripViewModel.Current = _tripVm;
                await Navigation.PushModalAsync(new TripPage());
            });
        }
    }
}
