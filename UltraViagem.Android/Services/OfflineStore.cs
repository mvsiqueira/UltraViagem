using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UltraViagem.Core;

namespace UltraViagem.Android.Services;

/// <summary>
/// Cópias locais das viagens da nuvem para uso sem internet: o <c>trip.json</c> de cada
/// viagem e os anexos baixados pelo botão "Baixar para uso offline".
/// Fica em <see cref="FileSystem.AppDataDirectory"/> (o Android não limpa, ao contrário do cache).
/// As chaves são derivadas do tripRef (id do trip.json no provedor).
/// </summary>
public sealed class OfflineStore
{
    private static readonly JsonSerializerOptions TripOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private static string Root => Path.Combine(FileSystem.AppDataDirectory, "offline");

    /// <summary>Há acesso à internet agora.</summary>
    public static bool IsOnline => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

    private static string Key(string tripRef)
        => Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(tripRef)))[..16];

    private static string TripPath(string tripRef)      => Path.Combine(Root, "trips", Key(tripRef) + ".json");
    private static string AttachmentDir(string tripRef) => Path.Combine(Root, "attachments", Key(tripRef));
    private static string AttachmentFile(string tripRef, string filename)
        => Path.Combine(AttachmentDir(tripRef), Path.GetFileName(filename));

    // ── Viagem (trip.json) ───────────────────────────────────

    public void SaveTrip(string tripRef, Trip trip)
    {
        try
        {
            var path = TripPath(tripRef);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(trip, TripOpts));
            File.Move(tmp, path, overwrite: true);
        }
        catch { }
    }

    public Trip? LoadTrip(string tripRef)
    {
        try
        {
            var path = TripPath(tripRef);
            return File.Exists(path)
                ? JsonSerializer.Deserialize<Trip>(File.ReadAllText(path), TripOpts)
                : null;
        }
        catch { return null; }
    }

    // ── Anexos ───────────────────────────────────────────────

    /// <summary>Caminho da cópia offline do anexo, ou null se não foi baixado.</summary>
    public string? GetAttachment(string tripRef, string filename)
    {
        var path = AttachmentFile(tripRef, filename);
        return File.Exists(path) ? path : null;
    }

    /// <summary>Grava a cópia offline de um anexo (escrita atômica: só aparece se completou).</summary>
    public async Task<bool> SaveAttachmentAsync(string tripRef, string filename, Stream content)
    {
        var path = AttachmentFile(tripRef, filename);
        var tmp  = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using (var dst = File.Create(tmp))
                await content.CopyToAsync(dst);
            File.Move(tmp, path, overwrite: true);
            return true;
        }
        catch
        {
            try { File.Delete(tmp); } catch { }
            return false;
        }
    }

    public void DeleteAttachment(string tripRef, string filename)
    {
        try { File.Delete(AttachmentFile(tripRef, filename)); } catch { }
    }

    /// <summary>Quantos dos anexos listados já têm cópia offline.</summary>
    public int CountAttachments(string tripRef, IEnumerable<string> filenames)
        => filenames.Count(f => GetAttachment(tripRef, f) != null);
}
