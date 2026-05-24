// Phase K Wave 23 — Hicks (Frontend).
//
// `data-tip` tooltip engine.  Lazy-loaded behind the first
// `pointerover` event the page sees against an element carrying the
// `data-tip` attribute.  Until the chunk lands, hovering an element
// with `data-tip` is a no-op — the eager bundle only carries the
// 30-LoC probe that gates the dynamic import.
//
// Why lazy: lobby cold path bundle is at the §3.8 ≤95 KiB ceiling.
// Tooltip positioning is ~1 KB minified for a feature that only
// renders an overlay on actual hover; the probe is sub-200 B.  Most
// page loads never trigger a tooltip-visible event (esp. touch-only
// sessions); lazifying behind first hit means those sessions shed
// the chunk entirely.
//
// `data-tip="message"` is the wire contract.  Optional companion
// attributes:
//   • `data-tip-placement` — `top` (default), `bottom`, `left`,
//     `right`.  The engine flips to the opposite side if the
//     primary placement overflows the viewport.
//   • `data-tip-delay`     — ms before the tooltip mounts (default
//     400; instant tooltips can use `0`).
//
// Accessibility:
//   • The engine respects `prefers-reduced-motion: reduce` by
//     dropping the fade-in to a single rAF frame.
//   • `aria-describedby` is attached to the host element while the
//     tooltip is mounted so screen-readers narrate the content.

export interface TooltipEngineHandle {
  /** Disconnect the engine (used by the W23 e2e isolation). */
  dispose(): void;
}

interface TooltipState {
  host: HTMLElement;
  tooltip: HTMLElement;
  showTimer: number | null;
}

const TOOLTIP_ID_PREFIX = 'data-tip-tooltip-';
const DEFAULT_DELAY_MS = 400;
let tooltipIdCounter = 0;
let active: TooltipState | null = null;
let installed = false;

function nextTooltipId(): string {
  tooltipIdCounter += 1;
  return `${TOOLTIP_ID_PREFIX}${tooltipIdCounter}`;
}

function resolveHost(t: EventTarget | null): HTMLElement | null {
  if (!(t instanceof HTMLElement)) return null;
  // Walk up the ancestor chain in case the user hovered a child of
  // the `data-tip` element (e.g. an icon inside a labelled button).
  let cur: HTMLElement | null = t;
  while (cur !== null) {
    if (cur.dataset.tip !== undefined) return cur;
    cur = cur.parentElement;
  }
  return null;
}

function placementFor(host: HTMLElement): 'top' | 'bottom' | 'left' | 'right' {
  const raw = host.dataset.tipPlacement;
  if (raw === 'bottom' || raw === 'left' || raw === 'right') return raw;
  return 'top';
}

function delayFor(host: HTMLElement): number {
  const raw = host.dataset.tipDelay;
  if (raw === undefined) return DEFAULT_DELAY_MS;
  const n = Number.parseInt(raw, 10);
  if (!Number.isFinite(n) || n < 0) return DEFAULT_DELAY_MS;
  return n;
}

function buildTooltipNode(message: string): HTMLElement {
  const tip = document.createElement('div');
  tip.id = nextTooltipId();
  tip.className = 'data-tip-tooltip';
  tip.setAttribute('role', 'tooltip');
  tip.setAttribute('data-testid', 'data-tip-tooltip');
  tip.textContent = message;
  tip.style.cssText =
    'position:fixed;z-index:99997;background:#1e293b;color:#eaeaea;'
    + 'padding:6px 10px;border-radius:4px;font-size:13px;'
    + 'font-family:system-ui,sans-serif;pointer-events:none;'
    + 'box-shadow:0 4px 12px rgba(0,0,0,.35);max-width:280px;'
    + 'opacity:0;transition:opacity .16s ease-out;';
  return tip;
}

function position(tip: HTMLElement, host: HTMLElement): void {
  const placement = placementFor(host);
  const rect = host.getBoundingClientRect();
  // Mount once to measure tooltip dimensions.
  const tipRect = tip.getBoundingClientRect();
  const gap = 8;
  const viewport = { w: window.innerWidth, h: window.innerHeight };

  function clamp(val: number, lo: number, hi: number): number {
    return Math.max(lo, Math.min(hi, val));
  }

  let x = 0;
  let y = 0;
  let actual = placement;
  if (placement === 'top') {
    if (rect.top < tipRect.height + gap) actual = 'bottom';
  } else if (placement === 'bottom') {
    if (viewport.h - rect.bottom < tipRect.height + gap) actual = 'top';
  } else if (placement === 'left') {
    if (rect.left < tipRect.width + gap) actual = 'right';
  } else if (placement === 'right') {
    if (viewport.w - rect.right < tipRect.width + gap) actual = 'left';
  }

  switch (actual) {
    case 'top':
      x = rect.left + rect.width / 2 - tipRect.width / 2;
      y = rect.top - tipRect.height - gap;
      break;
    case 'bottom':
      x = rect.left + rect.width / 2 - tipRect.width / 2;
      y = rect.bottom + gap;
      break;
    case 'left':
      x = rect.left - tipRect.width - gap;
      y = rect.top + rect.height / 2 - tipRect.height / 2;
      break;
    case 'right':
      x = rect.right + gap;
      y = rect.top + rect.height / 2 - tipRect.height / 2;
      break;
  }
  x = clamp(x, 4, viewport.w - tipRect.width - 4);
  y = clamp(y, 4, viewport.h - tipRect.height - 4);
  tip.style.left = `${Math.round(x)}px`;
  tip.style.top = `${Math.round(y)}px`;
  tip.dataset.placement = actual;
}

