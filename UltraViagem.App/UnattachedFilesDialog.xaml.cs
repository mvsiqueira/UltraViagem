using System.Windows;

namespace UltraViagem.App;

public enum UnattachedFilesAction { Incorporate, Delete, Ignore }

public partial class UnattachedFilesDialog : Window
{
    public UnattachedFilesAction Result { get; private set; } = UnattachedFilesAction.Ignore;

    public UnattachedFilesDialog(IReadOnlyList<string> files)
    {
        InitializeComponent();
        var count = files.Count;
        SubtitleText.Text = count == 1
            ? "A pasta desta viagem contém 1 arquivo que não está listado nos anexos. O que deseja fazer?"
            : $"A pasta desta viagem contém {count} arquivos que não estão listados nos anexos. O que deseja fazer?";
        FileList.ItemsSource = files;
    }

    private void Incorporate_Click(object sender, RoutedEventArgs e)
    {
        Result = UnattachedFilesAction.Incorporate;
        Close();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        Result = UnattachedFilesAction.Delete;
        Close();
    }

    private void Ignore_Click(object sender, RoutedEventArgs e)
    {
        Result = UnattachedFilesAction.Ignore;
        Close();
    }
}
