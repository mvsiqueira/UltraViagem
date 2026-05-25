using System.Text.Json;
using System.Text.Json.Serialization;
using UltraViagem.Core;

namespace UltraViagem.Android.Services;

public sealed class TripFileService
{
    private const string RecentKey = "recent_trips";
    private const int MaxRecent = 10;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public async Task<Trip?> PickAndLoadAsync()
    {
        var options = new PickOptions
        {
            PickerTitle = "Selecionar trip.json",
            FileTypes = FilePickerFileType.Images   // placeholder; Android ignora filtro por json
        };

        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Selecionar trip.json" });
            if (result is null) return null;

            await using var stream = await result.OpenReadAsync();
            var trip = await JsonSerializer.DeserializeAsync<Trip>(stream, JsonOptions);
            if (trip is null) return null;

            SaveRecent(result.FullPath, trip.Title);
            return trip;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TripFileService] Erro ao abrir: {ex.Message}");
            return null;
        }
    }

    public async Task<Trip?> LoadFromPathAsync(string path)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<Trip>(stream, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public List<RecentTrip> GetRecents()
    {
        var json = Preferences.Default.Get(RecentKey, "[]");
        try { return JsonSerializer.Deserialize<List<RecentTrip>>(json) ?? []; }
        catch { return []; }
    }

    private void SaveRecent(string path, string title)
    {
        var list = GetRecents();
        list.RemoveAll(r => r.Path == path);
        list.Insert(0, new RecentTrip(path, title));
        if (list.Count > MaxRecent) list = list[..MaxRecent];
        Preferences.Default.Set(RecentKey, JsonSerializer.Serialize(list));
    }

    public void RemoveRecent(string path)
    {
        var list = GetRecents();
        list.RemoveAll(r => r.Path == path);
        Preferences.Default.Set(RecentKey, JsonSerializer.Serialize(list));
    }
}

public sealed record RecentTrip(string Path, string Title);
