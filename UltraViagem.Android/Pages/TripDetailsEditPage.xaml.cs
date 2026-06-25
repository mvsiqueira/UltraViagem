using UltraViagem.Core;

namespace UltraViagem.Android.Pages;

public sealed record TripDetailsResult(
    string Title, DateOnly? Start, DateOnly? End, int People, string Currency, string? MapUrl);

public partial class TripDetailsEditPage : ContentPage
{
    private readonly TaskCompletionSource<TripDetailsResult?> _tcs = new();

    /// <summary>Aguarda o resultado da edição: null = cancelado.</summary>
    public Task<TripDetailsResult?> Result => _tcs.Task;

    public TripDetailsEditPage(Trip trip)
    {
        InitializeComponent();

        TitleEntry.Text    = trip.Title;
        var today          = DateTime.Today;
        StartPicker.Date   = trip.StartDate?.ToDateTime(TimeOnly.MinValue) ?? today;
        EndPicker.Date     = trip.EndDate?.ToDateTime(TimeOnly.MinValue)
                             ?? trip.StartDate?.ToDateTime(TimeOnly.MinValue) ?? today;
        PeopleEntry.Text   = trip.People.ToString();
        CurrencyEntry.Text = trip.BaseCurrency;
        MapEntry.Text      = trip.MyMapsUrl;
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

        int people = int.TryParse((PeopleEntry.Text ?? "").Trim(), out var p) ? Math.Max(1, p) : 1;
        var currency = (CurrencyEntry.Text ?? "").Trim().ToUpperInvariant();
        if (currency.Length == 0) currency = "BRL";

        var result = new TripDetailsResult(
            title,
            DateOnly.FromDateTime(StartPicker.Date ?? DateTime.Today),
            DateOnly.FromDateTime(EndPicker.Date ?? DateTime.Today),
            people,
            currency,
            string.IsNullOrWhiteSpace(MapEntry.Text) ? null : MapEntry.Text.Trim());

        _tcs.TrySetResult(result);
        _ = Navigation.PopModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _tcs.TrySetResult(null);
        return base.OnBackButtonPressed();
    }
}
