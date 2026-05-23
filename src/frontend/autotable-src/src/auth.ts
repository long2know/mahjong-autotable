// Phase J Wave 8 — Auth UI module.
//
// Wires the sign-in modal, the magic-link landing page, the
// lobby-header auth status chip, and the "Linked accounts" section
// of the Wave-7 profile page.
//
// ── Wire contract with Bishop ──────────────────────────────────────
//
// All endpoints are *feature-detected*: a 404 on `GET /api/auth/providers`
// degrades gracefully to an "Auth coming soon" placeholder so the
// frontend never blocks waiting on Bishop's Wave-8 backend merge.
//
//   GET  /api/auth/providers
//     → 200  { providers: ["google", "github", "email"] }
//     → 404  Auth subsystem not deployed yet
//
//   GET  /api/auth/me
//     → 200  { authenticated: true,
//              email: "stephen@example.com",
//              primaryProvider: "github",
//              identities: [{ provider: "github", subject: "12345",
//                             email: "stephen@example.com" }] }
//     → 200  { authenticated: false }
//     → 404  Auth subsystem not deployed yet
//
//   POST /api/auth/oauth/{provider}/start
//     body: { returnUrl: "<absolute or relative URL>" }
//     → 200  { authorizeUrl: "https://accounts.google.com/o/oauth2/..." }
//        The frontend navigates `window.location.href = authorizeUrl`.
//
//   POST /api/auth/email/start
//     body: { email: "stephen@example.com",
//             returnUrl: "<absolute URL>" }
//     → 200  { sent: true }       (don't reveal account existence in 200)
//     → 400  { error: "Invalid email." }
//     → 429  rate limit
//
//   GET  /api/auth/email/verify?token=...
//     → 200  { authenticated: true, email: "..." }
//     → 400  { error: "Token expired." }
//     → 404  Auth not deployed
//
//   POST /api/auth/link/{provider}
//     body (email only): { email: "..." }
//     OAuth providers: returns { authorizeUrl } for re-entry with link
//     intent.
//
//   POST /api/auth/unlink/{provider}
//     → 200  { unlinked: true, identities: [...] }
//     → 409  { error: "Cannot unlink the only sign-in method." }
//
//   POST /api/auth/logout
//     → 204
//
//   POST /api/auth/dev-login         (Development env ONLY)
//     body: { email: "...", provider?: "..." }
//     Used by E2E to skip the real OAuth round-trip.
//
// Persistence:
//   • Server-side cookie owns the session.  Frontend does NOT cache
//     the session token in localStorage.
//   • LS key `mahjong.auth.last-email.v1` remembers the last email
//     used so the modal can pre-populate on revisits.
//   • LS key `mahjong.auth.cache.v1` (best-effort) caches the last
//     `{ authenticated, email, primaryProvider }` so the lobby chip
//     paints immediately on load — re-validated via `/api/auth/me`.

import { EventEmitter } from 'events';

// ── Public types ────────────────────────────────────────────────────

export type AuthProviderId = 'google' | 'github' | 'email';

export interface AuthIdentity {
  provider: AuthProviderId;
  subject: string;
  email: string | null;
}

export interface AuthState {
  authenticated: boolean;
  email: string | null;
  primaryProvider: AuthProviderId | null;
  identities: AuthIdentity[];
  /** True when the GET /api/auth/providers feature-detect 404s. */
  serverHasAuth: boolean;
  /** Providers offered by the server (intersected with the trio we know). */
  availableProviders: AuthProviderId[];
}

// ── Constants ───────────────────────────────────────────────────────

const KNOWN_PROVIDERS: ReadonlyArray<AuthProviderId> = ['google', 'github', 'email'];

const LS_KEY_LAST_EMAIL = 'mahjong.auth.last-email.v1';
const LS_KEY_AUTH_CACHE = 'mahjong.auth.cache.v1';

