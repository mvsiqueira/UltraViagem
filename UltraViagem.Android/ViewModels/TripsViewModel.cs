using System.Collections.ObjectModel;
using System.Windows.Input;
using UltraViagem.Android.Services;
using UltraViagem.Core;

namespace UltraViagem.Android.ViewModels;

public sealed class TripsViewModel : BindableObject
{
    private readonly TripFileService _service;
    private bool _isBusy;

    public ObservableCollection<RecentTrip> Recents { get; } = [];
    public ICommand OpenCommand { get; }
    public ICommand OpenRecentCommand { get; }
    public ICommand RemoveRecentCommand { get; }

    // Viagem carregada — consumida pela TripsPage para navegar
    public Trip? LoadedTrip { get; private set; }

    public bool IsBusy
    {
        get => _isBusy;
        private set { _isBusy = value; OnPropertyChanged(); }
    }

    public TripsViewModel(TripFileService service)
    {
        _service = service;
        OpenCommand = new Command(async () => await PickTripAsync());
        OpenRecentCommand = new Command<RecentTrip>(async r => await LoadRecentAsync(r));
        RemoveRecentCommand = new Command<RecentTrip>(RemoveRecent);
        RefreshRecents();
    }

    public void RefreshRecents()
    {
        Recents.Clear();
        foreach (var r in _service.GetRecents())
            Recents.Add(r);
    }

    private async Task PickTripAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var trip = await _service.PickAndLoadAsync();
            if (trip is not null)
            {
                LoadedTrip = trip;
                RefreshRecents();
                OnPropertyChanged(nameof(LoadedTrip));
            }
        }
        finally { IsBusy = false; }
    }

    private async Task LoadRecentAsync(RecentTrip recent)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var trip = await _service.LoadFromPathAsync(recent.Path);
            if (trip is not null)
            {
                LoadedTrip = trip;
                OnPropertyChanged(nameof(LoadedTrip));
            }
            else
            {
                _service.RemoveRecent(recent.Path);
                RefreshRecents();
            }
        }
        finally { IsBusy = false; }
    }

    private void RemoveRecent(RecentTrip recent)
    {
        _service.RemoveRecent(recent.Path);
        Recents.Remove(recent);
    }
}
