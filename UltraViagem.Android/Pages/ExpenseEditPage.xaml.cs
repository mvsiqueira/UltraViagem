using System.Globalization;
using UltraViagem.Core;

namespace UltraViagem.Android.Pages;

public sealed record ExpenseEditResult(ExpenseItem Values, bool Deleted);

public partial class ExpenseEditPage : ContentPage
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    private readonly TaskCompletionSource<ExpenseEditResult?> _tcs = new();
    private readonly ExpenseItem _source;

    /// <summary>Aguarda o resultado da edição: null = cancelado.</summary>
    public Task<ExpenseEditResult?> Result => _tcs.Task;

    public ExpenseEditPage(ExpenseItem source, bool isEdit)
    {
        InitializeComponent();
        _source = source;

        TitleLabel.Text = isEdit ? "Editar gasto" : "Novo gasto";

        TitleEntry.Text     = source.Title;
        TypeEntry.Text      = source.Type;
        CompanyEntry.Text   = source.Company;
        PriceEntry.Text     = FmtNum(source.Price);
        TaxesEntry.Text     = FmtNum(source.Taxes);
        PeopleEntry.Text    = source.People.ToString();
        QuantityEntry.Text  = source.Quantity.ToString();
        CurrencyEntry.Text  = source.Currency;
        RateEntry.Text      = FmtNum(source.ExchangeRateToBase);
        PaidEntry.Text      = FmtNum(source.PaidAmount);
        LinkEntry.Text      = source.Link;
        NotesEntry.Text     = source.Notes;
        ActiveSwitch.IsToggled = source.IsActive;

        DeleteButton.IsVisible = isEdit;
    }

    private void OnCancelClicked(object? sender, EventArgs e)
    {
        _tcs.TrySetResult(null);
        _ = Navigation.PopModalAsync();
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        var title = TitleEntry.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(title))
        {
            TitleEntry.Focus();
            return;
        }

        var values = new ExpenseItem
        {
            Id                 = _source.Id,
            Title              = title,
            Type               = EmptyToNull(TypeEntry.Text),
            Company            = EmptyToNull(CompanyEntry.Text),
            Link               = EmptyToNull(LinkEntry.Text),
            Notes              = EmptyToNull(NotesEntry.Text),
            Price              = ParseDecimal(PriceEntry.Text),
            Taxes              = ParseDecimal(TaxesEntry.Text),
            People             = Math.Max(1, ParseInt(PeopleEntry.Text)),
            Quantity           = Math.Max(1, ParseInt(QuantityEntry.Text)),
            Currency           = ParseCurrency(CurrencyEntry.Text),
            ExchangeRateToBase = ParseRate(RateEntry.Text),
            PaidAmount         = ParseDecimal(PaidEntry.Text),
            IsActive           = ActiveSwitch.IsToggled,
        };

        _tcs.TrySetResult(new ExpenseEditResult(values, false));
        _ = Navigation.PopModalAsync();
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        bool confirm = await DisplayAlert("Confirmar exclusão",
            "Excluir este gasto permanentemente?", "Excluir", "Cancelar");
        if (!confirm) return;
        _tcs.TrySetResult(new ExpenseEditResult(_source, true));
        _ = Navigation.PopModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _tcs.TrySetResult(null);
        return base.OnBackButtonPressed();
    }

    // ── Helpers ─────────────────────────────────────────────

    private static string FmtNum(decimal v) => v == 0m ? "" : v.ToString("0.####", PtBr);

    private static string? EmptyToNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static decimal ParseDecimal(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0m;
        s = s.Trim();
        // Aceita vírgula (pt-BR) ou ponto como separador decimal
        if (s.Contains(',')) s = s.Replace(".", "").Replace(',', '.');
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
    }

    private static int ParseInt(string? s)
        => int.TryParse((s ?? "").Trim(), out var i) ? i : 1;

    private static decimal ParseRate(string? s)
    {
        var d = ParseDecimal(s);
        return d > 0m ? d : 1m;
    }

    private static string ParseCurrency(string? s)
    {
        var c = (s ?? "").Trim().ToUpperInvariant();
        return c.Length > 0 ? c : "BRL";
    }
}
