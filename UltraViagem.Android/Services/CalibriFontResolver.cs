using PdfSharp.Fonts;

namespace UltraViagem.Android.Services;

/// <summary>
/// Fornece os bytes da fonte Calibri (embutida no UltraViagem.Core) para o PDFsharp/MigraDoc,
/// já que o Android não tem Calibri instalada no sistema.
/// </summary>
public sealed class CalibriFontResolver : IFontResolver
{
    public static readonly CalibriFontResolver Instance = new();

    private static readonly Dictionary<string, byte[]> _cache = new();

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var face = (isBold, isItalic) switch
        {
            (true, true)  => "Calibri#z",
            (true, false) => "Calibri#b",
            (false, true) => "Calibri#i",
            _             => "Calibri",
        };
        return new FontResolverInfo(face);
    }

    public byte[]? GetFont(string faceName)
    {
        var file = faceName switch
        {
            "Calibri#z" => "calibriz.ttf",
            "Calibri#b" => "calibrib.ttf",
            "Calibri#i" => "calibrii.ttf",
            _           => "calibri.ttf",
        };

        if (_cache.TryGetValue(file, out var cached)) return cached;

        var asm = typeof(global::UltraViagem.Core.Trip).Assembly;
        using var s = asm.GetManifestResourceStream($"UltraViagem.Core.Fonts.{file}")
                      ?? throw new FileNotFoundException($"Fonte embutida não encontrada: {file}");
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        var bytes = ms.ToArray();
        _cache[file] = bytes;
        return bytes;
    }
}
