// Phase J Wave 6 — Auth-bootstrap identity module.
//
// On app boot we make a `POST /api/identity` call with
// `credentials: 'include'` so Bishop's backend can stamp the
// `mahjong_pid` cookie if it isn't already there.  The endpoint
// returns the persistent profile shape `{ playerId, displayName,
// avatarColor }` — that's the *cookie-bound* identity, which is
// distinct from the SignalR `Context.ConnectionId` the existing
// `profile.ts` module caches.  We treat the two as parallel:
//
//   • `profile.ts` (Wave 5) → SignalR profile, ProfileLoaded event,
//     drives lobby chips + post-game stats.
//   • `identity.ts` (Wave 6) → cookie-bound profile, drives the
//     onboarding card + the leaderboard's "this is you" row match.
//
// ── First-visit detection ──────────────────────────────────────────
//
// Bishop's `mahjong_pid` cookie is `HttpOnly`, so JavaScript cannot
// read its value via `document.cookie` — the sniff in `readCookie`
// will always return null in the browser.  We therefore use a
// localStorage flag (`mahjong.identity.onboarded.v1`) as the
// authoritative "user has been here before" signal:
//
//   • Cookie sniff returns null AND LS flag missing  → first visit
//                                                       (show card)
//   • LS flag present                                → silent restore
//                                                       (skip card)
//   • Server sends `isNewProfile: false`             → silent restore
//
// The cookie sniff is still wired up as a fallback / future-proof
// signal: when the backend stops using HttpOnly (or the test harness
// reads through a server middleware that strips the flag) the sniff
// becomes the primary detector.
//
// ── Wire contract (Bishop, Phase J Wave 6) ──────────────────────────
//
//   POST /api/identity
//     credentials: include   (so Set-Cookie: mahjong_pid=… lands)
//     → 200 OK
//       {
//         playerId: "<32-char hex>",
//         displayName: "Player-AABBCC",
//         avatarColor: "#1E88E5",
//         createdAt: "<iso>",
//         lastSeenAt: "<iso>"
//       }
//
// (Optional future field) `isNewProfile: boolean` — when present
// overrides the localStorage / cookie heuristic; the backend can
// detect "row just inserted" from EF Core's tracking.

import { EventEmitter } from 'events';

import { getHubConnection, invokeHub } from './hub';
import {
  AVATAR_COLOR_PRESETS,
  DISPLAY_NAME_MAX,
  DISPLAY_NAME_MIN,
  getProfile,
  initProfileHubBindings,
  setAvatarColor as setProfileAvatarColor,
  setDisplayName as setProfileDisplayName,
  validateAvatarColor,
  validateDisplayName,
} from './profile';

// ── Public types ─────────────────────────────────────────────────────

export interface Identity {
  playerId: string;
  displayName: string;
  avatarColor: string;
  /** True when the cookie wasn't present at boot (this is a brand-new visitor). */
  isFirstVisit: boolean;
  /** ISO timestamp of first identity creation; surfaced as
   *  "Member since" on the Wave-7 profile page.  Optional because
   *  pre-Wave-7 caches won't carry it. */
  createdAt?: string;
}

// ── Constants ────────────────────────────────────────────────────────

export const IDENTITY_ENDPOINT = '/api/identity';
export const IDENTITY_COOKIE_NAME = 'mahjong_pid';

const LS_KEY_IDENTITY_CACHE = 'mahjong.identity.cache.v1';
const LS_KEY_ONBOARDED = 'mahjong.identity.onboarded.v1';

// ── Module state ─────────────────────────────────────────────────────

const events = new EventEmitter();
let current: Identity | null = null;
let cookieAtBoot: string | null = null;
let bootPromise: Promise<Identity | null> | null = null;

// ── Helpers ──────────────────────────────────────────────────────────

/**
 * Read a cookie value by name (or null if absent).  We split on `; `
 * which is what `document.cookie` serialises to in every browser.
 */
