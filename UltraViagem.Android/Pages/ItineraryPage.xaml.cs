using UltraViagem.Android.ViewModels;

namespace UltraViagem.Android.Pages;

public partial class ItineraryPage : ContentPage
{
    public ItineraryPage() => InitializeComponent();

    private void OnActivityTapped(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is ActivityRow row && row.HasDetails)
            row.IsExpanded = !row.IsExpanded;
    }
}
