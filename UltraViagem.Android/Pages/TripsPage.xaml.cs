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
            await _vm.InitializeAsync();
        }
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
                _tripVm!.Load(_vm.LoadedTrip, _vm.LoadedTripUri, _vm.LoadedTripFolderUri);
                TripViewModel.Current = _tripVm;
                await Navigation.PushModalAsync(new TripPage());
            });
        }
    }
}