const ENDPOINT_PROVIDERS = '/api/auth/providers';
const ENDPOINT_ME = '/api/auth/me';
const ENDPOINT_EMAIL_START = '/api/auth/email/start';
const ENDPOINT_EMAIL_VERIFY = '/api/auth/email/verify';
const ENDPOINT_LOGOUT = '/api/auth/logout';
const oauthStartEndpoint = (provider: AuthProviderId): string =>
  `/api/auth/oauth/${provider}/start`;
const linkEndpoint = (provider: AuthProviderId): string =>
  `/api/auth/link/${provider}`;
const unlinkEndpoint = (provider: AuthProviderId): string =>
  `/api/auth/unlink/${provider}`;

// ── Module state ────────────────────────────────────────────────────

const events = new EventEmitter();

let state: AuthState = {
  authenticated: false,
  email: null,
  primaryProvider: null,
  identities: [],
  serverHasAuth: false,
  availableProviders: [],
};

let installed = false;
let bootPromise: Promise<AuthState> | null = null;

// ── LS helpers ──────────────────────────────────────────────────────

function readLastEmail(): string {
  try {
    return window.localStorage.getItem(LS_KEY_LAST_EMAIL) ?? '';
  } catch {
    return '';
  }
}

function writeLastEmail(email: string): void {
  try {
    window.localStorage.setItem(LS_KEY_LAST_EMAIL, email);
  } catch { /* skip */ }
}

function readAuthCache(): Partial<AuthState> | null {
  try {
    const raw = window.localStorage.getItem(LS_KEY_AUTH_CACHE);
    if (raw === null) return null;
    return JSON.parse(raw) as Partial<AuthState>;
  } catch {
    return null;
  }
}

function writeAuthCache(s: AuthState): void {
  try {
    window.localStorage.setItem(LS_KEY_AUTH_CACHE, JSON.stringify({
      authenticated: s.authenticated,
      email: s.email,
      primaryProvider: s.primaryProvider,
    }));
  } catch { /* skip */ }
}

// ── Public API ──────────────────────────────────────────────────────

export function getAuthState(): AuthState {
  return { ...state, identities: [...state.identities],
           availableProviders: [...state.availableProviders] };
}

export function onAuth(handler: (s: AuthState) => void): () => void {
  events.on('auth', handler);
  return () => events.off('auth', handler);
}

function setState(next: Partial<AuthState>): void {
  state = { ...state, ...next };
  writeAuthCache(state);
  events.emit('auth', state);
  renderLobbyChip();
  renderLinkedAccountsSection();
}

// ── Provider intersection helper ────────────────────────────────────

function intersectProviders(raw: unknown): AuthProviderId[] {
  if (!Array.isArray(raw)) return [];
  const lower = raw.map((p) => String(p).toLowerCase());
  return KNOWN_PROVIDERS.filter((p) => lower.indexOf(p) !== -1);
}

function coerceProvider(raw: unknown): AuthProviderId | null {
  if (typeof raw !== 'string') return null;
  const s = raw.toLowerCase();
  if (s === 'google' || s === 'github' || s === 'email') return s;
  return null;
}

function coerceIdentities(raw: unknown): AuthIdentity[] {
  if (!Array.isArray(raw)) return [];
  const out: AuthIdentity[] = [];
  for (const item of raw) {
    if (item === null || typeof item !== 'object') continue;
    const o = item as Record<string, unknown>;
    const p = coerceProvider(o.provider);
    if (p === null) continue;
    const subject = typeof o.subject === 'string'
      ? o.subject
      : (typeof o.Subject === 'string' ? o.Subject : '');
    const email = typeof o.email === 'string'
      ? o.email
      : (typeof o.Email === 'string' ? o.Email : null);
    out.push({ provider: p, subject, email });
  }
  return out;
}

function normaliseMe(raw: unknown): Partial<AuthState> {
  if (raw === null || typeof raw !== 'object') {
    return { authenticated: false, email: null, primaryProvider: null, identities: [] };
  }
  const o = raw as Record<string, unknown>;
  const authenticated = o.authenticated === true || o.Authenticated === true;
  if (!authenticated) {
    return { authenticated: false, email: null, primaryProvider: null, identities: [] };
  }
  return {
    authenticated: true,
    email: typeof o.email === 'string' ? o.email
      : (typeof o.Email === 'string' ? o.Email : null),
    primaryProvider: coerceProvider(o.primaryProvider ?? o.PrimaryProvider),
    identities: coerceIdentities(o.identities ?? o.Identities),
  };
}

