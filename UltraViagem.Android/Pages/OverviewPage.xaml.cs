using UltraViagem.Android.ViewModels;

namespace UltraViagem.Android.Pages;

public partial class OverviewPage : ContentPage
{
    public OverviewPage() => InitializeComponent();

    private async void OnBlockTapped(object? sender, TappedEventArgs e)
    {
        var vm = TripViewModel.Current;
        if (vm == null) return;

        if (e.Parameter is "map")
        {
            if (vm.Trip.MyMapsUrl is { } url && !string.IsNullOrWhiteSpace(url))
                await Launcher.Default.OpenAsync(new Uri(url));
            return;
        }

        if (e.Parameter is string s && int.TryParse(s, out int index))
            vm.RequestSection(index);
    }
}
