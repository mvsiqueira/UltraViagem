using UltraViagem.Android.ViewModels;

namespace UltraViagem.Android.Pages;

public partial class TripsPage : ContentPage
{
    private TripsViewModel? _vm;
    private TripViewModel?  _tripVm;

    public TripsPage() => InitializeComponent();

    protected override void OnAppearing()
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

        _vm.RefreshRecents();
    }

    private async void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TripsViewModel.LoadedTrip) && _vm!.LoadedTrip is not null)
        {
            _tripVm!.Load(_vm.LoadedTrip);
            TripViewModel.Current = _tripVm;
            await Shell.Current.GoToAsync("trip");
        }
    }
}
