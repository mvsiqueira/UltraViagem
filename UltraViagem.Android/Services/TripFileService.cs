using global::Android.Provider;
using System.Text.Json;
using System.Text.Json.Serialization;
using UltraViagem.Core;

namespace UltraViagem.Android.Services;

public sealed partial class TripFileService : ITripStorage
{
    private const string RepoUriKey      = "repo_uri";
    private const string RepoKindKey     = "repo_kind";
    private const string RepoLabelKey    = "repo_label";
    private const string LastTripUriKey  = "last_trip_uri";
    private const string LastTripTitleKey = "last_trip_title";
    private const string LastTripRepoKey  = "last_trip_repo";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    // ── Repositório ─────────────────────────────────────────

    public string? GetSavedRepoUri()   => Preferences.Default.Get<string?>(RepoUriKey, null);
    public string  GetSavedRepoKind()  => Preferences.Default.Get<string?>(RepoKindKey, "saf") ?? "saf";
    public string? GetSavedRepoLabel() => Preferences.Default.Get<string?>(RepoLabelKey, null);

    /// <summary>Persiste o repositório ativo (provedor + ref opaca + rótulo). Não pega permissão.</summary>
    public void SaveRepoRef(string kind, string repoRef, string? label)
    {
        Preferences.Default.Set(RepoKindKey, kind);
        Preferences.Default.Set(RepoUriKey, repoRef);
        if (label != null) Preferences.Default.Set(RepoLabelKey, label);
    }

    public void SaveRepoUri(global::Android.Net.Uri uri)
    {
        var resolver = Platform.AppContext.ContentResolver!;
        try
        {
            // Tenta persistir leitura + escrita (local, Google Drive): permite salvar edições no disco.
            resolver.TakePersistableUriPermission(
                uri, global::Android.Content.ActivityFlags.GrantReadUriPermission
                   | global::Android.Content.ActivityFlags.GrantWriteUriPermission);
        }
        catch
        {
            // Provedores somente-leitura (ex.: OneDrive) não concedem escrita persistível.
            // Cai para leitura para que a pasta ainda funcione como visualizador (como antes).
            try
            {
                resolver.TakePersistableUriPermission(
                    uri, global::Android.Content.ActivityFlags.GrantReadUriPermission);
            }
            catch { }
        }
        SaveRepoRef("saf", uri.ToString(), null);
    }

    // ── Última viagem ────────────────────────────────────────

    /// <summary>Última viagem aberta — só é retornada se pertencer ao repositório atual (evita atalho com ref de outro provedor).</summary>
    public TripEntry? GetLastTrip(string? currentRepoRef)
    {
        var uri   = Preferences.Default.Get<string?>(LastTripUriKey, null);
        var title = Preferences.Default.Get<string?>(LastTripTitleKey, null);
        var repo  = Preferences.Default.Get<string?>(LastTripRepoKey, null);
        if (uri == null || title == null) return null;
        // Só mostra a última viagem se ela é do repositório atualmente selecionado.
        if (currentRepoRef == null || repo != currentRepoRef) return null;
        return new TripEntry(title, null, uri);
    }

    public void SaveLastTrip(TripEntry entry, string? repoRef)
    {
        Preferences.Default.Set(LastTripUriKey, entry.UriString);
        Preferences.Default.Set(LastTripTitleKey, entry.Title);
        if (repoRef != null) Preferences.Default.Set(LastTripRepoKey, repoRef);
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

// ── ITripStorage (SAF) ───────────────────────────────────────
public sealed partial class TripFileService
{
    public string Kind => "saf";
    public bool   AccessDenied => ScanPermissionDenied;

    public Task<List<TripEntry>> ScanTripsAsync(string repoRef) => ScanRepositoryAsync(repoRef);
    public Task<Trip?>           LoadTripAsync(string tripRef)  => LoadTripFromUriAsync(tripRef);
    // SaveTripAsync(string, Trip) já satisfaz a interface.

    private global::Android.Net.Uri? ResolveSibling(string tripRef, string? folderRef, string filename)
    {
        var uri = BuildSiblingUri(tripRef, filename);
        if (uri != null) return uri;
        return folderRef != null ? FindSiblingInFolder(folderRef, filename) : null;
    }

    public Task<Stream?> OpenAttachmentAsync(string tripRef, string? folderRef, string filename)
    {
        try
        {
            var uri = ResolveSibling(tripRef, folderRef, filename);
            if (uri == null) return Task.FromResult<Stream?>(null);
            return Task.FromResult(Platform.AppContext.ContentResolver!.OpenInputStream(uri));
        }
        catch { return Task.FromResult<Stream?>(null); }
    }

    public Task<bool> DeleteAttachmentAsync(string tripRef, string? folderRef, string filename)
    {
        try
        {
            var uri = ResolveSibling(tripRef, folderRef, filename);
            if (uri == null) return Task.FromResult(false);
            global::Android.Provider.DocumentsContract.DeleteDocument(
                Platform.AppContext.ContentResolver!, uri);
            return Task.FromResult(true);
        }
        catch { return Task.FromResult(false); }
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