// ── Bootstrap ───────────────────────────────────────────────────────

/**
 * Bootstrap auth.  Idempotent — concurrent callers share the same
 * in-flight pair of GETs.  When `/api/auth/providers` returns 404 we
 * paint the "Auth coming soon" placeholder and skip the `/me` probe.
 */
export async function bootstrapAuth(): Promise<AuthState> {
  if (bootPromise !== null) return bootPromise;
  // Seed from cache so the chip paints synchronously on revisits.
  const cached = readAuthCache();
  if (cached !== null) {
    setState({
      authenticated: cached.authenticated === true,
      email: typeof cached.email === 'string' ? cached.email : null,
      primaryProvider: coerceProvider(cached.primaryProvider),
    });
  }
  bootPromise = (async () => {
    try {
      const providersResp = await fetch(ENDPOINT_PROVIDERS, {
        method: 'GET',
        credentials: 'include',
        headers: { Accept: 'application/json' },
      });
      if (providersResp.status === 404) {
        setState({ serverHasAuth: false, availableProviders: [],
                   authenticated: false, email: null, primaryProvider: null,
                   identities: [] });
        return state;
      }
      if (!providersResp.ok) {
        setState({ serverHasAuth: false, availableProviders: [] });
        return state;
      }
      const providersJson = await providersResp.json() as Record<string, unknown>;
      const available = intersectProviders(providersJson.providers ?? providersJson.Providers);
      setState({ serverHasAuth: true, availableProviders: available });
    } catch {
      setState({ serverHasAuth: false, availableProviders: [] });
      return state;
    }
    try {
      const meResp = await fetch(ENDPOINT_ME, {
        method: 'GET',
        credentials: 'include',
        headers: { Accept: 'application/json' },
      });
      if (meResp.status === 404) {
        setState({ serverHasAuth: false });
        return state;
      }
      if (!meResp.ok) {
        return state;
      }
      const me = await meResp.json() as unknown;
      setState(normaliseMe(me));
    } catch {
      /* keep cached state */
    }
    return state;
  })();
  return bootPromise;
}

/** Force a re-fetch of `/api/auth/me` (e.g. after link/unlink). */
export async function refreshAuth(): Promise<AuthState> {
  if (!state.serverHasAuth) return state;
  try {
    const r = await fetch(ENDPOINT_ME, {
      method: 'GET',
      credentials: 'include',
      headers: { Accept: 'application/json' },
    });
    if (r.ok) {
      const me = await r.json() as unknown;
      setState(normaliseMe(me));
    }
  } catch { /* swallow */ }
  return state;
}

// ── OAuth start ─────────────────────────────────────────────────────

async function startOAuth(
  provider: AuthProviderId,
  intent: 'login' | 'link',
): Promise<{ ok: boolean; error: string | null }> {
  const url = intent === 'link' ? linkEndpoint(provider) : oauthStartEndpoint(provider);
  try {
    const resp = await fetch(url, {
      method: 'POST',
      credentials: 'include',
      headers: {
        Accept: 'application/json',
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        returnUrl: window.location.href,
      }),
    });
    if (resp.status === 404) {
      return { ok: false, error: 'Auth not available yet.' };
    }
    if (!resp.ok) {
      return { ok: false, error: `Sign-in failed (${resp.status}).` };
    }
    const body = await resp.json() as { authorizeUrl?: string };
    if (typeof body.authorizeUrl === 'string' && body.authorizeUrl !== '') {
      window.location.href = body.authorizeUrl;
      return { ok: true, error: null };
    }
    // Some link flows respond 200 without a redirect (already linked).
    void refreshAuth();
    return { ok: true, error: null };
  } catch (e) {
    return { ok: false, error: e instanceof Error ? e.message : String(e) };
  }
}

