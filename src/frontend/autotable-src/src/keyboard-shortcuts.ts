// Phase K Wave 23 — Hicks (Frontend).
//
// Global keyboard-shortcut handler.  Lazy-loaded behind the first
// `keydown` event the page sees that matches the shortcut prefix-set
// (`/`, `?`, `g`, `Esc`, `Ctrl+K`).  Until the chunk lands, the page
// receives no shortcut bindings — the eager bundle only carries the
// 80-LoC probe that gates the dynamic import.
//
// Why lazy: the lobby cold path bundle is at the §3.8 ≤ 95 KiB
// ceiling (W23 tightening; see `docs/lh13-soft-pin-rationale.md §3`).
// Wiring the shortcut handler eagerly would cost ~1.5 KB minified for
// a feature that only ~30 % of sessions touch.  Lazifying behind the
// first keystroke means non-keyboard sessions (touch-only, screen-
// readers using virtual cursor) shed the chunk entirely.
//
// The shortcut surface (W23 baseline; expandable wave-over-wave):
//   • `/` or `Ctrl+K` → focus the lobby search/quick-action input
//     (no-op when the input isn't in the DOM).
//   • `?` → show the keyboard-shortcuts cheat-sheet overlay.
//   • `g`+`l` → "go to lobby" — navigates to `/`.
//   • `g`+`p` → "go to profile" — clicks the profile chip.
//   • `Esc` → close the top-most modal/overlay (cheat-sheet, settings
//     drawer, profile-page modal).
//
// Each binding is a tiny imperative wiring against documented
// data-testid hooks; the module ships zero framework, zero deps.

export interface KeyboardShortcutsHandle {
  /** Disconnect the handlers (used by the W23 e2e smoke). */
  dispose(): void;
}

type ShortcutKey =
  | 'focus-search'
  | 'cheat-sheet'
  | 'go-lobby'
  | 'go-profile'
  | 'dismiss-overlay';

// ── Two-key chord buffer (`g`+`l`, `g`+`p`) ────────────────────────
//
// The two-key shortcuts use a 1.5-second buffer window: after `g` is
// pressed, the next keystroke is treated as the second-half of the
// chord if it lands within 1.5 s; otherwise the buffer is cleared and
// the keystroke is processed standalone.

const CHORD_WINDOW_MS = 1500;
let chordTimer: number | null = null;
let chordPrefix: string | null = null;

function startChord(prefix: string): void {
  chordPrefix = prefix;
  if (chordTimer !== null) window.clearTimeout(chordTimer);
  chordTimer = window.setTimeout(() => {
    chordPrefix = null;
    chordTimer = null;
  }, CHORD_WINDOW_MS);
}

function consumeChord(): string | null {
  const p = chordPrefix;
  if (chordTimer !== null) window.clearTimeout(chordTimer);
  chordPrefix = null;
  chordTimer = null;
  return p;
}

// ── Cheat-sheet overlay ────────────────────────────────────────────

const CHEAT_SHEET_ID = 'keyboard-shortcuts-cheat-sheet';

function showCheatSheet(): void {
  let overlay = document.getElementById(CHEAT_SHEET_ID);
  if (overlay !== null) return;
  overlay = document.createElement('div');
  overlay.id = CHEAT_SHEET_ID;
  overlay.setAttribute('data-testid', 'keyboard-shortcuts-cheat-sheet');
  overlay.setAttribute('role', 'dialog');
  overlay.setAttribute('aria-modal', 'true');
  overlay.setAttribute('aria-label', 'Keyboard shortcuts');
  overlay.style.cssText =
    'position:fixed;inset:0;display:flex;align-items:center;'
    + 'justify-content:center;background:rgba(0,0,0,.55);z-index:99998;'
    + 'font-family:system-ui,sans-serif;color:#eaeaea;';
  overlay.innerHTML = `
    <section style="background:#1e293b;padding:24px 32px;border-radius:8px;
                    min-width:320px;max-width:480px;">
      <h2 style="margin:0 0 12px;font-size:18px;">Keyboard shortcuts</h2>
      <dl style="display:grid;grid-template-columns:auto 1fr;gap:8px 16px;
                 margin:0;font-size:14px;">
        <dt><kbd>/</kbd> / <kbd>Ctrl</kbd>+<kbd>K</kbd></dt><dd>Focus search</dd>
        <dt><kbd>?</kbd></dt><dd>Show this cheat-sheet</dd>
        <dt><kbd>g</kbd> <kbd>l</kbd></dt><dd>Go to lobby</dd>
        <dt><kbd>g</kbd> <kbd>p</kbd></dt><dd>Go to profile</dd>
        <dt><kbd>Esc</kbd></dt><dd>Dismiss overlay</dd>
      </dl>
      <p style="margin:16px 0 0;font-size:12px;opacity:.7;">
        Press <kbd>Esc</kbd> to close.
      </p>
    </section>`;
  document.body.appendChild(overlay);
}

