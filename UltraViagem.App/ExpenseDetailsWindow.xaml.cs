using System.Windows;

namespace UltraViagem.App;

public partial class ExpenseDetailsWindow : Window
{
    public ExpenseDetailsWindow(ExpenseEditorViewModel expense)
    {
        InitializeComponent();
        DataContext = expense;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