// ── Magic-link email ───────────────────────────────────────────────

async function startEmailMagicLink(
  email: string,
): Promise<{ ok: boolean; error: string | null }> {
  try {
    const resp = await fetch(ENDPOINT_EMAIL_START, {
      method: 'POST',
      credentials: 'include',
      headers: {
        Accept: 'application/json',
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        email,
        returnUrl: window.location.origin + window.location.pathname,
      }),
    });
    if (resp.status === 404) {
      return { ok: false, error: 'Email sign-in is not available yet.' };
    }
    if (resp.status === 429) {
      return { ok: false, error: 'Too many requests — try again in a minute.' };
    }
    if (!resp.ok) {
      return { ok: false, error: `Failed to send magic link (${resp.status}).` };
    }
    writeLastEmail(email);
    return { ok: true, error: null };
  } catch (e) {
    return { ok: false, error: e instanceof Error ? e.message : String(e) };
  }
}

async function verifyEmailToken(
  token: string,
): Promise<{ ok: boolean; email: string | null; error: string | null }> {
  try {
    const resp = await fetch(`${ENDPOINT_EMAIL_VERIFY}?token=${encodeURIComponent(token)}`, {
      method: 'GET',
      credentials: 'include',
      headers: { Accept: 'application/json' },
    });
    if (resp.status === 404) {
      return { ok: false, email: null, error: 'Auth subsystem unavailable.' };
    }
    if (!resp.ok) {
      const body = await resp.json().catch(() => null) as { error?: string } | null;
      return { ok: false, email: null,
               error: body?.error ?? `Verification failed (${resp.status}).` };
    }
    const body = await resp.json() as { email?: string; authenticated?: boolean };
    void refreshAuth();
    return { ok: true, email: typeof body.email === 'string' ? body.email : null, error: null };
  } catch (e) {
    return { ok: false, email: null, error: e instanceof Error ? e.message : String(e) };
  }
}

// ── Unlink / Logout ────────────────────────────────────────────────

export async function unlinkProvider(
  provider: AuthProviderId,
): Promise<{ ok: boolean; error: string | null }> {
  try {
    const resp = await fetch(unlinkEndpoint(provider), {
      method: 'POST',
      credentials: 'include',
      headers: { Accept: 'application/json' },
    });
    if (!resp.ok) {
      const body = await resp.json().catch(() => null) as { error?: string } | null;
      return { ok: false, error: body?.error ?? `Unlink failed (${resp.status}).` };
    }
    await refreshAuth();
    return { ok: true, error: null };
  } catch (e) {
    return { ok: false, error: e instanceof Error ? e.message : String(e) };
  }
}

export async function logout(): Promise<void> {
  try {
    await fetch(ENDPOINT_LOGOUT, { method: 'POST', credentials: 'include' });
  } catch { /* swallow */ }
  setState({
    authenticated: false,
    email: null,
    primaryProvider: null,
    identities: [],
  });
}

// ── DOM install — modal + chip + landing page ──────────────────────

export function installAuthUi(): void {
  if (installed) return;
  installed = true;
  // The lobby chip + modal + landing page markup is mounted in
  // index.html; we just wire the listeners here.
  wireSignInButton();
  wireSignInModal();
  wireLogoutButton();
  wireMagicLinkLanding();
  renderLobbyChip();
  renderLinkedAccountsSection();
  onAuth(() => {
    renderLobbyChip();
    renderLinkedAccountsSection();
  });
  // Auto-bootstrap once the DOM is wired.
  void bootstrapAuth();
}

function wireSignInButton(): void {
  const btn = document.getElementById('signin-button');
  if (btn === null) return;
  btn.addEventListener('click', (e) => {
    e.preventDefault();
    openSignInModal();
  });
}

function wireLogoutButton(): void {
  const btn = document.getElementById('logout-button');
  if (btn === null) return;
  btn.addEventListener('click', (e) => {
    e.preventDefault();
    void logout();
  });
}