function hideCheatSheet(): boolean {
  const overlay = document.getElementById(CHEAT_SHEET_ID);
  if (overlay === null) return false;
  overlay.remove();
  return true;
}

// ── Binding actions ────────────────────────────────────────────────

function focusSearchInput(): void {
  const candidates = [
    'lobby-search',
    'quick-action-input',
    'matchmaking-search',
  ];
  for (const id of candidates) {
    const el = document.getElementById(id) as HTMLInputElement | null;
    if (el !== null && typeof el.focus === 'function') {
      el.focus();
      el.select?.();
      return;
    }
  }
}

function goToLobby(): void {
  if (window.location.pathname !== '/' || window.location.search !== '') {
    window.location.assign('/');
  }
}

function goToProfile(): void {
  const chip = document.getElementById('lobby-open-profile') as HTMLElement | null;
  if (chip !== null && typeof chip.click === 'function') {
    chip.click();
  }
}

function dismissTopOverlay(): void {
  if (hideCheatSheet()) return;
  // Try clicking known modal close buttons in priority order.
  const closers = [
    'admin-panel-close',
    'settings-drawer-v2-close',
    'profile-page-close',
  ];
  for (const testid of closers) {
    const btn = document.querySelector(`[data-testid="${testid}"]`) as HTMLElement | null;
    if (btn !== null && typeof btn.click === 'function') {
      btn.click();
      return;
    }
  }
}

// ── Event dispatcher ───────────────────────────────────────────────

function dispatch(action: ShortcutKey): void {
  switch (action) {
    case 'focus-search':    focusSearchInput(); break;
    case 'cheat-sheet':     showCheatSheet(); break;
    case 'go-lobby':        goToLobby(); break;
    case 'go-profile':      goToProfile(); break;
    case 'dismiss-overlay': dismissTopOverlay(); break;
  }
}

function isEditableTarget(t: EventTarget | null): boolean {
  if (!(t instanceof HTMLElement)) return false;
  if (t.isContentEditable) return true;
  const tag = t.tagName;
  return tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT';
}

function onKeydown(ev: KeyboardEvent): void {
  // Honour editable targets — never hijack typing.
  if (isEditableTarget(ev.target)) {
    // Exception: Esc inside an editable target still dismisses overlays.
    if (ev.key === 'Escape') dispatch('dismiss-overlay');
    return;
  }

  const ctrlOrMeta = ev.ctrlKey || ev.metaKey;

  // Ctrl+K / Cmd+K → focus search.
  if (ctrlOrMeta && (ev.key === 'k' || ev.key === 'K')) {
    ev.preventDefault();
    dispatch('focus-search');
    return;
  }

  if (ev.key === '/') {
    ev.preventDefault();
    dispatch('focus-search');
    return;
  }

  if (ev.key === '?') {
    ev.preventDefault();
    dispatch('cheat-sheet');
    return;
  }

  if (ev.key === 'Escape') {
    dispatch('dismiss-overlay');
    return;
  }

  // Two-key chord: prefix `g`, then `l`/`p`.
  if (chordPrefix === 'g') {
    if (ev.key === 'l' || ev.key === 'L') {
      consumeChord();
      ev.preventDefault();
      dispatch('go-lobby');
      return;
    }
    if (ev.key === 'p' || ev.key === 'P') {
      consumeChord();
      ev.preventDefault();
      dispatch('go-profile');
      return;
    }
    consumeChord();
    return;
  }

  if (ev.key === 'g' || ev.key === 'G') {
    startChord('g');
    return;
  }
}

/**
 * Install the shortcut handler.  Idempotent — calling twice is
 * harmless (the second call short-circuits).  Returns a handle the
 * test harness uses to detach the handler for isolation.
 */
let installed = false;
let installedHandler: ((ev: KeyboardEvent) => void) | null = null;

export function installKeyboardShortcuts(): KeyboardShortcutsHandle {
  if (installed && installedHandler !== null) {
    return {
      dispose: (): void => {
        if (installedHandler !== null) {
          window.removeEventListener('keydown', installedHandler);
          installedHandler = null;
          installed = false;
        }
      },
    };
  }
  installed = true;
  installedHandler = onKeydown;
  window.addEventListener('keydown', installedHandler);
  return {
    dispose: (): void => {
      if (installedHandler !== null) {
        window.removeEventListener('keydown', installedHandler);
        installedHandler = null;
        installed = false;
      }
    },
  };
}

/** Public test-helper: directly fire a shortcut action without a key event. */
export function fireShortcutForTesting(action: ShortcutKey): void {
  dispatch(action);
}
