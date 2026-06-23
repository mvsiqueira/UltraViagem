using global::Android.Provider;
using System.Text.Json;
using System.Text.Json.Serialization;
using UltraViagem.Core;

namespace UltraViagem.Android.Services;

public sealed class TripFileService
{
    private const string RepoUriKey      = "repo_uri";
    private const string LastTripUriKey  = "last_trip_uri";
    private const string LastTripTitleKey = "last_trip_title";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    // ── Repositório ─────────────────────────────────────────

    public string? GetSavedRepoUri() => Preferences.Default.Get<string?>(RepoUriKey, null);

    public void SaveRepoUri(global::Android.Net.Uri uri)
    {
        try
        {
            Platform.AppContext.ContentResolver!.TakePersistableUriPermission(
                uri, global::Android.Content.ActivityFlags.GrantReadUriPermission);
        }
        catch { }
        Preferences.Default.Set(RepoUriKey, uri.ToString());
    }

    // ── Última viagem ────────────────────────────────────────

    public TripEntry? GetLastTrip()
    {
        var uri   = Preferences.Default.Get<string?>(LastTripUriKey, null);
        var title = Preferences.Default.Get<string?>(LastTripTitleKey, null);
        return (uri != null && title != null) ? new TripEntry(title, null, uri) : null;
    }

    public void SaveLastTrip(TripEntry entry)
    {
        Preferences.Default.Set(LastTripUriKey, entry.UriString);
        Preferences.Default.Set(LastTripTitleKey, entry.Title);
    }

    // ── Scan ─────────────────────────────────────────────────

    public bool ScanPermissionDenied { get; private set; }

    public async Task<List<TripEntry>> ScanRepositoryAsync(string uriString)
    {
        ScanPermissionDenied = false;
        var results = new List<TripEntry>();
        var ctx = Platform.AppContext;
        try
        {
            var treeUri   = global::Android.Net.Uri.Parse(uriString)!;
            var rootDocId = DocumentsContract.GetTreeDocumentId(treeUri)!;
            global::Android.Util.Log.Debug("UVDBG", $"Scan rootDocId={rootDocId}");
            string[] proj = { "document_id", "mime_type", "_display_name" };
            var childrenUri = DocumentsContract.BuildChildDocumentsUriUsingTree(treeUri, rootDocId)!;

            global::Android.Database.ICursor? cursor;
            try
            {
                cursor = ctx.ContentResolver!.Query(childrenUri, proj, null, null, null);
            }
            catch (Exception qex)
            {
                global::Android.Util.Log.Debug("UVDBG", $"Scan Query threw: {qex.GetType().Name}: {qex.Message}");
                ScanPermissionDenied = true;
                return results;
            }

            if (cursor == null)
            {
                global::Android.Util.Log.Debug("UVDBG", "Scan cursor=null → permission denied");
                ScanPermissionDenied = true;
                return results;
            }

            global::Android.Util.Log.Debug("UVDBG", $"Scan cursor rows={cursor.Count}");
            while (cursor.MoveToNext())
            {
                var docId = cursor.GetString(0);
                var mime  = cursor.GetString(1);
                var name  = cursor.GetString(2);
                global::Android.Util.Log.Debug("UVDBG", $"  child: name={name} mime={mime}");
                if (mime != "vnd.android.document/directory" || docId == null) continue;

                var subUri = DocumentsContract.BuildChildDocumentsUriUsingTree(treeUri, docId)!;
                using var sub = ctx.ContentResolver!.Query(subUri, proj, null, null, null);
                if (sub == null) continue;
                while (sub.MoveToNext())
                {
                    if (sub.GetString(2) != "trip.json") continue;
                    var fileDocId = sub.GetString(0);
                    if (fileDocId == null) continue;
                    var fileUri = DocumentsContract.BuildDocumentUriUsingTree(treeUri, fileDocId)!;
                    try
                    {
                        using var stream = ctx.ContentResolver!.OpenInputStream(fileUri);
                        if (stream == null) continue;
                        var trip = await JsonSerializer.DeserializeAsync<Trip>(stream, JsonOptions);
                        if (trip != null)
                        {
                            var folderDocUri = DocumentsContract.BuildDocumentUriUsingTree(treeUri, docId)!;
                            results.Add(new TripEntry(
                                trip.Title,
                                trip.StartDate?.ToString("yyyy-MM-dd"),
                                fileUri.ToString())
                            { FolderUri = folderDocUri.ToString() });
                        }
                    }
                    catch { }
                }
            }
            cursor.Close();
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Debug("UVDBG", $"Scan outer catch: {ex.GetType().Name}: {ex.Message}");
            ScanPermissionDenied = true;
        }
        return [.. results.OrderByDescending(t => t.SortKey)];
    }

