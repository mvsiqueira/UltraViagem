using UltraViagem.Android.ViewModels;

namespace UltraViagem.Android.Pages;

public partial class OverviewPage : ContentPage
{
    public OverviewPage() => InitializeComponent();

    private async void OnOpenMapClicked(object sender, EventArgs e)
    {
        var vm = BindingContext as TripViewModel;
        if (vm?.Trip.MyMapsUrl is { } url && !string.IsNullOrWhiteSpace(url))
            await Launcher.Default.OpenAsync(new Uri(url));
    }
}
