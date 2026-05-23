using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace Mahjong.Autotable.Api.Auth;

/// <summary>
/// Phase J Wave 8 — OAuth 2.0 helper. Knows the canonical authorize / token
/// / userinfo endpoints for the supported providers (Google, GitHub) and
/// constructs the redirect URLs + handles the code-for-token exchange and
/// userinfo fetch.
///
/// <para>State token: a 32-byte URL-safe nonce stored in a short-lived
/// <c>mahjong_oauth_state</c> cookie. On callback we compare the state
/// query param to the cookie value and refuse mismatches.</para>
///
/// <para>Provider config:
/// <list type="bullet">
///   <item><b>Google</b>: <c>https://accounts.google.com/o/oauth2/v2/auth</c>
///         + <c>https://oauth2.googleapis.com/token</c> +
///         <c>https://www.googleapis.com/oauth2/v3/userinfo</c>. Default scopes:
///         <c>openid profile email</c>.</item>
///   <item><b>GitHub</b>: <c>https://github.com/login/oauth/authorize</c>
///         + <c>https://github.com/login/oauth/access_token</c> +
///         <c>https://api.github.com/user</c>. Default scopes: <c>read:user user:email</c>.</item>
/// </list></para>
/// </summary>
public sealed class OAuthService
{
    public const string StateCookieName = "mahjong_oauth_state";
    public const string ReturnUrlCookieName = "mahjong_oauth_return";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthOptions _options;

    public OAuthService(IHttpClientFactory httpClientFactory, AuthOptions options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    public OAuthProviderOptions GetProviderOptions(string provider) => provider.ToLowerInvariant() switch
    {
        "google" => _options.Google,
        "github" => _options.GitHub,
        _ => new OAuthProviderOptions(),
    };

    public bool IsConfigured(string provider)
    {
        var opts = GetProviderOptions(provider);
        return opts.Enabled
            && !string.IsNullOrWhiteSpace(opts.ClientId)
            && !string.IsNullOrWhiteSpace(opts.ClientSecret);
    }

    public string BuildAuthorizeUrl(string provider, string redirectUri, string state)
    {
        var opts = GetProviderOptions(provider);
        var (authorize, _, _, defaultScopes) = ResolveProviderEndpoints(provider, opts);
        var scopes = string.IsNullOrWhiteSpace(opts.Scopes) ? defaultScopes : opts.Scopes;
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = opts.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = scopes,
            ["state"] = state,
            ["prompt"] = provider.Equals("google", StringComparison.OrdinalIgnoreCase) ? "select_account" : null,
        };
        return QueryHelpers.AddQueryString(authorize, query);
    }

    public async Task<OAuthUserInfo?> ExchangeAndFetchUserInfoAsync(
        string provider,
        string code,
        string redirectUri,
        CancellationToken ct = default)
    {
        var opts = GetProviderOptions(provider);
        var (_, tokenEndpoint, userInfoEndpoint, _) = ResolveProviderEndpoints(provider, opts);

        using var client = _httpClientFactory.CreateClient("oauth");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.UserAgent.ParseAdd("mahjong-autotable/1.0");

        var tokenForm = new Dictionary<string, string>
        {
            ["client_id"] = opts.ClientId,
            ["client_secret"] = opts.ClientSecret,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = redirectUri,
        };
        using var tokenResp = await client.PostAsync(tokenEndpoint, new FormUrlEncodedContent(tokenForm), ct);
        if (!tokenResp.IsSuccessStatusCode) return null;

        var tokenJson = await tokenResp.Content.ReadAsStringAsync(ct);
        string? accessToken;
        try
        {
            using var doc = JsonDocument.Parse(tokenJson);
            if (!doc.RootElement.TryGetProperty("access_token", out var tok)) return null;
            accessToken = tok.GetString();
        }
        catch (JsonException)
        {
            // GitHub may return form-encoded by default; the Accept header
            // above asks for JSON but be defensive.
            return null;
        }

        if (string.IsNullOrEmpty(accessToken)) return null;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var userResp = await client.GetAsync(userInfoEndpoint, ct);
        if (!userResp.IsSuccessStatusCode) return null;

        var userJson = await userResp.Content.ReadAsStringAsync(ct);
        return ParseUserInfo(provider, userJson);
    }

