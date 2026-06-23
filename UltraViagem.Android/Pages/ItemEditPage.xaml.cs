namespace UltraViagem.Android.Pages;

public sealed record ItemEditResult(string Field1, string Field2, bool Deleted);

public partial class ItemEditPage : ContentPage
{
    private readonly TaskCompletionSource<ItemEditResult?> _tcs = new();
    private readonly bool _isEdit;
    private readonly string _deleteConfirmMessage;

    /// <summary>Aguarda o resultado da edição: null = cancelado</summary>
    public Task<ItemEditResult?> Result => _tcs.Task;

    /// <param name="pageTitle">Título exibido na barra (ex: "Editar dica")</param>
    /// <param name="f1Label">Rótulo do campo 1 (ex: "Título")</param>
    /// <param name="f1Value">Valor inicial do campo 1</param>
    /// <param name="f2Label">Rótulo do campo 2; null = esconde o campo</param>
    /// <param name="f2Value">Valor inicial do campo 2</param>
    /// <param name="isEdit">true = modo edição (mostra botão Excluir)</param>
    /// <param name="f2Keyboard">Teclado do campo 2 (padrão = texto)</param>
    /// <param name="deleteConfirmMessage">Mensagem de confirmação de exclusão</param>
    public ItemEditPage(
        string pageTitle,
        string f1Label, string f1Value,
        string? f2Label = null, string? f2Value = null,
        bool isEdit = false,
        Keyboard? f2Keyboard = null,
        string deleteConfirmMessage = "Tem certeza que deseja excluir este item?")
    {
        InitializeComponent();

        _isEdit = isEdit;
        _deleteConfirmMessage = deleteConfirmMessage;

        TitleLabel.Text  = pageTitle;
        F1Label.Text     = f1Label;
        F1Entry.Text     = f1Value;

        if (f2Label != null)
        {
            F2Label.Text    = f2Label;
            F2Entry.Text    = f2Value ?? "";
            F2Entry.Keyboard = f2Keyboard ?? Keyboard.Text;
        }
        else
        {
            F2Group.IsVisible = false;
        }

        DeleteButton.IsVisible = isEdit;
        if (isEdit)
            DeleteButton.Text = $"Excluir {pageTitle.Replace("Editar ", "").ToLowerInvariant()}";
    }

    private void OnCancelClicked(object? sender, EventArgs e)
    {
        _tcs.TrySetResult(null);
        _ = Navigation.PopModalAsync();
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        var f1 = F1Entry.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(f1))
        {
            F1Entry.Focus();
            return;
        }
        _tcs.TrySetResult(new ItemEditResult(f1, F2Entry.Text?.Trim() ?? "", false));
        _ = Navigation.PopModalAsync();
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        bool confirm = await DisplayAlert("Confirmar exclusão", _deleteConfirmMessage, "Excluir", "Cancelar");
        if (!confirm) return;
        _tcs.TrySetResult(new ItemEditResult("", "", true));
        _ = Navigation.PopModalAsync();
    }

    // Fecha ao pressionar o botão Voltar do Android
    protected override bool OnBackButtonPressed()
    {
        _tcs.TrySetResult(null);
        return base.OnBackButtonPressed();
    }
}
