using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Maui.Authentication;

namespace UltraViagem.Android.Services;

/// <summary>
/// Autenticação OAuth 2.0 com o Google (fluxo de código + PKCE, sem servidor),
/// usando o <see cref="WebAuthenticator"/> do MAUI. O refresh token é guardado
/// no <see cref="SecureStorage"/>; access tokens são obtidos sob demanda.
///
/// Cliente do tipo "iOS" no Google Cloud (esquema de redirect reverso), porque
/// clientes Android não suportam mais esquema de URI personalizado.
/// </summary>
public sealed class GoogleAuthService
{
    // Cliente OAuth (público — protegido por PKCE, sem segredo).
    private const string ClientId =
        "959043703733-b6nn595b8krsmb3buolflpp31kqrmthn.apps.googleusercontent.com";
    // Esquema reverso do client id (registrado no intent-filter do Android).
    public  const string RedirectScheme =
        "com.googleusercontent.apps.959043703733-b6nn595b8krsmb3buolflpp31kqrmthn";
    private const string RedirectUri = RedirectScheme + ":/oauth2redirect";

    private const string AuthEndpoint  = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string Scope         = "https://www.googleapis.com/auth/drive";

    private const string RefreshTokenKey = "gdrive_refresh_token";

    private readonly HttpClient _http = new();

    private string? _accessToken;
    private DateTimeOffset _accessExpiry = DateTimeOffset.MinValue;

    public bool IsSignedIn =>
        !string.IsNullOrEmpty(SecureStorage.Default.GetAsync(RefreshTokenKey).GetAwaiter().GetResult());

    /// <summary>Faz login interativo (abre o navegador). Retorna true se autorizado.</summary>
    public async Task<bool> SignInAsync()
    {
        var verifier  = CreateCodeVerifier();
        var challenge = CreateCodeChallenge(verifier);

        var authUrl =
            $"{AuthEndpoint}?client_id={Uri.EscapeDataString(ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
            $"&response_type=code" +
            $"&scope={Uri.EscapeDataString(Scope)}" +
            $"&code_challenge={challenge}&code_challenge_method=S256" +
            $"&access_type=offline&prompt=consent";

        WebAuthenticatorResult result;
        try
        {
            result = await WebAuthenticator.Default.AuthenticateAsync(
                new Uri(authUrl), new Uri(RedirectUri));
        }
        catch (TaskCanceledException) { return false; } // usuário cancelou

        if (!result.Properties.TryGetValue("code", out var code) || string.IsNullOrEmpty(code))
            return false;

        return await ExchangeCodeAsync(code, verifier);
    }

    public void SignOut()
    {
        SecureStorage.Default.Remove(RefreshTokenKey);
        _accessToken = null;
        _accessExpiry = DateTimeOffset.MinValue;
    }

    /// <summary>Retorna um access token válido (renova via refresh token quando necessário), ou null se não autenticado.</summary>
    public async Task<string?> GetAccessTokenAsync()
    {
        if (_accessToken != null && DateTimeOffset.UtcNow < _accessExpiry.AddSeconds(-60))
            return _accessToken;

        var refresh = await SecureStorage.Default.GetAsync(RefreshTokenKey);
        if (string.IsNullOrEmpty(refresh)) return null;

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"]     = ClientId,
            ["grant_type"]    = "refresh_token",
            ["refresh_token"] = refresh,
        });
        try
        {
            var resp = await _http.PostAsync(TokenEndpoint, form);
            if (!resp.IsSuccessStatusCode) return null;
            var tok = await resp.Content.ReadFromJsonAsync<TokenResponse>();
            if (tok?.access_token == null) return null;
            _accessToken  = tok.access_token;
            _accessExpiry = DateTimeOffset.UtcNow.AddSeconds(tok.expires_in);
            return _accessToken;
        }
        catch { return null; }
    }

    private async Task<bool> ExchangeCodeAsync(string code, string verifier)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"]     = ClientId,
            ["grant_type"]    = "authorization_code",
            ["code"]          = code,
            ["redirect_uri"]  = RedirectUri,
            ["code_verifier"] = verifier,
        });
        try
        {
            var resp = await _http.PostAsync(TokenEndpoint, form);
            if (!resp.IsSuccessStatusCode) return false;
            var tok = await resp.Content.ReadFromJsonAsync<TokenResponse>();
            if (tok?.access_token == null) return false;

            _accessToken  = tok.access_token;
            _accessExpiry = DateTimeOffset.UtcNow.AddSeconds(tok.expires_in);
            if (!string.IsNullOrEmpty(tok.refresh_token))
                await SecureStorage.Default.SetAsync(RefreshTokenKey, tok.refresh_token);
            return true;
        }
        catch { return false; }
    }

    // ── PKCE ────────────────────────────────────────────────
    private static string CreateCodeVerifier()
        => Base64Url(RandomNumberGenerator.GetBytes(32));

    private static string CreateCodeChallenge(string verifier)
        => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class TokenResponse
    {
        public string? access_token { get; set; }
        public string? refresh_token { get; set; }
        public int expires_in { get; set; }
    }
}
