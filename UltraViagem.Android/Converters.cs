using System.Globalization;

namespace UltraViagem.Android;

/// <summary>Converte string hex "#RRGGBB" em Color do MAUI.</summary>
public sealed class HexToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
            try { return Color.FromArgb(hex); } catch { }
        return Colors.Transparent;
    }
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
}

/// <summary>Converte string hex em cor de texto com contraste adequado (#111827 ou branco).</summary>
public sealed class HexToContrastColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex)
        {
            hex = hex.TrimStart('#');
            if (hex.Length >= 6)
            {
                int r = System.Convert.ToInt32(hex[0..2], 16);
                int g = System.Convert.ToInt32(hex[2..4], 16);
                int b = System.Convert.ToInt32(hex[4..6], 16);
                double brightness = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
                return brightness > 0.55 ? Color.FromArgb("#111827") : Colors.White;
            }
        }
        return Color.FromArgb("#111827");
    }
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
}

/// <summary>Retorna true se int > 0.</summary>
public sealed class CountToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int i && i > 0;
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
}

/// <summary>Inverte bool.</summary>
public sealed class InvertBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
}

/// <summary>Retorna true se string não nula/vazia.</summary>
public sealed class StringToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrWhiteSpace(s);
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
}

/// <summary>Extrai a extensão de um nome de arquivo e retorna em maiúsculas (ex: "PDF").</summary>
public sealed class FileExtLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string filename) return "?";
        var ext = Path.GetExtension(filename).TrimStart('.').ToUpperInvariant();
        return ext.Length > 4 ? ext[..4] : (ext.Length > 0 ? ext : "?");
    }
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
}

/// <summary>Mapeia extensão de arquivo para uma cor de badge.</summary>
public sealed class FileExtColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var ext = (value is string s ? Path.GetExtension(s).TrimStart('.').ToLowerInvariant() : "");
        return ext switch
        {
            "pdf"                           => Color.FromArgb("#EF4444"),
            "doc" or "docx"                 => Color.FromArgb("#3B82F6"),
            "xls" or "xlsx"                 => Color.FromArgb("#22C55E"),
            "png" or "jpg" or "jpeg"
                or "gif" or "webp" or "svg" => Color.FromArgb("#8B5CF6"),
            "mp4" or "mov" or "avi"
                or "mkv"                    => Color.FromArgb("#EC4899"),
            "zip" or "rar" or "7z"          => Color.FromArgb("#F97316"),
            _                               => Color.FromArgb("#6B7280"),
        };
    }
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
}
