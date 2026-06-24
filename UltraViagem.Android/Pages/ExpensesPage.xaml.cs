using UltraViagem.Android.ViewModels;

namespace UltraViagem.Android.Pages;

public partial class ExpensesPage : ContentPage
{
    public ExpensesPage() => InitializeComponent();

    private void OnExpenseTapped(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is ExpenseRow row)
            row.IsExpanded = !row.IsExpanded;
    }

    private async void OnReservationLinkTapped(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is ExpenseRow row && !string.IsNullOrWhiteSpace(row.Link))
        {
            try { await Launcher.Default.OpenAsync(new Uri(row.Link)); }
            catch { }
        }
    }
}
