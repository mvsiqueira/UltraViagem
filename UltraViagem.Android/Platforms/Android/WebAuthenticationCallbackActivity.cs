using Android.App;
using Android.Content;
using Android.Content.PM;
using UltraViagem.Android.Services;

namespace UltraViagem.Android;

// Intercepta os redirects do OAuth (Google: esquema reverso do client id;
// Microsoft: msal<clientId>) e devolve o resultado ao WebAuthenticator.
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = GoogleAuthService.RedirectScheme)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = OneDriveAuthService.RedirectScheme)]
public class WebAuthenticationCallbackActivity : Microsoft.Maui.Authentication.WebAuthenticatorCallbackActivity
{
}
