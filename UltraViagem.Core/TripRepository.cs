using System.Diagnostics;
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
        if (!File.Exists(path)) return null;

        try
        {
            var json = File.ReadAllText(path);
            var trip = JsonSerializer.Deserialize<Trip>(json, _jsonOptions);
            return trip is null ? null : ValidateAndRepair(trip, path);
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"[TripRepository] JSON inválido em '{path}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Garante que o trip desserializado está em estado consistente.
    /// Corrige problemas silenciosamente em vez de propagar exceções.
    /// </summary>
    private static Trip ValidateAndRepair(Trip trip, string sourcePath)
    {
        // Garante que todas as coleções existem
        trip.ItineraryVersions ??= [];
        trip.Tasks                ??= [];
        trip.Links                ??= [];
        trip.Expenses             ??= [];
        trip.CurrencyRates        ??= [];
        trip.Attachments          ??= [];

        // Garante integridade interna de cada versão do itinerário
        foreach (var version in trip.ItineraryVersions)
        {
            version.Itinerary      ??= [];
            version.BankActivities ??= [];
            foreach (var day in version.Itinerary)
                day.Activities ??= [];
        }

        // Corrige ActiveVersionId se aponta para uma versão que não existe
        if (trip.ItineraryVersions.Count > 0 &&
            !trip.ItineraryVersions.Any(v => v.Id == trip.ActiveVersionId))
        {
            var fixed_id = trip.ItineraryVersions[0].Id;
            Debug.WriteLine($"[TripRepository] ActiveVersionId '{trip.ActiveVersionId}' não encontrado em '{sourcePath}' — corrigido para '{fixed_id}'.");
            trip.ActiveVersionId = fixed_id;
        }

        // Remove entradas sem identificador (corrupção parcial)
        var removedAttachments = trip.Attachments.RemoveAll(a => string.IsNullOrWhiteSpace(a.File) || string.IsNullOrWhiteSpace(a.Id));
        var removedTasks       = trip.Tasks.RemoveAll(t => string.IsNullOrWhiteSpace(t.Id));
        var removedExpenses    = trip.Expenses.RemoveAll(e => string.IsNullOrWhiteSpace(e.Id));

        if (removedAttachments + removedTasks + removedExpenses > 0)
            Debug.WriteLine($"[TripRepository] Removidos em '{sourcePath}': {removedAttachments} anexo(s), {removedTasks} tarefa(s), {removedExpenses} gasto(s) com dados inválidos.");

        return trip;
    }

    public void SaveTrip(Trip trip, string folderPath)
    {
        Directory.CreateDirectory(folderPath);
        var path = Path.Combine(folderPath, TripFileName);
        File.WriteAllText(path, JsonSerializer.Serialize(trip, _jsonOptions));
    }

    public void CopyTripFolder(string sourceFolderPath, string destFolderName)
    {
        var sourceDir = sourceFolderPath;
        var destDir = Path.Combine(RootPath, destFolderName);
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.EnumerateFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);
        }
    }
}
