using UltraViagem.Android.ViewModels;

namespace UltraViagem.Android.Pages;

public partial class TripsPage : ContentPage
{
    private readonly TripsViewModel _vm;
    private readonly TripViewModel  _tripVm;

    public TripsPage(TripsViewModel vm, TripViewModel tripVm)
    {
        InitializeComponent();
        _vm     = vm;
        _tripVm = tripVm;
        BindingContext = vm;

        // Observa quando uma viagem é carregada para navegar
        vm.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName == nameof(TripsViewModel.LoadedTrip) && vm.LoadedTrip is not null)
            {
                _tripVm.Load(vm.LoadedTrip);
                TripViewModel.Current = _tripVm;
                await Shell.Current.GoToAsync("trip");
            }
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.RefreshRecents();
    }
}
