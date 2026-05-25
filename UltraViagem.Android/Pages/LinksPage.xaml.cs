using UltraViagem.Core;

namespace UltraViagem.Android.Pages;

public partial class LinksPage : ContentPage
{
    public LinksPage() => InitializeComponent();

    private async void OnLinkTapped(object sender, TappedEventArgs e)
    {
        if (sender is Frame { BindingContext: LinkItem link } &&
            Uri.TryCreate(link.Url, UriKind.Absolute, out var uri))
        {
            await Launcher.Default.OpenAsync(uri);
        }
    }
}
