using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;

namespace UltraViagem.App;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        var assembly = Assembly.GetExecutingAssembly();
        var version = FileVersionInfo.GetVersionInfo(assembly.Location).ProductVersion;
        VersionText.Text = string.IsNullOrWhiteSpace(version) ? "Aplicativo de planejamento de viagens" : $"Versao {version}";
        BuildDateText.Text = GetBuildDate(assembly).ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("pt-BR"));
    }

    private static DateTime GetBuildDate(Assembly assembly)
    {
        if (!string.IsNullOrWhiteSpace(assembly.Location) && File.Exists(assembly.Location))
        {
            return File.GetLastWriteTime(assembly.Location);
        }

        return DateTime.Now;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
