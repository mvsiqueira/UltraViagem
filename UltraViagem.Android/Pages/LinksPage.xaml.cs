using UltraViagem.Android.ViewModels;
using UltraViagem.Core;

namespace UltraViagem.Android.Pages;

public partial class LinksPage : ContentPage
{
    public LinksPage() => InitializeComponent();

    // Toque simples: abre URL no navegador
    private async void OnLinkTapped(object? sender, EventArgs e)
    {
        if (sender is BindableObject { BindingContext: LinkItem link } &&
            Uri.TryCreate(link.Url, UriKind.Absolute, out var uri))
            await Launcher.Default.OpenAsync(uri);
    }

    // Toca-e-segura: abre tela de edição
    private async void OnLinkLongPressed(object? sender, EventArgs e)
    {
        if (sender is not BindableObject bo || bo.BindingContext is not LinkItem link)
            return;

        var editPage = new ItemEditPage(
            pageTitle: "Editar dica",
            f1Label: "Título", f1Value: link.Title,
            f2Label: "URL", f2Value: link.Url,
            isEdit: true,
            f2Keyboard: Keyboard.Url,
            deleteConfirmMessage: "Excluir esta dica permanentemente?");

        await GetCurrentPage().Navigation.PushModalAsync(editPage);
        var result = await editPage.Result;
        if (result == null) return;

        if (result.Deleted)
        {
            await TripViewModel.Current!.DeleteLinkAsync(link);
            return;
        }

        await TripViewModel.Current!.EditLinkAsync(link, result.Field1, result.Field2);
    }

    // FAB: cria nova dica
    private async void OnAddTapped(object? sender, EventArgs e)
    {
        var editPage = new ItemEditPage(
            pageTitle: "Nova dica",
            f1Label: "Título", f1Value: "",
            f2Label: "URL", f2Value: "",
            isEdit: false,
            f2Keyboard: Keyboard.Url);

        await GetCurrentPage().Navigation.PushModalAsync(editPage);
        var result = await editPage.Result;
        if (result == null || result.Deleted || string.IsNullOrWhiteSpace(result.Field1)) return;

        await TripViewModel.Current!.AddLinkAsync(result.Field1, result.Field2);
    }

    private static Page GetCurrentPage()
    {
        var root = Application.Current!.Windows[0].Page!;
        return root.Navigation.ModalStack.LastOrDefault() ?? root;
    }
}
