using System.Text.Json;
using System.Text.Json.Serialization;

namespace UltraViagem.Core;

public sealed class TripRepository
{
    private const string ConfigFileName = "config.json";
    private const string TripFileName = "trip.json";

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string RootPath { get; }

    public TripRepository(string rootPath)
    {
        RootPath = rootPath;
    }

    public AppConfig LoadOrCreateConfig()
    {
        var path = Path.Combine(RootPath, ConfigFileName);
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions) ?? new AppConfig();
        }

        var config = new AppConfig();
        SaveConfig(config);
        return config;
    }

    public void SaveConfig(AppConfig config)
    {
        Directory.CreateDirectory(RootPath);
        var path = Path.Combine(RootPath, ConfigFileName);
        File.WriteAllText(path, JsonSerializer.Serialize(config, _jsonOptions));
    }

    public IReadOnlyList<string> GetTripIds()
    {
        if (!Directory.Exists(RootPath))
        {
            return [];
        }

        return Directory
            .EnumerateDirectories(RootPath)
            .Where(directory => File.Exists(Path.Combine(directory, TripFileName)))
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .OrderBy(name => name)
            .ToList();
    }

    public Trip? LoadTrip(string tripId)
    {
        var path = Path.Combine(RootPath, tripId, TripFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Trip>(json, _jsonOptions);
    }

    public void SaveTrip(Trip trip)
    {
        var tripPath = Path.Combine(RootPath, trip.Id);
        Directory.CreateDirectory(tripPath);
        var path = Path.Combine(tripPath, TripFileName);
        File.WriteAllText(path, JsonSerializer.Serialize(trip, _jsonOptions));
    }

    public void CopyTripFolder(string sourceTripId, string destTripId)
    {
        var sourceDir = Path.Combine(RootPath, sourceTripId);
        var destDir = Path.Combine(RootPath, destTripId);
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.EnumerateFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);
        }
    }
}
