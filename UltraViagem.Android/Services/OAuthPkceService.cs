using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Maui.Authentication;

namespace UltraViagem.Android.Services;

/// <summary>
/// Base comum para login OAuth 2.0 (fluxo de código + PKCE, cliente público, sem servidor)
/// usando o <see cref="WebAuthenticator"/> do MAUI. O refresh token fica no
/// <see cref="SecureStorage"/>; access tokens são obtidos sob demanda.
/// Cada provedor (Google, Microsoft) só define endpoints, escopo e parâmetros extras.
/// </summary>
public abstract class OAuthPkceService
{
    protected abstract string ClientId        { get; }
    protected abstract string RedirectUri     { get; }
    protected abstract string AuthEndpoint    { get; }
    protected abstract string TokenEndpoint   { get; }
    protected abstract string Scope           { get; }
    protected abstract string RefreshTokenKey { get; }

    /// <summary>Parâmetros extras na URL de autorização (ex.: "&amp;prompt=consent").</summary>
    protected virtual string ExtraAuthParams => "";

    /// <summary>Se o provedor espera o escopo também nas requisições ao endpoint de token (Microsoft).</summary>
    protected virtual bool SendScopeOnTokenRequests => false;

    private readonly HttpClient _http = new();
    private string? _accessToken;
    private DateTimeOffset _accessExpiry = DateTimeOffset.MinValue;

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
            ExtraAuthParams;

        WebAuthenticatorResult result;
        try
        {
            result = await WebAuthenticator.Default.AuthenticateAsync(
                new Uri(authUrl), new Uri(RedirectUri));
        }
        catch (TaskCanceledException) { return false; } // usuário cancelou

        if (!result.Properties.TryGetValue("code", out var code) || string.IsNullOrEmpty(code))
            return false;

        return await RequestTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"]    = "authorization_code",
            ["code"]          = code,
            ["redirect_uri"]  = RedirectUri,
            ["code_verifier"] = verifier,
        });
    }

    /// <summary>
    /// Garante uma sessão: reaproveita o refresh token salvo (sem abrir o navegador) e só
    /// faz o login interativo se não houver sessão válida (nunca logou, revogada ou expirada).
    /// </summary>
    public async Task<bool> EnsureSignedInAsync()
        => await GetAccessTokenAsync() != null || await SignInAsync();

    public void SignOut()
    {
        SecureStorage.Default.Remove(RefreshTokenKey);
        _accessToken  = null;
        _accessExpiry = DateTimeOffset.MinValue;
    }

    /// <summary>Retorna um access token válido (renova via refresh token quando necessário), ou null se não autenticado.</summary>
    public async Task<string?> GetAccessTokenAsync()
    {
        if (_accessToken != null && DateTimeOffset.UtcNow < _accessExpiry.AddSeconds(-60))
            return _accessToken;

        var refresh = await SecureStorage.Default.GetAsync(RefreshTokenKey);
        if (string.IsNullOrEmpty(refresh)) return null;

        var ok = await RequestTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"]    = "refresh_token",
            ["refresh_token"] = refresh,
        });
        return ok ? _accessToken : null;
    }

    private async Task<bool> RequestTokenAsync(Dictionary<string, string> form)
    {
        form["client_id"] = ClientId;
        if (SendScopeOnTokenRequests) form["scope"] = Scope;
        try
        {
            var resp = await _http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form));
            if (!resp.IsSuccessStatusCode) return false;
            var tok = await resp.Content.ReadFromJsonAsync<TokenResponse>();
            if (tok?.access_token == null) return false;

            _accessToken  = tok.access_token;
            _accessExpiry = DateTimeOffset.UtcNow.AddSeconds(tok.expires_in);
            // A Microsoft rotaciona o refresh token a cada renovação; o Google devolve só no login.
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