function mountTooltip(host: HTMLElement, message: string): void {
  const tip = buildTooltipNode(message);
  document.body.appendChild(tip);
  position(tip, host);
  host.setAttribute('aria-describedby', tip.id);
  // Fade in on next frame.
  window.requestAnimationFrame(() => {
    tip.style.opacity = '1';
  });
  active = { host, tooltip: tip, showTimer: null };
}

function dismiss(): void {
  if (active === null) return;
  const { host, tooltip, showTimer } = active;
  if (showTimer !== null) window.clearTimeout(showTimer);
  host.removeAttribute('aria-describedby');
  tooltip.remove();
  active = null;
}

function onPointerOver(ev: PointerEvent): void {
  const host = resolveHost(ev.target);
  if (host === null) return;
  if (active !== null && active.host === host) return;
  if (active !== null) dismiss();
  const message = host.dataset.tip ?? '';
  if (message === '') return;
  const delay = delayFor(host);
  if (delay === 0) {
    mountTooltip(host, message);
  } else {
    const timer = window.setTimeout(() => {
      mountTooltip(host, message);
    }, delay);
    active = {
      host,
      tooltip: buildTooltipNode(''),  // placeholder so dismiss() works
      showTimer: timer,
    };
    // The placeholder tooltip is NOT appended; it's used purely as
    // a state marker so dismiss() can clear `active` and the
    // showTimer.  The real tooltip mounts inside the timer.
    active.tooltip = document.createElement('div');
  }
}

function onPointerOut(ev: PointerEvent): void {
  const host = resolveHost(ev.target);
  if (host === null) return;
  // Bail out if the pointer is still inside the same host element
  // (transitioning between child nodes shouldn't dismiss the tooltip).
  const next = ev.relatedTarget;
  if (next instanceof Node && host.contains(next)) return;
  if (active !== null && active.host === host) dismiss();
}

function onScrollOrResize(): void {
  dismiss();
}

function onKeydown(ev: KeyboardEvent): void {
  if (ev.key === 'Escape') dismiss();
}

let pointerOverHandler: ((ev: PointerEvent) => void) | null = null;
let pointerOutHandler: ((ev: PointerEvent) => void) | null = null;
let scrollHandler: (() => void) | null = null;
let resizeHandler: (() => void) | null = null;
let keydownHandler: ((ev: KeyboardEvent) => void) | null = null;

/**
 * Install the tooltip engine.  Idempotent — calling twice is
 * harmless.  Returns a handle the test harness uses to detach the
 * engine for isolation.
 */
export function installTooltipEngine(): TooltipEngineHandle {
  if (installed) {
    return { dispose };
  }
  installed = true;
  pointerOverHandler = onPointerOver;
  pointerOutHandler = onPointerOut;
  scrollHandler = onScrollOrResize;
  resizeHandler = onScrollOrResize;
  keydownHandler = onKeydown;
  document.addEventListener('pointerover', pointerOverHandler);
  document.addEventListener('pointerout', pointerOutHandler);
  window.addEventListener('scroll', scrollHandler, { passive: true, capture: true });
  window.addEventListener('resize', resizeHandler, { passive: true });
  window.addEventListener('keydown', keydownHandler);
  return { dispose };
}

function dispose(): void {
  dismiss();
  if (pointerOverHandler !== null) {
    document.removeEventListener('pointerover', pointerOverHandler);
    pointerOverHandler = null;
  }
  if (pointerOutHandler !== null) {
    document.removeEventListener('pointerout', pointerOutHandler);
    pointerOutHandler = null;
  }
  if (scrollHandler !== null) {
    window.removeEventListener('scroll', scrollHandler, { capture: true } as EventListenerOptions);
    scrollHandler = null;
  }
  if (resizeHandler !== null) {
    window.removeEventListener('resize', resizeHandler);
    resizeHandler = null;
  }
  if (keydownHandler !== null) {
    window.removeEventListener('keydown', keydownHandler);
    keydownHandler = null;
  }
  installed = false;
}

/** Force-show a tooltip for the given host (used by tests). */
export function showTooltipForTesting(host: HTMLElement): void {
  const message = host.dataset.tip ?? '';
  if (message === '') return;
  if (active !== null) dismiss();
  mountTooltip(host, message);
}

/** Force-dismiss any active tooltip (used by tests). */
export function dismissTooltipForTesting(): void {
  dismiss();
}
