using System.Globalization;
using System.Windows;
using UltraViagem.Core;

namespace UltraViagem.App;

public partial class TripDetailsWindow : Window
{
    private readonly TripDetailsDraft _draft;

    public TripDetailsWindow(TripDetailsDraft draft, string title)
    {
        InitializeComponent();
        _draft = draft;
        Title = title;
        TitleText.Text = title;

        TripTitleBox.Text = draft.Title;
        StartDatePicker.SelectedDate = ParseDraftDate(draft.StartDate);
        EndDatePicker.SelectedDate = ParseDraftDate(draft.EndDate);
        PeopleBox.Text = draft.People.ToString(CultureInfo.InvariantCulture);
        CurrencyBox.Text = draft.BaseCurrency;
    }

    public TripDetailsDraft Draft => _draft;

    public static TripDetailsDraft FromTrip(Trip trip)
    {
        return new TripDetailsDraft
        {
            Id = trip.Id,
            Title = trip.Title,
            StartDate = trip.StartDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
            EndDate = trip.EndDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
            People = trip.People,
            BaseCurrency = trip.BaseCurrency,
            MyMapsUrl = trip.MyMapsUrl
        };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TripTitleBox.Text))
        {
            ShowError("Informe o nome da viagem.");
            return;
        }

        if (!TryReadDatePicker(StartDatePicker, "data inicial", out var startDate))
        {
            return;
        }

        if (!TryReadDatePicker(EndDatePicker, "data final", out var endDate))
        {
            return;
        }

        if (!int.TryParse(PeopleBox.Text, CultureInfo.InvariantCulture, out var people) || people < 1)
        {
            ShowError("Informe uma quantidade válida de pessoas.");
            return;
        }

        _draft.Title = TripTitleBox.Text.Trim();
        _draft.StartDate = FormatDraftDate(startDate);
        _draft.EndDate = FormatDraftDate(endDate);
        _draft.People = people;
        _draft.BaseCurrency = string.IsNullOrWhiteSpace(CurrencyBox.Text) ? "BRL" : CurrencyBox.Text.Trim().ToUpperInvariant();

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private bool TryReadDatePicker(System.Windows.Controls.DatePicker picker, string label, out DateTime? date)
    {
        date = picker.SelectedDate;
        if (!string.IsNullOrWhiteSpace(picker.Text) && date is null)
        {
            ShowError($"Selecione uma {label} válida.");
            return false;
        }

        return true;
    }

    private static DateTime? ParseDraftDate(string value)
    {
        return DateOnly.TryParse(value, CultureInfo.InvariantCulture, out var date)
            ? date.ToDateTime(TimeOnly.MinValue)
            : null;
    }

    private static string FormatDraftDate(DateTime? value)
    {
        return value is null
            ? ""
            : DateOnly.FromDateTime(value.Value).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}

public sealed class TripDetailsDraft
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public int People { get; set; } = 1;
    public string BaseCurrency { get; set; } = "BRL";
    public string? MyMapsUrl { get; set; }
}