// ── Sign-in modal ──────────────────────────────────────────────────

function openSignInModal(): void {
  const modal = document.getElementById('signin-modal');
  if (modal === null) return;
  modal.classList.add('signin-modal-open');
  modal.setAttribute('aria-hidden', 'false');
  // Reset to the providers panel each time.
  showSignInPanel('providers');
  refreshModalProvidersVisibility();
  // Pre-populate the email input from LS.
  const emailInput = document.getElementById('signin-email-input') as HTMLInputElement | null;
  if (emailInput !== null && emailInput.value === '') {
    emailInput.value = readLastEmail();
  }
  // Focus the dialog so screen readers land on it.
  window.setTimeout(() => {
    const closeBtn = document.getElementById('signin-modal-close');
    closeBtn?.focus();
  }, 50);
}

function closeSignInModal(): void {
  const modal = document.getElementById('signin-modal');
  if (modal === null) return;
  modal.classList.remove('signin-modal-open');
  modal.setAttribute('aria-hidden', 'true');
}

type ModalPanel = 'providers' | 'email-success' | 'placeholder';

function showSignInPanel(panel: ModalPanel): void {
  for (const id of ['signin-providers-panel', 'signin-email-success', 'signin-placeholder']) {
    const el = document.getElementById(id);
    if (el === null) continue;
    el.style.display = 'none';
  }
  const map: Record<ModalPanel, string> = {
    providers: 'signin-providers-panel',
    'email-success': 'signin-email-success',
    placeholder: 'signin-placeholder',
  };
  const target = document.getElementById(map[panel]);
  if (target !== null) target.style.display = '';
}

function refreshModalProvidersVisibility(): void {
  if (!state.serverHasAuth) {
    showSignInPanel('placeholder');
    return;
  }
  const available = state.availableProviders;
  for (const provider of KNOWN_PROVIDERS) {
    const btn = document.getElementById(`signin-${provider}`);
    const emailForm = document.getElementById('signin-email-form');
    if (provider === 'email') {
      if (emailForm !== null) {
        emailForm.style.display = available.indexOf('email') !== -1 ? '' : 'none';
      }
      continue;
    }
    if (btn !== null) {
      btn.style.display = available.indexOf(provider) !== -1 ? '' : 'none';
    }
  }
}

function wireSignInModal(): void {
  const closeBtn = document.getElementById('signin-modal-close');
  if (closeBtn !== null) {
    closeBtn.addEventListener('click', () => closeSignInModal());
  }
  const backdrop = document.getElementById('signin-modal-backdrop');
  if (backdrop !== null) {
    backdrop.addEventListener('click', () => closeSignInModal());
  }
  document.addEventListener('keydown', (e) => {
    if (e.key !== 'Escape') return;
    const modal = document.getElementById('signin-modal');
    if (modal !== null && modal.classList.contains('signin-modal-open')) {
      closeSignInModal();
    }
  });

  for (const provider of ['google', 'github'] as const) {
    const btn = document.getElementById(`signin-${provider}`) as HTMLButtonElement | null;
    if (btn === null) continue;
    btn.addEventListener('click', async () => {
      btn.disabled = true;
      const original = btn.textContent;
      btn.textContent = `Redirecting to ${provider}…`;
      const result = await startOAuth(provider, 'login');
      btn.disabled = false;
      btn.textContent = original;
      if (!result.ok) {
        setModalError(result.error ?? 'Sign-in failed.');
      }
    });
  }

  const emailInput = document.getElementById('signin-email-input') as HTMLInputElement | null;
  const emailSubmit = document.getElementById('signin-email-submit') as HTMLButtonElement | null;
  const emailError = document.getElementById('signin-email-error');
  if (emailInput !== null && emailSubmit !== null) {
    const submit = async (): Promise<void> => {
      const email = (emailInput.value ?? '').trim();
      if (email === '' || !/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(email)) {
        if (emailError !== null) emailError.textContent = 'Enter a valid email address.';
        emailInput.focus();
        return;
      }
      if (emailError !== null) emailError.textContent = '';
      emailSubmit.disabled = true;
      const prev = emailSubmit.textContent;
      emailSubmit.textContent = 'Sending…';
      const result = await startEmailMagicLink(email);
      emailSubmit.disabled = false;
      emailSubmit.textContent = prev;
      if (result.ok) {
        const successEmailEl = document.getElementById('signin-email-success-email');
        if (successEmailEl !== null) successEmailEl.textContent = email;
        showSignInPanel('email-success');
      } else if (emailError !== null) {
        emailError.textContent = result.error ?? 'Failed to send link.';
      }
    };
    emailSubmit.addEventListener('click', () => { void submit(); });
    emailInput.addEventListener('keydown', (e: KeyboardEvent) => {
      if (e.key === 'Enter') {
        e.preventDefault();
        void submit();
      }
    });
  }

  const emailSuccessBack = document.getElementById('signin-email-success-back');
  if (emailSuccessBack !== null) {
    emailSuccessBack.addEventListener('click', () => showSignInPanel('providers'));
  }
}

