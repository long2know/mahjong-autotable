namespace Mahjong.Autotable.Api.Auth;

/// <summary>
/// Phase J Wave 8 — root configuration section for the Wave 8 auth surface.
///
/// <para>Bound from the <c>Authentication</c> section of <c>appsettings.json</c>:</para>
/// <code>
/// "Authentication": {
///   "Google":          { "Enabled": true, "ClientId": "…", "ClientSecret": "…" },
///   "GitHub":          { "Enabled": true, "ClientId": "…", "ClientSecret": "…" },
///   "EmailMagicLink":  { "Enabled": true, "BaseUrl": "https://…" }
/// }
/// </code>
///
/// <para>A provider is treated as "configured" when its
/// <see cref="OAuthProviderOptions.Enabled"/> is true AND it has a non-empty
/// <see cref="OAuthProviderOptions.ClientId"/> + <see cref="OAuthProviderOptions.ClientSecret"/>.
/// Configured providers surface in <c>GET /api/auth/providers</c>. The
/// dev-login fallback is registered separately when
/// <c>IHostEnvironment.IsDevelopment()</c> is true.</para>
/// </summary>
public sealed class AuthOptions
{
    /// <summary>Google OAuth provider config.</summary>
    public OAuthProviderOptions Google { get; set; } = new();

    /// <summary>GitHub OAuth provider config.</summary>
    public OAuthProviderOptions GitHub { get; set; } = new();

    /// <summary>Email magic-link config.</summary>
    public EmailMagicLinkOptions EmailMagicLink { get; set; } = new();

    /// <summary>Session cookie lifetime. Default 30 days.</summary>
    public int SessionLifetimeDays { get; set; } = 30;

    /// <summary>Magic-link token TTL in minutes. Default 15.</summary>
    public int MagicLinkTtlMinutes { get; set; } = 15;

    /// <summary>
    /// Phase K Wave 1 — HMAC signing key for OAuth state tokens.
    /// When empty, the application mints a per-process random key
    /// (logged once at startup). Production deployments should pin
    /// a stable secret so a rolling restart doesn't invalidate
    /// in-flight authorize redirects. Minimum recommended length:
    /// 32 bytes (256 bits). Hex / base64 / arbitrary string all work
    /// — the secret is hashed into a 256-bit key on use.
    /// </summary>
    public string StateSigningKey { get; set; } = string.Empty;
}

/// <summary>
/// Phase J Wave 8 — per-provider OAuth config. The default authorize /
/// token / userinfo endpoints are hard-coded per provider in
/// <see cref="OAuthService"/> (Google's and GitHub's URLs are stable). The
/// only required tenant-specific values are <see cref="ClientId"/> +
/// <see cref="ClientSecret"/>, both of which are pulled from configuration
/// secrets (env var, k8s secret, user-secrets) — never committed.
/// </summary>
public sealed class OAuthProviderOptions
{
    public bool Enabled { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Optional override for the OAuth authorize endpoint.
    /// Empty falls back to the provider-default in <see cref="OAuthService"/>.</summary>
    public string AuthorizationEndpoint { get; set; } = string.Empty;

    /// <summary>Optional override for the OAuth token endpoint.</summary>
    public string TokenEndpoint { get; set; } = string.Empty;

    /// <summary>Optional override for the userinfo endpoint.</summary>
    public string UserInfoEndpoint { get; set; } = string.Empty;

    /// <summary>Optional override for the requested OAuth scopes (space-delimited).</summary>
    public string Scopes { get; set; } = string.Empty;
}

/// <summary>
/// Phase J Wave 8 — email magic-link config.
/// </summary>
public sealed class EmailMagicLinkOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>Public base URL that the magic-link verify path is appended to.
    /// Defaults to the request's own origin when unset.</summary>
    public string BaseUrl { get; set; } = string.Empty;
}

/// <summary>
/// Phase J Wave 8 — SMTP settings consumed by <see cref="SmtpEmailSender"/>.
/// When <see cref="Host"/> is empty the SMTP sender falls back to the
/// <see cref="LogEmailSender"/> stub so dev / test never need real SMTP.
/// </summary>
public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string User { get; set; } = string.Empty;
    public string Pass { get; set; } = string.Empty;
    public string From { get; set; } = "no-reply@mahjong-autotable.local";
    public bool UseSsl { get; set; } = true;
}