export function readCookie(name: string): string | null {
  try {
    const all = document.cookie;
    if (all === '') return null;
    const parts = all.split(';');
    for (const raw of parts) {
      const trimmed = raw.trim();
      const eq = trimmed.indexOf('=');
      if (eq === -1) {
        if (trimmed === name) return '';
        continue;
      }
      if (trimmed.slice(0, eq) === name) {
        return decodeURIComponent(trimmed.slice(eq + 1));
      }
    }
    return null;
  } catch {
    return null;
  }
}

function loadCache(): Identity | null {
  try {
    const raw = window.localStorage.getItem(LS_KEY_IDENTITY_CACHE);
    if (raw === null) return null;
    const j = JSON.parse(raw) as Partial<Identity>;
    if (typeof j.playerId !== 'string' || j.playerId === '') return null;
    return {
      playerId: j.playerId,
      displayName: typeof j.displayName === 'string' ? j.displayName : '',
      avatarColor:
        typeof j.avatarColor === 'string' && validateAvatarColor(j.avatarColor)
          ? j.avatarColor
          : AVATAR_COLOR_PRESETS[5],
      isFirstVisit: false,
      createdAt: typeof j.createdAt === 'string' ? j.createdAt : undefined,
    };
  } catch {
    return null;
  }
}

function writeCache(id: Identity): void {
  try {
    window.localStorage.setItem(
      LS_KEY_IDENTITY_CACHE,
      JSON.stringify({
        playerId: id.playerId,
        displayName: id.displayName,
        avatarColor: id.avatarColor,
        createdAt: id.createdAt,
      }),
    );
  } catch {
    /* private mode / quota — skip */
  }
}

function isOnboardingComplete(): boolean {
  try {
    return window.localStorage.getItem(LS_KEY_ONBOARDED) === 'true';
  } catch {
    return false;
  }
}

function markOnboardingComplete(): void {
  try {
    window.localStorage.setItem(LS_KEY_ONBOARDED, 'true');
  } catch {
    /* skip */
  }
}

function normalizeIdentity(raw: unknown, fallbackId: string, isFirstVisit: boolean): Identity {
  const o = (raw !== null && typeof raw === 'object' ? raw : {}) as Record<string, unknown>;
  const playerId =
    typeof o.playerId === 'string' && o.playerId !== '' ? o.playerId : fallbackId;
  const displayName = typeof o.displayName === 'string' ? o.displayName : '';
  const avatarColor =
    typeof o.avatarColor === 'string' && validateAvatarColor(o.avatarColor)
      ? o.avatarColor
      : AVATAR_COLOR_PRESETS[5];
  // Server hint overrides the cookie sniff when present.
  const serverIsNew = typeof o.isNewProfile === 'boolean' ? o.isNewProfile : null;
  // Phase J Wave 7 — surface the createdAt timestamp so the profile
  // page can show "Member since {date}".  The endpoint returns it as
  // an ISO string; we accept either camelCase or PascalCase.
  const createdAt =
    typeof o.createdAt === 'string' && o.createdAt !== ''
      ? o.createdAt
      : (typeof o.CreatedAt === 'string' && o.CreatedAt !== '' ? o.CreatedAt : undefined);
  return {
    playerId,
    displayName,
    avatarColor,
    isFirstVisit: serverIsNew !== null ? serverIsNew : isFirstVisit,
    createdAt,
  };
}

function setCurrent(id: Identity): void {
  current = id;
  writeCache(id);
  events.emit('identity', id);
}

// ── Public API ───────────────────────────────────────────────────────

export function getIdentity(): Identity | null {
  return current;
}

export function onIdentity(handler: (id: Identity) => void): () => void {
  events.on('identity', handler);
  if (current !== null) handler(current);
  return () => events.off('identity', handler);
}

