namespace UltraViagem.Core;

public sealed class AppConfig
{
    public int Version { get; set; } = 1;
    public string TripsRoot { get; set; } = ".";
    public string DefaultCurrency { get; set; } = "BRL";
    public List<string> RecentTrips { get; set; } = [];
    public List<string> FavoriteTrips { get; set; } = [];
}
