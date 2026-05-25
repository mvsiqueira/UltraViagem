using System.Globalization;
using UltraViagem.Core;

namespace UltraViagem.Android.ViewModels;

public sealed class TripViewModel : BindableObject
{
    /// <summary>Instância ativa, usada para passar o VM entre páginas sem DI complexo.</summary>
    public static TripViewModel? Current { get; set; }

    private static readonly CultureInfo PtBr = new("pt-BR");

    public Trip Trip { get; private set; } = new();
    public ItineraryVersion? ActiveVersion { get; private set; }

    public string DurationLabel { get; private set; } = "";
    public string TotalLabel    { get; private set; } = "";
    public string PaidLabel     { get; private set; } = "";
    public bool   HasMapUrl     => !string.IsNullOrWhiteSpace(Trip.MyMapsUrl);
    public bool   HasTasks      => Trip.Tasks.Count > 0;
    public bool   HasLinks      => Trip.Links.Count > 0;
    public bool   HasExpenses   => Trip.Expenses.Count > 0;

    public void Load(Trip trip)
    {
        Trip = trip;
        ActiveVersion = trip.ItineraryVersions.FirstOrDefault(v => v.Id == trip.ActiveVersionId)
                     ?? trip.ItineraryVersions.FirstOrDefault();

        DurationLabel = BuildDurationLabel(trip);
        TotalLabel    = BuildTotalLabel(trip);
        PaidLabel     = BuildPaidLabel(trip);

        OnPropertyChanged(nameof(Trip));
        OnPropertyChanged(nameof(ActiveVersion));
        OnPropertyChanged(nameof(DurationLabel));
        OnPropertyChanged(nameof(TotalLabel));
        OnPropertyChanged(nameof(PaidLabel));
        OnPropertyChanged(nameof(HasMapUrl));
        OnPropertyChanged(nameof(HasTasks));
        OnPropertyChanged(nameof(HasLinks));
        OnPropertyChanged(nameof(HasExpenses));
    }

    private static string BuildDurationLabel(Trip trip)
    {
        if (trip.StartDate is null && trip.EndDate is null) return "";

        var parts = new List<string>();
        if (trip.StartDate.HasValue && trip.EndDate.HasValue)
        {
            var start = trip.StartDate.Value;
            var end   = trip.EndDate.Value;
            int days  = end.DayNumber - start.DayNumber + 1;

            // Ex: "15 – 22 abr 2023 · 8 dias"
            if (start.Month == end.Month && start.Year == end.Year)
                parts.Add($"{start.Day} – {end.Day} {start.ToString("MMM", PtBr)} {start.Year}");
            else
                parts.Add($"{start.ToString("dd MMM", PtBr)} – {end.ToString("dd MMM", PtBr)} {end.Year}");

            parts.Add($"{days} {(days == 1 ? "dia" : "dias")}");
        }
        else if (trip.StartDate.HasValue)
            parts.Add(trip.StartDate.Value.ToString("dd MMM yyyy", PtBr));

        return string.Join(" · ", parts);
    }

    private static string BuildTotalLabel(Trip trip)
    {
        var total = trip.Expenses.Where(e => e.IsActive).Sum(e => e.SubtotalBase);
        return FormatCurrency(total, trip.BaseCurrency);
    }

    private static string BuildPaidLabel(Trip trip)
    {
        var paid = trip.Expenses.Where(e => e.IsActive).Sum(e => e.PaidAmount);
        return FormatCurrency(paid, trip.BaseCurrency);
    }

    public static string FormatCurrency(decimal value, string currency)
        => currency == "BRL"
            ? value.ToString("C2", PtBr)
            : $"{currency} {value.ToString("N2", PtBr)}";

    /// <summary>Mesmo algoritmo ContrastColor do TripPdfExporter.</summary>
    public static string ContrastColor(string hexColor)
    {
        hexColor = hexColor.TrimStart('#');
        if (hexColor.Length < 6) return "#111827";
        int r = Convert.ToInt32(hexColor[0..2], 16);
        int g = Convert.ToInt32(hexColor[2..4], 16);
        int b = Convert.ToInt32(hexColor[4..6], 16);
        double brightness = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
        return brightness > 0.55 ? "#111827" : "#FFFFFF";
    }
}
