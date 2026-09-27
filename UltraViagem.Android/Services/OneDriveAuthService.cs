namespace UltraViagem.Android.Services;

/// <summary>
/// Login OAuth com a conta Microsoft (OneDrive), via Microsoft identity platform v2.
/// App registrado no Azure (Entra ID) como cliente público "Aplicativos móveis e da área
/// de trabalho", aceitando contas pessoais e organizacionais (endpoint "common").
/// </summary>
public sealed class OneDriveAuthService : OAuthPkceService
{
    // "ID do aplicativo (cliente)" do registro no Azure.
    private const string MsClientId = "2ba7cefd-3a78-4cf9-8630-3b8e6dbe6df0";
    // Esquema do redirect "msal<clientId>://auth" (registrado no intent-filter do Android).
    public  const string RedirectScheme = "msal" + MsClientId;

    private const string Authority = "https://login.microsoftonline.com/common/oauth2/v2.0";

    protected override string ClientId        => MsClientId;
    protected override string RedirectUri     => RedirectScheme + "://auth";
    protected override string AuthEndpoint    => Authority + "/authorize";
    protected override string TokenEndpoint   => Authority + "/token";
    protected override string Scope           => "Files.ReadWrite offline_access User.Read";
    protected override string RefreshTokenKey => "onedrive_refresh_token";

    protected override string ExtraAuthParams => "&prompt=select_account";
    protected override bool   SendScopeOnTokenRequests => true;
}