/**
 * Bootstrap the identity.  Idempotent — concurrent callers share the
 * same in-flight POST.  Returns the resolved identity, or null when
 * the endpoint is unreachable (offline / 5xx).
 *
 * The "first-visit" decision is made *before* the POST: if the
 * `mahjong_pid` cookie isn't on the jar at boot, the Set-Cookie
 * response we receive is the very first issue of the identity.
 */
export async function bootstrapIdentity(): Promise<Identity | null> {
  if (bootPromise !== null) return bootPromise;
  cookieAtBoot = readCookie(IDENTITY_COOKIE_NAME);
  const firstVisitGuess = cookieAtBoot === null || cookieAtBoot === '';
  bootPromise = (async () => {
    try {
      const resp = await fetch(IDENTITY_ENDPOINT, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Accept': 'application/json' },
      });
      if (!resp.ok) {
        // Fall back to cache so the UI isn't blocked behind a 5xx.
        const cached = loadCache();
        if (cached !== null) {
          setCurrent({ ...cached, isFirstVisit: false });
          return current;
        }
        return null;
      }
      const body = (await resp.json()) as unknown;
      const id = normalizeIdentity(body, cookieAtBoot ?? '', firstVisitGuess);
      setCurrent(id);
      return id;
    } catch {
      const cached = loadCache();
      if (cached !== null) {
        setCurrent({ ...cached, isFirstVisit: false });
        return current;
      }
      return null;
    }
  })();
  return bootPromise;
}

/** Pre-boot cookie sniff (null if bootstrapIdentity hasn't started yet). */
export function getCookieAtBoot(): string | null {
  return cookieAtBoot;
}

// ── Onboarding card ─────────────────────────────────────────────────
//
// First-visit users see a small card pinned at the top of the lobby
// asking for their preferred display name + avatar colour.  The card
// is dismissed when the user clicks Continue or Skip.  On subsequent
// visits the card is suppressed via the LS_KEY_ONBOARDED flag.

let onboardingInstalled = false;

/**
 * Decide whether the onboarding card should be shown for this visit.
 * Returns true when:
 *   • the `mahjong_pid` cookie was absent at boot, AND
 *   • the user has not previously completed onboarding
 *     (LS flag absent or false).
 *
 * Server `isNewProfile` flag (when present) overrides the cookie sniff
 * — if the backend says "this is a brand-new row" we trust it.
 */
export function shouldShowOnboarding(): boolean {
  if (isOnboardingComplete()) return false;
  if (current !== null && current.isFirstVisit) return true;
  // Pre-boot path: rely on the cookie sniff alone.
  if (current === null) {
    return cookieAtBoot === null || cookieAtBoot === '';
  }
  return false;
}

/**
 * Mount the onboarding card.  Idempotent — bails out if the markup
 * isn't present or if it's already wired.  When `shouldShowOnboarding`
 * returns false the card stays hidden.
 *
 * Bishop's `UpdateProfile` SignalR RPC is the canonical writer for
 * displayName + avatarColor — we route the Continue button through
 * there so the cookie-bound identity stays in sync with the
 * connection-id profile that drives the rest of the UI.
 */
