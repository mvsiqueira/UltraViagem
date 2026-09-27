using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UltraViagem.Core;

namespace UltraViagem.Android.Services;

/// <summary>
/// Backend de armazenamento sobre a Microsoft Graph (OneDrive).
/// As "refs" são ids de driveItem: repoRef = pasta raiz das viagens,
/// tripRef = id do <c>trip.json</c>, folderRef = id da pasta da viagem.
/// Downloads usam o link pré-autenticado dos metadados (<c>@microsoft.graph.downloadUrl</c>),
/// evitando o redirecionamento de <c>/content</c> para outro host.
/// </summary>
public sealed class OneDriveStorage : ITripStorage, ICloudFolderBrowser
{
    private const string DriveEndpoint = "https://graph.microsoft.com/v1.0/me/drive";

    private static readonly JsonSerializerOptions TripOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly OneDriveAuthService _auth;
    private readonly HttpClient _http = new();

    public string Kind => "onedrive";
    public string RootName => "OneDrive";
    public bool   AccessDenied { get; private set; }

    public OneDriveStorage(OneDriveAuthService auth) => _auth = auth;

    private static string Item(string id)
        => id == "root" ? $"{DriveEndpoint}/root" : $"{DriveEndpoint}/items/{id}";

    // ── Navegação de pastas ──────────────────────────────────

    public async Task<List<DriveFolder>?> ListFoldersAsync(string parentId)
    {
        var items = await ListChildrenAsync(parentId);
        return items?.Where(i => i.IsFolder)
                     .Select(i => new DriveFolder(i.Id, i.Name))
                     .OrderBy(f => f.Name).ToList();
    }

    // ── ITripStorage ─────────────────────────────────────────

    public async Task<List<TripEntry>> ScanTripsAsync(string repoRef)
    {
        AccessDenied = false;

        // Pré-aquece o token (evita várias renovações concorrentes na rajada abaixo).
        if (await _auth.GetAccessTokenAsync() == null) { AccessDenied = true; return []; }

        var children = await ListChildrenAsync(repoRef);
        if (children == null) return [];

        using var gate = new SemaphoreSlim(6);
        var tasks = children.Where(c => c.IsFolder).Select(async folder =>
        {
            await gate.WaitAsync();
            try
            {
                var meta = await GetChildMetaAsync(folder.Id, "trip.json");
                if (meta?.DownloadUrl == null) return null;
                var trip = await DownloadTripAsync(meta.Value.DownloadUrl);
                if (trip == null) return null;
                return new TripEntry(
                    trip.Title, trip.StartDate?.ToString("yyyy-MM-dd"), meta.Value.Id)
                { FolderUri = folder.Id };
            }
            finally { gate.Release(); }
        });

        var entries = (await Task.WhenAll(tasks)).OfType<TripEntry>();
        return [.. entries.OrderByDescending(t => t.SortKey)];
    }

    public async Task<Trip?> LoadTripAsync(string tripRef)
    {
        var meta = await GetMetaAsync(Item(tripRef));
        return meta?.DownloadUrl == null ? null : await DownloadTripAsync(meta.Value.DownloadUrl);
    }

    public async Task<bool> SaveTripAsync(string tripRef, Trip trip)
    {
        var json = JsonSerializer.Serialize(trip, TripOpts);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var resp = await SendAsync(HttpMethod.Put, $"{Item(tripRef)}/content", content);
        return resp?.IsSuccessStatusCode ?? false;
    }

    public async Task<Stream?> OpenAttachmentAsync(string tripRef, string? folderRef, string filename)
    {
        if (folderRef == null) return null;
        var meta = await GetChildMetaAsync(folderRef, filename);
        if (meta?.DownloadUrl == null) return null;
        try
        {
            var resp = await _http.GetAsync(meta.Value.DownloadUrl);
            return resp.IsSuccessStatusCode ? await resp.Content.ReadAsStreamAsync() : null;
        }
        catch { return null; }
    }

    public async Task<bool> DeleteAttachmentAsync(string tripRef, string? folderRef, string filename)
    {
        if (folderRef == null) return false;
        var meta = await GetChildMetaAsync(folderRef, filename);
        if (meta == null) return false;
        var resp = await SendAsync(HttpMethod.Delete, Item(meta.Value.Id));
        return resp?.IsSuccessStatusCode ?? false;
    }

    // ── Helpers ──────────────────────────────────────────────

    private async Task<List<GraphItem>?> ListChildrenAsync(string parentId)
    {
        var list = new List<GraphItem>();
        string? url = $"{Item(parentId)}/children?$select=id,name,folder&$top=200";
        while (url != null)
        {
            var resp = await SendAsync(HttpMethod.Get, url);
            if (resp == null || !resp.IsSuccessStatusCode) return null;
            try
            {
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStreamAsync());
                if (doc.RootElement.TryGetProperty("value", out var items))
                    foreach (var it in items.EnumerateArray())
                        list.Add(new GraphItem(
                            it.GetProperty("id").GetString()!,
                            it.GetProperty("name").GetString()!,
                            it.TryGetProperty("folder", out _)));
                url = doc.RootElement.TryGetProperty("@odata.nextLink", out var next)
                    ? next.GetString() : null;
            }
            catch { return null; }
        }
        return list;
    }

    /// <summary>Metadados de um filho pelo nome (endereçamento por caminho). Null se não existir.</summary>
    private Task<ItemMeta?> GetChildMetaAsync(string folderId, string name)
        => GetMetaAsync($"{Item(folderId)}:/{Uri.EscapeDataString(name)}");

    private async Task<ItemMeta?> GetMetaAsync(string itemUrl)
    {
        var resp = await SendAsync(HttpMethod.Get, itemUrl);
        if (resp == null || !resp.IsSuccessStatusCode) return null;
        try
        {
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStreamAsync());
            var root = doc.RootElement;
            return new ItemMeta(
                root.GetProperty("id").GetString()!,
                root.TryGetProperty("@microsoft.graph.downloadUrl", out var dl) ? dl.GetString() : null);
        }
        catch { return null; }
    }

    /// <summary>Baixa e desserializa um trip.json pelo link pré-autenticado (sem token).</summary>
    private async Task<Trip?> DownloadTripAsync(string downloadUrl)
    {
        try
        {
            var resp = await _http.GetAsync(downloadUrl);
            if (!resp.IsSuccessStatusCode) return null;
            using var s = await resp.Content.ReadAsStreamAsync();
            return await JsonSerializer.DeserializeAsync<Trip>(s, TripOpts);
        }
        catch { return null; }
    }

    private async Task<HttpResponseMessage?> SendAsync(HttpMethod method, string url, HttpContent? content = null)
    {
        var token = await _auth.GetAccessTokenAsync();
        if (token == null) { AccessDenied = true; return null; }
        try
        {
            using var req = new HttpRequestMessage(method, url) { Content = content };
            req.Headers.Authorization = new("Bearer", token);
            var resp = await _http.SendAsync(req);
            // Só sinaliza acesso perdido em falha real de autenticação (401).
            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                AccessDenied = true;
            return resp;
        }
        catch { return null; }
    }

    private sealed record GraphItem(string Id, string Name, bool IsFolder);
    private readonly record struct ItemMeta(string Id, string? DownloadUrl);
}
