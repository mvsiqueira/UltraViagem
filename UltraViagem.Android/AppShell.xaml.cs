namespace UltraViagem.Android;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("trip", typeof(Pages.TripPage));
    }
}
