// Phase K Wave 3 — Shared toast helper.
//
// `client-ui.ts` already owns a private `showToast` helper that drives
// the `#toast-region` aria-live container.  Wave 3 surfaces a thin
// module-level wrapper so other modules (voice.ts, tournaments.ts)
// can push toasts into the same region without needing a Client
// instance — Voice's pre-game disabled-mic toast is fired before any
// Client is constructed, and the tournament seeding panel is owned
// by the lobby chain which doesn't hold a Client either.
//
// The DOM is the single source of truth: this helper looks up the
// `[data-testid="toast-region"]` element each call so it Just Works
// whether the region was rendered statically (index.html) or wired
// dynamically (client-ui's ctor).

export type ToastSeverity = 'info' | 'error' | 'success';

const ENTRY_DELAY_MS = 0; // rAF gates the visibility class

export function showToast(
  message: string,
  severity: ToastSeverity = 'info',
  duration: number = 4000,
): void {
  const region = document.getElementById('toast-region');
  if (region === null) {
    // Fallback: log when no region exists so we don't lose the message
    // entirely during early-boot or in tests that haven't mounted the
    // lobby HTML.  This is intentionally console-only — no analytics.
    // eslint-disable-next-line no-console
    console.warn(`[toast] ${severity}: ${message}`);
    return;
  }
  const el = document.createElement('div');
  el.className = `toast toast-${severity}`;
  el.setAttribute('role', severity === 'error' ? 'alert' : 'status');
  el.setAttribute(
    'data-testid',
    severity === 'error' ? 'toast-error' : (severity === 'success' ? 'toast-success' : 'toast-info'),
  );
  el.textContent = message;
  region.appendChild(el);
  window.setTimeout(() => {
    window.requestAnimationFrame(() => el.classList.add('toast-visible'));
  }, ENTRY_DELAY_MS);
  window.setTimeout(() => {
    el.classList.remove('toast-visible');
    window.setTimeout(() => {
      if (el.parentNode !== null) el.parentNode.removeChild(el);
    }, 400);
  }, duration);
}

// Phase K Wave 3 — Voice-specific toast.  VoiceHub.JoinVoice can fail
// with two known reasons surfaced by Bishop's Wave-3 backend:
//   • "voice not enabled" — the table-creator hasn't flipped the flag.
//   • "spectators cannot join voice" — the viewer is in spectate mode.
// We map both to a friendly user-facing toast.
export function showVoiceToast(reason: string): void {
  const lc = reason.toLowerCase();
  if (lc.indexOf('not enabled') !== -1) {
    showToast(
      'Voice is not enabled for this table. Ask the host to enable it.',
      'error',
    );
    return;
  }
  if (lc.indexOf('spectator') !== -1) {
    showToast(
      'Spectators cannot join voice chat — take a seat to talk.',
      'error',
    );
    return;
  }
  showToast(`Voice chat error: ${reason}`, 'error');
}
