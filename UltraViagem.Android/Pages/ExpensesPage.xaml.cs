using UltraViagem.Android.ViewModels;
using UltraViagem.Core;

namespace UltraViagem.Android.Pages;

public partial class ExpensesPage : ContentPage
{
    public ExpensesPage() => InitializeComponent();

    private void OnExpenseTapped(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is ExpenseRow row)
            row.IsExpanded = !row.IsExpanded;
    }

    // Toque longo: abre o editor do gasto
    private async void OnExpenseLongPressed(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not ExpenseRow row) return;

        var editPage = new ExpenseEditPage(row.Source, isEdit: true);
        await GetCurrentPage().Navigation.PushModalAsync(editPage);
        var result = await editPage.Result;
        if (result == null) return;

        if (result.Deleted)
            await TripViewModel.Current!.DeleteExpenseAsync(row.Source);
        else
            await TripViewModel.Current!.UpdateExpenseAsync(row.Source, result.Values);
    }

    // FAB: cria um novo gasto
    private async void OnAddTapped(object? sender, EventArgs e)
    {
        var vm = TripViewModel.Current!;
        var template = new ExpenseItem
        {
            People   = vm.Trip.People,
            Quantity = 1,
            Currency = vm.Trip.BaseCurrency,
            ExchangeRateToBase = 1m,
            IsActive = true,
        };

        var editPage = new ExpenseEditPage(template, isEdit: false);
        await GetCurrentPage().Navigation.PushModalAsync(editPage);
        var result = await editPage.Result;
        if (result == null || result.Deleted) return;

        await vm.AddExpenseAsync(result.Values);
    }

    private async void OnReservationLinkTapped(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is ExpenseRow row && !string.IsNullOrWhiteSpace(row.Link))
        {
            try { await Launcher.Default.OpenAsync(new Uri(row.Link)); }
            catch { }
        }
    }

    private static Page GetCurrentPage()
    {
        var root = Application.Current!.Windows[0].Page!;
        return root.Navigation.ModalStack.LastOrDefault() ?? root;
    }
}