export function installOnboardingCard(): void {
  if (onboardingInstalled) return;
  const card = document.getElementById('onboarding-card');
  if (card === null) return;
  onboardingInstalled = true;

  const nameInput = document.getElementById(
    'onboarding-display-name-input') as HTMLInputElement | null;
  const nameError = document.getElementById('onboarding-display-name-error');
  const presetsHost = document.getElementById('onboarding-avatar-presets');
  const customColor = document.getElementById(
    'onboarding-avatar-color-custom') as HTMLInputElement | null;
  const previewAvatar = document.getElementById('onboarding-preview-avatar');
  const continueBtn = document.getElementById(
    'onboarding-continue') as HTMLButtonElement | null;
  const skipBtn = document.getElementById(
    'onboarding-skip') as HTMLButtonElement | null;

  let selectedColor: string =
    current !== null ? current.avatarColor : AVATAR_COLOR_PRESETS[5];

  const refreshPreview = (): void => {
    if (previewAvatar !== null) {
      (previewAvatar as HTMLElement).style.backgroundColor = selectedColor;
      const name = nameInput?.value.trim() ?? '';
      previewAvatar.textContent = onboardingInitial(name);
    }
    if (presetsHost !== null) {
      for (const btn of presetsHost.querySelectorAll<HTMLButtonElement>(
        '.onboarding-avatar-preset')) {
        const matches = btn.getAttribute('data-color')?.toLowerCase()
          === selectedColor.toLowerCase();
        btn.classList.toggle('onboarding-avatar-preset-selected', matches);
        btn.setAttribute('aria-checked', matches ? 'true' : 'false');
      }
    }
    if (customColor !== null && document.activeElement !== customColor) {
      customColor.value = selectedColor;
    }
  };

  if (presetsHost !== null) {
    presetsHost.replaceChildren();
    AVATAR_COLOR_PRESETS.forEach((hex, idx) => {
      const btn = document.createElement('button');
      btn.type = 'button';
      btn.className = 'onboarding-avatar-preset';
      btn.style.backgroundColor = hex;
      btn.setAttribute('data-color', hex);
      btn.setAttribute('data-testid', `onboarding-avatar-color-preset-${idx}`);
      btn.setAttribute('role', 'radio');
      btn.setAttribute('aria-checked', 'false');
      btn.setAttribute('aria-label', `Preset colour ${idx + 1}`);
      btn.title = hex;
      btn.addEventListener('click', () => {
        selectedColor = hex;
        refreshPreview();
      });
      presetsHost.appendChild(btn);
    });
  }

  if (customColor !== null) {
    customColor.addEventListener('input', () => {
      if (!validateAvatarColor(customColor.value)) return;
      selectedColor = customColor.value.toLowerCase();
      refreshPreview();
    });
  }

  if (nameInput !== null) {
    nameInput.setAttribute('minlength', String(DISPLAY_NAME_MIN));
    nameInput.setAttribute('maxlength', String(DISPLAY_NAME_MAX));
    nameInput.addEventListener('input', () => {
      const { error } = validateDisplayName(nameInput.value);
      if (nameError !== null) nameError.textContent = error ?? '';
      nameInput.classList.toggle('onboarding-input-invalid', error !== null);
      refreshPreview();
    });
  }

  if (continueBtn !== null) {
    continueBtn.addEventListener('click', () => {
      const rawName = nameInput?.value ?? '';
      const { value, error } = validateDisplayName(rawName);
      if (error !== null || value === null) {
        if (nameError !== null) nameError.textContent = error ?? 'Enter a name.';
        nameInput?.focus();
        return;
      }
      const expanded = expandHexColor(selectedColor).toUpperCase();
      if (current !== null) {
        setCurrent({ ...current, displayName: value, avatarColor: selectedColor });
      }
      // Bridge to the Wave-5 profile.ts cache so the lobby chip +
      // any other `onProfile` subscriber re-render with the chosen
      // name and colour immediately.  We have to install the
      // SignalR hub bindings + force a connect first, because
      // `profile.ts` only wires its `ProfileLoaded` listener once
      // the hub is up, and `setDisplayName` / `setAvatarColor` are
      // no-ops while its local `current` is still null.
      void applyProfileFromOnboarding(value, selectedColor, expanded);
      markOnboardingComplete();
      dismissOnboardingCard();
    });
  }

  if (skipBtn !== null) {
    skipBtn.addEventListener('click', () => {
      markOnboardingComplete();
      dismissOnboardingCard();
    });
  }

  // Populate the input with whatever default name the backend gave us
  // so Continue with no edits still ends up with a sensible name.
  if (current !== null && nameInput !== null && nameInput.value === '') {
    nameInput.value = current.displayName;
  }
  refreshPreview();

  onIdentity((id) => {
    if (nameInput !== null && document.activeElement !== nameInput
        && nameInput.value === '') {
      nameInput.value = id.displayName;
    }
    if (current === null || !id.isFirstVisit) {
      // Identity arrived after the card was installed — only show
      // the card if first-visit and not previously onboarded.
    }
    refreshPreview();
  });

  // Visibility: shown only when shouldShowOnboarding() returns true.
  if (shouldShowOnboarding()) {
    showOnboardingCard();
  } else {
    hideOnboardingCard();
  }
}

