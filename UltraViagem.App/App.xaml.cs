using System.Windows.Input;

namespace UltraViagem.App;

public partial class App : System.Windows.Application
{
    private void TextBox_TripleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 3 && sender is System.Windows.Controls.TextBox tb)
        {
            tb.SelectAll();
            e.Handled = true;
        }
    }
}
