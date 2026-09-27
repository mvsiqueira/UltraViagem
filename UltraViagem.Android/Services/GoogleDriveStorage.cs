using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UltraViagem.Core;

namespace UltraViagem.Android.Services;

/// <summary>
/// Backend de armazenamento sobre a Google Drive API v3.
/// As "refs" são ids de arquivo/pasta do Drive: repoRef = pasta raiz das viagens,
/// tripRef = id do <c>trip.json</c>, folderRef = id da pasta da viagem.
/// </summary>
public sealed class GoogleDriveStorage : ITripStorage, ICloudFolderBrowser
{
    private const string FilesEndpoint  = "https://www.googleapis.com/drive/v3/files";
    private const string UploadEndpoint = "https://www.googleapis.com/upload/drive/v3/files";

    private static readonly JsonSerializerOptions TripOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly GoogleAuthService _auth;
    private readonly OfflineStore _offline;
    private readonly HttpClient _http = new();

    public string Kind => "gdrive";
    public string RootName => "Meu Drive";
    public bool   AccessDenied { get; private set; }

    public GoogleDriveStorage(GoogleAuthService auth, OfflineStore offline)
    {
        _auth    = auth;
        _offline = offline;
    }

    // ── Navegação de pastas (para o usuário escolher a pasta de viagens) ──

    /// <summary>Lista as subpastas de <paramref name="parentId"/> ("root" para a raiz do Drive).</summary>
    public async Task<List<DriveFolder>?> ListFoldersAsync(string parentId)
    {
        var items = await ListChildrenAsync(parentId, "mimeType='application/vnd.google-apps.folder'");
        return items?.Select(i => new DriveFolder(i.Id, i.Name)).OrderBy(f => f.Name).ToList();
    }

    // ── ITripStorage ─────────────────────────────────────────

    public async Task<List<TripEntry>> ScanTripsAsync(string repoRef)
    {
        AccessDenied = false;

        // Pré-aquece o token (evita várias renovações concorrentes na rajada abaixo).
        if (await _auth.GetAccessTokenAsync() == null) { AccessDenied = OfflineStore.IsOnline; return []; }

        var folders = await ListChildrenAsync(repoRef, "mimeType='application/vnd.google-apps.folder'");
        if (folders == null) return [];

        // Processa as pastas em paralelo (limitado, para não estourar o rate limit do Drive).
        using var gate = new SemaphoreSlim(6);
        var tasks = folders.Select(async folder =>
        {
            await gate.WaitAsync();
            try
            {
                var tripFile = (await ListChildrenAsync(folder.Id, "name='trip.json'"))?.FirstOrDefault();
                if (tripFile == null) return null;
                var trip = await LoadTripAsync(tripFile.Id);
                if (trip == null) return null;
                _offline.SaveTrip(tripFile.Id, trip);   // cópia para uso sem internet
                return new TripEntry(
                    trip.Title, trip.StartDate?.ToString("yyyy-MM-dd"), tripFile.Id)
                { FolderUri = folder.Id };
            }
            finally { gate.Release(); }
        });

        var entries = (await Task.WhenAll(tasks)).OfType<TripEntry>();
        return [.. entries.OrderByDescending(t => t.SortKey)];
    }

    public async Task<Trip?> LoadTripAsync(string tripRef)
    {
        var resp = await SendAsync(HttpMethod.Get, $"{FilesEndpoint}/{tripRef}?alt=media");
        if (resp == null || !resp.IsSuccessStatusCode) return null;
        try
        {
            using var s = await resp.Content.ReadAsStreamAsync();
            return await JsonSerializer.DeserializeAsync<Trip>(s, TripOpts);
        }
        catch { return null; }
    }

    public async Task<bool> SaveTripAsync(string tripRef, Trip trip)
    {
        var json = JsonSerializer.Serialize(trip, TripOpts);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var resp = await SendAsync(new HttpMethod("PATCH"),
            $"{UploadEndpoint}/{tripRef}?uploadType=media", content);
        return resp?.IsSuccessStatusCode ?? false;
    }

    public async Task<Stream?> OpenAttachmentAsync(string tripRef, string? folderRef, string filename)
    {
        if (folderRef == null) return null;
        var id = await FindFileIdAsync(folderRef, filename);
        if (id == null) return null;
        var resp = await SendAsync(HttpMethod.Get, $"{FilesEndpoint}/{id}?alt=media");
        if (resp == null || !resp.IsSuccessStatusCode) return null;
        try { return await resp.Content.ReadAsStreamAsync(); }
        catch { return null; }
    }

    public async Task<bool> DeleteAttachmentAsync(string tripRef, string? folderRef, string filename)
    {
        if (folderRef == null) return false;
        var id = await FindFileIdAsync(folderRef, filename);
        if (id == null) return false;
        var resp = await SendAsync(HttpMethod.Delete, $"{FilesEndpoint}/{id}");
        return resp?.IsSuccessStatusCode ?? false;
    }

    // ── Helpers ──────────────────────────────────────────────

    private async Task<string?> FindFileIdAsync(string folderRef, string filename)
    {
        var esc = filename.Replace("\\", "\\\\").Replace("'", "\\'");
        var files = await ListChildrenAsync(folderRef, $"name='{esc}'");
        return files?.FirstOrDefault()?.Id;
    }

    private async Task<List<DriveItem>?> ListChildrenAsync(string parentId, string extra)
    {
        var q   = $"'{parentId}' in parents and trashed=false and {extra}";
        var url = $"{FilesEndpoint}?q={Uri.EscapeDataString(q)}&fields=files(id,name,mimeType)&pageSize=1000";
        var resp = await SendAsync(HttpMethod.Get, url);
        if (resp == null || !resp.IsSuccessStatusCode) return null;
        try
        {
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStreamAsync());
            var list = new List<DriveItem>();
            if (doc.RootElement.TryGetProperty("files", out var files))
                foreach (var f in files.EnumerateArray())
                    list.Add(new DriveItem(
                        f.GetProperty("id").GetString()!,
                        f.GetProperty("name").GetString()!,
                        f.TryGetProperty("mimeType", out var m) ? m.GetString() : null));
            return list;
        }
        catch { return null; }
    }

    private async Task<HttpResponseMessage?> SendAsync(HttpMethod method, string url, HttpContent? content = null)
    {
        var token = await _auth.GetAccessTokenAsync();
        if (token == null) { AccessDenied = OfflineStore.IsOnline; return null; }
        try
        {
            using var req = new HttpRequestMessage(method, url) { Content = content };
            req.Headers.Authorization = new("Bearer", token);
            var resp = await _http.SendAsync(req);
            // Só sinaliza acesso perdido em falha real de autenticação (401).
            // 403 pode ser rate-limit temporário e não deve esvaziar a lista.
            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                AccessDenied = true;
            return resp;
        }
        catch { return null; }
    }

    private sealed record DriveItem(string Id, string Name, string? MimeType);
}

