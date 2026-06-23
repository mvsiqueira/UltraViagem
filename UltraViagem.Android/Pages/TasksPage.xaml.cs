using UltraViagem.Android.ViewModels;

namespace UltraViagem.Android.Pages;

public partial class TasksPage : ContentPage
{
    public TasksPage() => InitializeComponent();

    // Tap no checkbox: alterna done/pending
    private async void OnToggleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject bo && bo.BindingContext is ObservableTaskItem item)
            await TripViewModel.Current!.ToggleTaskAsync(item);
    }

    // Toca-e-segura: abre tela de edição
    private async void OnTaskLongPressed(object? sender, EventArgs e)
    {
        if (sender is not BindableObject bo || bo.BindingContext is not ObservableTaskItem item)
            return;

        var editPage = new ItemEditPage(
            pageTitle: "Editar tarefa",
            f1Label: "Título", f1Value: item.Title,
            f2Label: "Notas", f2Value: item.Notes ?? "",
            isEdit: true,
            deleteConfirmMessage: "Excluir esta tarefa permanentemente?");

        await GetCurrentPage().Navigation.PushModalAsync(editPage);
        var result = await editPage.Result;
        if (result == null) return;

        if (result.Deleted)
        {
            await TripViewModel.Current!.DeleteTaskAsync(item);
            return;
        }

        item.UpdateContent(result.Field1, string.IsNullOrWhiteSpace(result.Field2) ? null : result.Field2);
        await TripViewModel.Current!.SaveAsync();
    }

    // FAB: cria nova tarefa
    private async void OnAddTapped(object? sender, EventArgs e)
    {
        var editPage = new ItemEditPage(
            pageTitle: "Nova tarefa",
            f1Label: "Título", f1Value: "",
            f2Label: "Notas", f2Value: "",
            isEdit: false);

        await GetCurrentPage().Navigation.PushModalAsync(editPage);
        var result = await editPage.Result;
        if (result == null || result.Deleted || string.IsNullOrWhiteSpace(result.Field1)) return;

        await TripViewModel.Current!.AddTaskAsync(
            result.Field1,
            string.IsNullOrWhiteSpace(result.Field2) ? null : result.Field2);
    }

    private static Page GetCurrentPage()
    {
        var root = Application.Current!.Windows[0].Page!;
        return root.Navigation.ModalStack.LastOrDefault() ?? root;
    }
}
