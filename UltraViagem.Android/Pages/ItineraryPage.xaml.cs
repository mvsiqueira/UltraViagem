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

    // Toque longo: edita a atividade
    private async void OnActivityLongPressed(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not ActivityRow row) return;

        var editPage = new ActivityEditPage(row.Source);
        await GetCurrentPage().Navigation.PushModalAsync(editPage);
        var result = await editPage.Result;
        if (result == null) return;

        await TripViewModel.Current!.UpdateActivityAsync(
            row.Source, result.Title, result.Type, result.Color, result.Details);
        row.Refresh();
    }

    private static Page GetCurrentPage()
    {
        var root = Application.Current!.Windows[0].Page!;
        return root.Navigation.ModalStack.LastOrDefault() ?? root;
    }
}
