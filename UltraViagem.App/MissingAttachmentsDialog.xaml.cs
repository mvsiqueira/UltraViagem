using System.Windows;

namespace UltraViagem.App;

public enum MissingAttachmentsAction { Remove, Ignore }

public partial class MissingAttachmentsDialog : Window
{
    public MissingAttachmentsAction Result { get; private set; } = MissingAttachmentsAction.Ignore;

    public MissingAttachmentsDialog(IReadOnlyList<string> files)
    {
        InitializeComponent();
        var count = files.Count;
        SubtitleText.Text = count == 1
            ? "1 arquivo listado nos anexos não foi encontrado na pasta da viagem. O que deseja fazer?"
            : $"{count} arquivos listados nos anexos não foram encontrados na pasta da viagem. O que deseja fazer?";
        FileList.ItemsSource = files;
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        Result = MissingAttachmentsAction.Remove;
        Close();
    }

    private void Ignore_Click(object sender, RoutedEventArgs e)
    {
        Result = MissingAttachmentsAction.Ignore;
        Close();
    }
}