function showOnboardingCard(): void {
  const card = document.getElementById('onboarding-card');
  if (card === null) return;
  card.style.display = '';
  card.setAttribute('aria-hidden', 'false');
}

function hideOnboardingCard(): void {
  const card = document.getElementById('onboarding-card');
  if (card === null) return;
  card.style.display = 'none';
  card.setAttribute('aria-hidden', 'true');
}

function dismissOnboardingCard(): void {
  hideOnboardingCard();
}

function onboardingInitial(name: string): string {
  const trimmed = name.trim();
  if (trimmed === '') return '?';
  const parts = trimmed.split(/\s+/);
  if (parts.length === 1) return parts[0].charAt(0).toUpperCase();
  return (parts[0].charAt(0) + parts[parts.length - 1].charAt(0)).toUpperCase();
}

function expandHexColor(hex: string): string {
  if (/^#[0-9a-fA-F]{3}$/.test(hex)) {
    const r = hex.charAt(1);
    const g = hex.charAt(2);
    const b = hex.charAt(3);
    return `#${r}${r}${g}${g}${b}${b}`;
  }
  return hex;
}

/**
 * Push the onboarding-form values into the Wave-5 profile cache so
 * the lobby's profile chip + the move-log nick map re-render with
 * the chosen name/colour.  Steps:
 *
 *   1.  Install the `ProfileLoaded` listener (profile.ts only wires
 *       this once the hub is up).
 *   2.  Force a hub connection — the server's `OnConnectedAsync`
 *       fires a `ProfileLoaded` event with the *default* name, which
 *       seeds profile.ts's local cache so `setDisplayName` /
 *       `setAvatarColor` aren't no-ops.
 *   3.  Poll briefly for the seed event to land (it travels over
 *       the same SignalR WebSocket as the connect, so we usually
 *       see it within a single tick).
 *   4.  Apply the onboarding values through the profile module's
 *       public setters; their optimistic local-update path fires
 *       `onProfile` listeners immediately, and the debounced
 *       `UpdateProfile` invoke commits to the server.
 *   5.  As a defensive belt-and-braces measure (e.g. if SignalR is
 *       unreachable in tests), invoke `UpdateProfile` directly so
 *       the server still gets the new name even when profile.ts
 *       falls through.
 */
async function applyProfileFromOnboarding(
  displayName: string,
  selectedColor: string,
  expandedColor: string,
): Promise<void> {
  try {
    initProfileHubBindings();
    await getHubConnection();
  } catch {
    // Hub unreachable — fall through to the direct invoke below;
    // the local identity cache is still updated by the caller.
  }
  const deadline = Date.now() + 2000;
  while (getProfile() === null && Date.now() < deadline) {
    await new Promise<void>((resolve) => setTimeout(resolve, 50));
  }
  const nameResult = setProfileDisplayName(displayName);
  const colorResult = setProfileAvatarColor(selectedColor);
  if (nameResult.error !== null || colorResult.error !== null) {
    // profile.ts hadn't loaded — push the values to the hub
    // ourselves so the server-side record still picks them up.
    try {
      await invokeHub('UpdateProfile', displayName, expandedColor);
    } catch {
      /* swallow — the local identity cache is the source of truth
         until the hub is reachable */
    }
  }
}

/**
 * Re-evaluate visibility (called by lobby after installOnboardingCard
 * + after bootstrapIdentity resolves, in case the identity arrived
 * *after* the lobby mounted).
 */
export function refreshOnboardingVisibility(): void {
  if (shouldShowOnboarding()) showOnboardingCard();
  else hideOnboardingCard();
}
