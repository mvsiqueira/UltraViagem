namespace UltraViagem.Android.Services;

/// <summary>
/// Login OAuth com o Google (Drive). Cliente do tipo "iOS" no Google Cloud (esquema de
/// redirect reverso), porque clientes Android não suportam mais esquema de URI personalizado.
/// </summary>
public sealed class GoogleAuthService : OAuthPkceService
{
    // Cliente OAuth (público — protegido por PKCE, sem segredo).
    private const string GoogleClientId =
        "959043703733-b6nn595b8krsmb3buolflpp31kqrmthn.apps.googleusercontent.com";
    // Esquema reverso do client id (registrado no intent-filter do Android).
    public  const string RedirectScheme =
        "com.googleusercontent.apps.959043703733-b6nn595b8krsmb3buolflpp31kqrmthn";

    protected override string ClientId        => GoogleClientId;
    protected override string RedirectUri     => RedirectScheme + ":/oauth2redirect";
    protected override string AuthEndpoint    => "https://accounts.google.com/o/oauth2/v2/auth";
    protected override string TokenEndpoint   => "https://oauth2.googleapis.com/token";
    protected override string Scope           => "https://www.googleapis.com/auth/drive";
    protected override string RefreshTokenKey => "gdrive_refresh_token";

    // offline + consent: garante o refresh token no login.
    protected override string ExtraAuthParams => "&access_type=offline&prompt=consent";
}