function setModalError(msg: string): void {
  const errEl = document.getElementById('signin-modal-error');
  if (errEl === null) return;
  errEl.textContent = msg;
  errEl.style.display = msg === '' ? 'none' : '';
}

// ── Lobby auth chip + signed-in chip rendering ─────────────────────

function renderLobbyChip(): void {
  const chip = document.getElementById('auth-status-chip');
  const signinBtn = document.getElementById('signin-button');
  const logoutBtn = document.getElementById('logout-button');
  if (chip === null) return;
  if (state.authenticated) {
    chip.style.display = '';
    chip.setAttribute('data-authenticated', 'true');
    const label = chip.querySelector('.auth-status-chip-email') as HTMLElement | null;
    const badge = chip.querySelector('.auth-status-chip-provider') as HTMLElement | null;
    if (label !== null) label.textContent = state.email ?? 'Signed in';
    if (badge !== null) {
      const provider = state.primaryProvider ?? 'email';
      badge.textContent = providerBadgeLabel(provider);
      badge.setAttribute('data-provider', provider);
    }
    if (signinBtn !== null) signinBtn.style.display = 'none';
    if (logoutBtn !== null) logoutBtn.style.display = '';
  } else {
    chip.style.display = 'none';
    chip.setAttribute('data-authenticated', 'false');
    if (signinBtn !== null) {
      // Show sign-in button only if the server actually has auth, OR when
      // we don't yet know — better UX to let the user click and see the
      // placeholder than to hide the button entirely.
      signinBtn.style.display = '';
    }
    if (logoutBtn !== null) logoutBtn.style.display = 'none';
  }
}

function providerBadgeLabel(provider: AuthProviderId): string {
  switch (provider) {
    case 'google': return '🔵 Google';
    case 'github': return '⬛ GitHub';
    case 'email': return '✉ Email';
  }
}

// ── Linked accounts section on the profile page ────────────────────

function renderLinkedAccountsSection(): void {
  const section = document.getElementById('profile-linked-accounts');
  if (section === null) return;
  section.replaceChildren();

  if (!state.serverHasAuth) {
    const p = document.createElement('p');
    p.className = 'profile-linked-empty';
    p.textContent = 'Authentication is not available on this server yet.';
    section.appendChild(p);
    return;
  }

  if (!state.authenticated) {
    const p = document.createElement('p');
    p.className = 'profile-linked-empty';
    p.textContent = 'Sign in to link multiple providers to your account.';
    section.appendChild(p);
    const signinBtn = document.createElement('button');
    signinBtn.type = 'button';
    signinBtn.className = 'btn btn-sm btn-primary';
    signinBtn.textContent = 'Sign in';
    signinBtn.addEventListener('click', () => openSignInModal());
    section.appendChild(signinBtn);
    return;
  }

  // Render one row per known provider with the link/unlink affordance.
  const list = document.createElement('ul');
  list.className = 'profile-linked-list';
  list.setAttribute('role', 'list');
  for (const provider of KNOWN_PROVIDERS) {
    const linked = state.identities.find((id) => id.provider === provider) ?? null;
    list.appendChild(buildLinkedRow(provider, linked));
  }
  section.appendChild(list);
}