    private static OAuthUserInfo? ParseUserInfo(string provider, string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (provider.Equals("google", StringComparison.OrdinalIgnoreCase))
            {
                var sub = root.TryGetProperty("sub", out var s) ? s.GetString() : null;
                if (string.IsNullOrEmpty(sub)) return null;
                var email = root.TryGetProperty("email", out var e) ? e.GetString() : null;
                var emailVerified = root.TryGetProperty("email_verified", out var ev) && ev.GetBoolean();
                var name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
                return new OAuthUserInfo(sub, email, emailVerified, name);
            }

            if (provider.Equals("github", StringComparison.OrdinalIgnoreCase))
            {
                // GitHub's id is a number; convert to string.
                var idElem = root.TryGetProperty("id", out var i) ? i : default;
                var sub = idElem.ValueKind == JsonValueKind.Number ? idElem.GetInt64().ToString() : idElem.GetString();
                if (string.IsNullOrEmpty(sub)) return null;
                var email = root.TryGetProperty("email", out var e) ? e.GetString() : null;
                var name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (string.IsNullOrWhiteSpace(name) && root.TryGetProperty("login", out var login))
                    name = login.GetString();
                // GitHub doesn't surface email_verified on /user — emails come
                // verified via /user/emails. For Wave 8 we treat any returned
                // email as unverified unless the user later confirms via
                // magic link.
                return new OAuthUserInfo(sub, email, false, name);
            }
        }
        catch (JsonException)
        {
            return null;
        }
        return null;
    }

    private static (string AuthorizeEndpoint, string TokenEndpoint, string UserInfoEndpoint, string DefaultScopes)
        ResolveProviderEndpoints(string provider, OAuthProviderOptions opts)
    {
        if (provider.Equals("google", StringComparison.OrdinalIgnoreCase))
        {
            return (
                string.IsNullOrEmpty(opts.AuthorizationEndpoint) ? "https://accounts.google.com/o/oauth2/v2/auth" : opts.AuthorizationEndpoint,
                string.IsNullOrEmpty(opts.TokenEndpoint) ? "https://oauth2.googleapis.com/token" : opts.TokenEndpoint,
                string.IsNullOrEmpty(opts.UserInfoEndpoint) ? "https://www.googleapis.com/oauth2/v3/userinfo" : opts.UserInfoEndpoint,
                "openid profile email");
        }
        if (provider.Equals("github", StringComparison.OrdinalIgnoreCase))
        {
            return (
                string.IsNullOrEmpty(opts.AuthorizationEndpoint) ? "https://github.com/login/oauth/authorize" : opts.AuthorizationEndpoint,
                string.IsNullOrEmpty(opts.TokenEndpoint) ? "https://github.com/login/oauth/access_token" : opts.TokenEndpoint,
                string.IsNullOrEmpty(opts.UserInfoEndpoint) ? "https://api.github.com/user" : opts.UserInfoEndpoint,
                "read:user user:email");
        }
        return (opts.AuthorizationEndpoint, opts.TokenEndpoint, opts.UserInfoEndpoint, opts.Scopes);
    }

    /// <summary>Generates a CSRF state nonce.</summary>
    public static string GenerateState()
    {
        Span<byte> buf = stackalloc byte[32];
        RandomNumberGenerator.Fill(buf);
        return Convert.ToBase64String(buf)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>Constant-time string comparison for state validation.</summary>
    public static bool ConstantTimeEquals(string a, string b)
    {
        if (a is null || b is null) return false;
        if (a.Length != b.Length) return false;
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}

/// <summary>Phase J Wave 8 — provider-agnostic user info returned by <see cref="OAuthService.ExchangeAndFetchUserInfoAsync"/>.</summary>
public sealed record OAuthUserInfo(string Subject, string? Email, bool EmailVerified, string? DisplayName);
