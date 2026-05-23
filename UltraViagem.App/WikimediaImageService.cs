using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UltraViagem.App;

/// <summary>
/// Busca imagens no Wikimedia Commons/Wikipedia sem necessidade de API key.
/// Resultado é cacheado localmente na pasta .cache/ da viagem.
/// O parâmetro offset permite obter resultados diferentes a cada reload.
/// </summary>
public static class WikimediaImageService
{
    private static readonly HttpClient _http;
    private static readonly SemaphoreSlim _throttle = new(2);

    static WikimediaImageService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        _http.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent", "UltraViagem/1.0 (travel planner; contact: negrume@gmail.com)");
    }

    /// <summary>
    /// Retorna o caminho local da imagem (baixando e cacheando se necessário).
    /// O offset desloca o resultado da busca — use valores crescentes para obter imagens diferentes.
    /// Retorna null se não encontrar imagem ou ocorrer falha de rede.
    /// </summary>
    public static async Task<string?> FetchAndCacheAsync(string query, string cacheDir, int offset = 0)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;

        Directory.CreateDirectory(cacheDir);

        // Cache key inclui o offset para que cada "página" fique em arquivo separado
        var hash = Convert.ToHexString(
            MD5.HashData(Encoding.UTF8.GetBytes(query.ToLowerInvariant())))[..10];
        var cachedPath = Path.Combine(cacheDir, $"day-{hash}-{offset}.jpg");
        if (File.Exists(cachedPath)) return cachedPath;

        await _throttle.WaitAsync();
        try
        {
            // Passo 1: busca com sroffset para obter resultado diferente por offset
            var searchUrl = "https://en.wikipedia.org/w/api.php"
                          + "?action=query&list=search&format=json&srlimit=1&srnamespace=0"
                          + "&sroffset=" + offset
                          + "&srsearch=" + Uri.EscapeDataString(query);

            var searchJson = await _http.GetStringAsync(searchUrl);
            using var searchDoc = JsonDocument.Parse(searchJson);
            var results = searchDoc.RootElement.GetProperty("query").GetProperty("search");
            if (results.GetArrayLength() == 0) return null;

            var pageTitle = results[0].GetProperty("title").GetString();
            if (string.IsNullOrEmpty(pageTitle)) return null;

            // Passo 2: obtém thumbnail via REST summary
            var summaryUrl = "https://en.wikipedia.org/api/rest_v1/page/summary/"
                           + Uri.EscapeDataString(pageTitle);

            var summaryJson = await _http.GetStringAsync(summaryUrl);
            using var summaryDoc = JsonDocument.Parse(summaryJson);

            if (!summaryDoc.RootElement.TryGetProperty("thumbnail", out var thumbnail)) return null;
            if (!thumbnail.TryGetProperty("source", out var sourceEl)) return null;

            var imageUrl = sourceEl.GetString();
            if (string.IsNullOrEmpty(imageUrl)) return null;

            // Passo 3: baixa e salva no cache
            var imageBytes = await _http.GetByteArrayAsync(imageUrl);
            await File.WriteAllBytesAsync(cachedPath, imageBytes);
            return cachedPath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WikimediaImageService] Erro ao buscar '{query}' (offset={offset}): {ex.Message}");
            return null;
        }
        finally
        {
            _throttle.Release();
        }
    }
}