function buildLinkedRow(
  provider: AuthProviderId,
  linked: AuthIdentity | null,
): HTMLLIElement {
  const row = document.createElement('li');
  row.className = 'profile-linked-row';
  row.setAttribute('data-provider', provider);

  const badge = document.createElement('span');
  badge.className = `profile-linked-badge profile-linked-badge-${provider}`;
  badge.textContent = providerBadgeLabel(provider);
  row.appendChild(badge);

  const meta = document.createElement('span');
  meta.className = 'profile-linked-meta';
  meta.textContent = linked === null
    ? 'Not linked'
    : (linked.email ?? 'Linked');
  row.appendChild(meta);

  const action = document.createElement('button');
  action.type = 'button';
  action.className = 'btn btn-sm';
  if (linked === null) {
    action.classList.add('btn-primary');
    action.textContent = 'Link';
    action.setAttribute('data-testid', `profile-link-provider-${provider}`);
    action.addEventListener('click', async () => {
      action.disabled = true;
      if (provider === 'email') {
        // Email link uses the same magic-link flow with the user's
        // current cookie attached — server resolves "link" intent.
        openSignInModal();
        action.disabled = false;
        return;
      }
      const result = await startOAuth(provider, 'link');
      action.disabled = false;
      if (!result.ok && result.error !== null) {
        meta.textContent = result.error;
      }
    });
  } else {
    action.classList.add('btn-secondary');
    action.textContent = 'Unlink';
    action.setAttribute('data-testid', `profile-unlink-${provider}`);
    // Disable unlink if it's the only identity left.
    const isOnly = state.identities.length <= 1;
    if (isOnly) {
      action.disabled = true;
      action.title = 'Cannot unlink the only sign-in method.';
    }
    action.addEventListener('click', async () => {
      action.disabled = true;
      const result = await unlinkProvider(provider);
      action.disabled = false;
      if (!result.ok && result.error !== null) {
        meta.textContent = result.error;
      }
    });
  }
  row.appendChild(action);
  return row;
}

// ── Magic-link landing page ────────────────────────────────────────

function wireMagicLinkLanding(): void {
  const params = new URLSearchParams(window.location.search);
  const token = params.get('auth');
  if (token === null || token === '') return;

  const landing = document.getElementById('magic-link-landing');
  if (landing === null) return;
  landing.classList.add('magic-link-landing-open');
  landing.setAttribute('aria-hidden', 'false');

  const successEl = document.getElementById('magic-link-success');
  const failureEl = document.getElementById('magic-link-failure');
  const failureMsgEl = document.getElementById('magic-link-failure-message');
  const successEmailEl = document.getElementById('magic-link-success-email');
  if (successEl !== null) successEl.style.display = 'none';
  if (failureEl !== null) failureEl.style.display = 'none';

  void (async () => {
    const result = await verifyEmailToken(token);
    if (result.ok) {
      if (successEmailEl !== null && result.email !== null) {
        successEmailEl.textContent = result.email;
      }
      if (successEl !== null) successEl.style.display = '';
      // Clean the URL so a refresh doesn't re-verify.
      try {
        const cleaned = new URL(window.location.href);
        cleaned.searchParams.delete('auth');
        window.history.replaceState(null, '', cleaned.toString());
      } catch { /* swallow */ }
    } else {
      if (failureMsgEl !== null) {
        failureMsgEl.textContent = result.error ?? 'Magic link verification failed.';
      }
      if (failureEl !== null) failureEl.style.display = '';
    }
  })();

  // Wire the dismiss buttons.
  for (const id of ['magic-link-dismiss', 'magic-link-dismiss-failure']) {
    const btn = document.getElementById(id);
    if (btn === null) continue;
    btn.addEventListener('click', () => {
      landing.classList.remove('magic-link-landing-open');
      landing.setAttribute('aria-hidden', 'true');
    });
  }
}
