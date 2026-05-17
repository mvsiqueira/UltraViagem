using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace UltraViagem.App;

public sealed class AttachmentIconConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var fileName = values.ElementAtOrDefault(0) as string;
        var tripPath = values.ElementAtOrDefault(1) as string;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var path = string.IsNullOrWhiteSpace(tripPath)
            ? fileName
            : Path.Combine(tripPath, fileName);

        return SystemFileIconProvider.GetIcon(path);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

internal static class SystemFileIconProvider
{
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiLargeIcon = 0x000000000;
    private const uint ShgfiUseFileAttributes = 0x000000010;
    private const uint FileAttributeNormal = 0x00000080;
    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? GetIcon(string path)
    {
        var extension = Path.GetExtension(path);
        var cacheKey = string.IsNullOrWhiteSpace(extension) ? "__file" : extension;
        if (Cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var iconPath = File.Exists(path)
            ? path
            : string.IsNullOrWhiteSpace(extension) ? "file" : extension;
        var flags = ShgfiIcon | ShgfiLargeIcon | (File.Exists(path) ? 0 : ShgfiUseFileAttributes);

        var info = new ShFileInfo();
        var result = SHGetFileInfo(iconPath, FileAttributeNormal, ref info, (uint)Marshal.SizeOf<ShFileInfo>(), flags);
        if (result == IntPtr.Zero || info.IconHandle == IntPtr.Zero)
        {
            Cache[cacheKey] = null;
            return null;
        }

        try
        {
            var image = Imaging.CreateBitmapSourceFromHIcon(
                info.IconHandle,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(32, 32));
            image.Freeze();
            Cache[cacheKey] = image;
            return image;
        }
        finally
        {
            DestroyIcon(info.IconHandle);
        }
    }

    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string path,
        uint fileAttributes,
        ref ShFileInfo fileInfo,
        uint fileInfoSize,
        uint flags);

    [DllImport("User32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public IntPtr IconHandle;
        public int IconIndex;
        public uint Attributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string DisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string TypeName;
    }
}
