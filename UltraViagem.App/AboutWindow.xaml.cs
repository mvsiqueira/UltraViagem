using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;

namespace UltraViagem.App;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        var exePath = Environment.ProcessPath ?? string.Empty;
        var version = !string.IsNullOrWhiteSpace(exePath) ? FileVersionInfo.GetVersionInfo(exePath).ProductVersion : null;
        VersionText.Text = string.IsNullOrWhiteSpace(version) ? "Aplicativo de planejamento de viagens" : $"Versao {version}";
        BuildDateText.Text = GetBuildDate(exePath).ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("pt-BR"));
    }

    private static DateTime GetBuildDate(string exePath)
    {
        if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
        {
            return File.GetLastWriteTime(exePath);
        }

        return DateTime.Now;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
