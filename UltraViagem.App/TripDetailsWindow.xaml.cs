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
        StartDateBox.Text = draft.StartDate;
        EndDateBox.Text = draft.EndDate;
        PeopleBox.Text = draft.People.ToString(CultureInfo.InvariantCulture);
        CurrencyBox.Text = draft.BaseCurrency;
        MyMapsBox.Text = draft.MyMapsUrl ?? "";
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

        if (!string.IsNullOrWhiteSpace(StartDateBox.Text) && !DateOnly.TryParse(StartDateBox.Text, CultureInfo.InvariantCulture, out _))
        {
            ShowError("A data inicial precisa estar no formato aaaa-mm-dd.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(EndDateBox.Text) && !DateOnly.TryParse(EndDateBox.Text, CultureInfo.InvariantCulture, out _))
        {
            ShowError("A data final precisa estar no formato aaaa-mm-dd.");
            return;
        }

        if (!int.TryParse(PeopleBox.Text, CultureInfo.InvariantCulture, out var people) || people < 1)
        {
            ShowError("Informe uma quantidade válida de pessoas.");
            return;
        }

        _draft.Title = TripTitleBox.Text.Trim();
        _draft.StartDate = StartDateBox.Text.Trim();
        _draft.EndDate = EndDateBox.Text.Trim();
        _draft.People = people;
        _draft.BaseCurrency = string.IsNullOrWhiteSpace(CurrencyBox.Text) ? "BRL" : CurrencyBox.Text.Trim().ToUpperInvariant();
        _draft.MyMapsUrl = string.IsNullOrWhiteSpace(MyMapsBox.Text) ? null : MyMapsBox.Text.Trim();

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