    // ── Load / Save ──────────────────────────────────────────

    public async Task<Trip?> LoadTripFromUriAsync(string uriString)
    {
        try
        {
            var uri = global::Android.Net.Uri.Parse(uriString)!;
            using var stream = Platform.AppContext.ContentResolver!.OpenInputStream(uri);
            if (stream == null) return null;
            return await JsonSerializer.DeserializeAsync<Trip>(stream, JsonOptions);
        }
        catch { return null; }
    }

    /// <summary>Constrói o URI SAF de um arquivo irmão de trip.json via manipulação de docId (armazenamento local).</summary>
    public global::Android.Net.Uri? BuildSiblingUri(string tripJsonUriString, string filename)
    {
        try
        {
            var tripUri   = global::Android.Net.Uri.Parse(tripJsonUriString)!;
            var authority = tripUri.Authority!;
            var treeDocId = DocumentsContract.GetTreeDocumentId(tripUri);
            var docId     = DocumentsContract.GetDocumentId(tripUri);
            if (treeDocId == null || docId == null) return null;

            var lastSlash = docId.LastIndexOf('/');
            if (lastSlash < 0) return null;

            var siblingDocId = docId[..lastSlash] + "/" + filename;
            var treeUri      = DocumentsContract.BuildTreeDocumentUri(authority, treeDocId)!;
            return DocumentsContract.BuildDocumentUriUsingTree(treeUri, siblingDocId);
        }
        catch { return null; }
    }

    /// <summary>Localiza um arquivo pelo nome dentro de uma pasta SAF (funciona com Google Drive e armazenamento local).</summary>
    public global::Android.Net.Uri? FindSiblingInFolder(string folderDocUriString, string filename)
    {
        try
        {
            var folderDocUri = global::Android.Net.Uri.Parse(folderDocUriString)!;
            var authority    = folderDocUri.Authority!;
            var treeDocId    = DocumentsContract.GetTreeDocumentId(folderDocUri);
            var folderDocId  = DocumentsContract.GetDocumentId(folderDocUri);
            if (treeDocId == null || folderDocId == null) return null;

            var treeUri     = DocumentsContract.BuildTreeDocumentUri(authority, treeDocId)!;
            var childrenUri = DocumentsContract.BuildChildDocumentsUriUsingTree(treeUri, folderDocId)!;
            string[] proj   = { "document_id", "_display_name" };
            using var cursor = Platform.AppContext.ContentResolver!.Query(childrenUri, proj, null, null, null);
            if (cursor == null) return null;
            while (cursor.MoveToNext())
            {
                if (cursor.GetString(1) == filename)
                    return DocumentsContract.BuildDocumentUriUsingTree(treeUri, cursor.GetString(0)!);
            }
        }
        catch { }
        return null;
    }

    public async Task<bool> SaveTripAsync(string uriString, Trip trip)
    {
        try
        {
            var uri = global::Android.Net.Uri.Parse(uriString)!;
            using var stream = Platform.AppContext.ContentResolver!.OpenOutputStream(uri, "wt");
            if (stream == null) return false;
            await JsonSerializer.SerializeAsync(stream, trip, JsonOptions);
            return true;
        }
        catch { return false; }
    }

    // ── Cache da lista de viagens ────────────────────────────
    // Guarda o resultado do último scan num arquivo privado do app para
    // exibição instantânea na próxima abertura, enquanto o scan roda em segundo plano.

    private static string CacheFilePath => Path.Combine(FileSystem.AppDataDirectory, "trips_cache.json");

    public List<TripEntry>? LoadTripsCache(string repoUri)
    {
        try
        {
            if (!File.Exists(CacheFilePath)) return null;
            var json  = File.ReadAllText(CacheFilePath);
            var cache = JsonSerializer.Deserialize<TripsCache>(json, JsonOptions);
            // Só usa o cache se for da mesma pasta atualmente selecionada
            if (cache == null || cache.RepoUri != repoUri) return null;
            return cache.Entries;
        }
        catch { return null; }
    }

    public async Task SaveTripsCacheAsync(string repoUri, List<TripEntry> entries)
    {
        try
        {
            var cache = new TripsCache { RepoUri = repoUri, Entries = entries };
            var json  = JsonSerializer.Serialize(cache, JsonOptions);
            await File.WriteAllTextAsync(CacheFilePath, json);
        }
        catch { }
    }
}

public sealed class TripsCache
{
    public string RepoUri { get; set; } = "";
    public List<TripEntry> Entries { get; set; } = [];
}

public sealed record TripEntry(string Title, string? StartDate, string UriString)
{
    [JsonIgnore]
    public string SortKey   => StartDate ?? Title;
    public string? FolderUri { get; init; }
}
