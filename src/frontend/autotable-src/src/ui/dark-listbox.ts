// Ferro — dark-listbox: platform-independent, screenshottable custom listbox
// for setup/settings selects (UAT G5).
//
// Why: the native <select> option popup is drawn by the OS and ignores author
// option{background} on many platforms (macOS/GTK), so `.dark-select` rendered
// white-on-light and unreadable. The shipped native fix is `color-scheme: dark`
// on `.dark-select` (style.css) — reliable on Chromium/Tauri/mobile. This
// module is the PLATFORM-INDEPENDENT alternative: it renders the popup as
// ordinary DOM (fully styleable, screenshottable, dark + high-contrast) while
// keeping the native <select> in place for value + `change` compatibility and
// full keyboard/touch/a11y (role=listbox/option, aria-activedescendant,
// arrows/Enter/Escape/Home/End). It AUTO-ENHANCES every setup/lobby/settings
// `select.dark-select` / `.settings-v2-select` (single-choice only),
// idempotently, and keeps the native <select> in place (aria-hidden when
// enhanced) for value/`change` so it cannot disturb existing app handlers.

import './dark-listbox.css';

interface Opt { value: string; label: string; disabled: boolean; group: string | null; }

function readOptions(select: HTMLSelectElement): Opt[] {
  const opts: Opt[] = [];
  for (const node of Array.from(select.children)) {
    if (node instanceof HTMLOptGroupElement) {
      for (const o of Array.from(node.children)) {
        if (o instanceof HTMLOptionElement) opts.push({ value: o.value, label: o.textContent || o.value, disabled: o.disabled, group: node.label });
      }
    } else if (node instanceof HTMLOptionElement) {
      opts.push({ value: node.value, label: node.textContent || node.value, disabled: node.disabled, group: null });
    }
  }
  return opts;
}

let uid = 0;

export function enhanceDarkSelect(select: HTMLSelectElement): void {
  if (select.dataset.darkListboxReady === '1') return;
  select.dataset.darkListboxReady = '1';
  const id = `dark-listbox-${++uid}`;

  const wrap = document.createElement('div');
  wrap.className = 'dark-listbox';
  select.parentNode?.insertBefore(wrap, select);
  wrap.appendChild(select);
  select.classList.add('dark-listbox-native');
  select.setAttribute('tabindex', '-1');
  select.setAttribute('aria-hidden', 'true');

  const aria = select.getAttribute('aria-label');
  const trigger = document.createElement('button');
  trigger.type = 'button';
  trigger.id = `${id}-trigger`;
  trigger.className = 'dark-listbox-trigger dark-select';
  trigger.setAttribute('aria-haspopup', 'listbox');
  trigger.setAttribute('aria-expanded', 'false');
  if (aria) trigger.setAttribute('aria-label', aria);
  wrap.appendChild(trigger);

  const popup = document.createElement('div');
  popup.className = 'dark-listbox-popup';
  popup.setAttribute('role', 'listbox');
  if (aria) popup.setAttribute('aria-label', aria);
  popup.hidden = true;
  wrap.appendChild(popup);

  let opts = readOptions(select);
  let active = Math.max(0, opts.findIndex((o) => o.value === select.value));

  const labelFor = (v: string): string => (opts.find((o) => o.value === v)?.label ?? v);
  const renderTrigger = (): void => { trigger.textContent = labelFor(select.value); };

  const renderPopup = (): void => {
    popup.textContent = '';
    let lastGroup: string | null = null;
    opts.forEach((o, i) => {
      if (o.group && o.group !== lastGroup) {
        const g = document.createElement('div');
        g.className = 'dark-listbox-group';
        g.setAttribute('role', 'presentation');
        g.textContent = o.group;
        popup.appendChild(g);
        lastGroup = o.group;
      }
      const item = document.createElement('div');
      item.className = 'dark-listbox-option';
      item.id = `${id}-opt-${i}`;
      item.setAttribute('role', 'option');
      item.setAttribute('aria-selected', String(o.value === select.value));
      if (o.disabled) item.setAttribute('aria-disabled', 'true');
      if (i === active) item.classList.add('active');
      item.textContent = o.label;
      item.addEventListener('click', () => { if (!o.disabled) choose(i); });
      popup.appendChild(item);
    });
  };

  const syncActiveDescendant = (): void => {
    const el = popup.querySelector(`#${id}-opt-${active}`);
    if (el) { trigger.setAttribute('aria-activedescendant', el.id); el.scrollIntoView({ block: 'nearest' }); }
  };

  const onDocDown = (e: Event): void => { if (!wrap.contains(e.target as Node)) close(); };

  function open(): void {
    opts = readOptions(select);
    active = Math.max(0, opts.findIndex((o) => o.value === select.value));
    renderPopup();
    popup.hidden = false;
    trigger.setAttribute('aria-expanded', 'true');
    syncActiveDescendant();
    document.addEventListener('mousedown', onDocDown, true);
  }
  function close(): void {
    popup.hidden = true;
    trigger.setAttribute('aria-expanded', 'false');
    trigger.removeAttribute('aria-activedescendant');
    document.removeEventListener('mousedown', onDocDown, true);
  }
  function choose(i: number): void {
    const o = opts[i];
    if (o.disabled) return;
    active = i;
    if (select.value !== o.value) { select.value = o.value; select.dispatchEvent(new Event('change', { bubbles: true })); }
    renderTrigger();
    close();
    trigger.focus();
  }
  function move(delta: number): void {
    let i = active;
    for (let n = 0; n < opts.length; n++) { i = (i + delta + opts.length) % opts.length; if (!opts[i].disabled) break; }
    active = i;
    renderPopup();
    syncActiveDescendant();
  }

  trigger.addEventListener('click', () => { if (popup.hidden) open(); else close(); });
  trigger.addEventListener('keydown', (e: KeyboardEvent) => {
    if (popup.hidden) {
      if (e.key === 'ArrowDown' || e.key === 'ArrowUp' || e.key === 'Enter' || e.key === ' ') { e.preventDefault(); open(); }
      return;
    }
    if (e.key === 'ArrowDown') { e.preventDefault(); move(1); }
    else if (e.key === 'ArrowUp') { e.preventDefault(); move(-1); }
    else if (e.key === 'Home') { e.preventDefault(); active = -1; move(1); }
    else if (e.key === 'End') { e.preventDefault(); active = 0; move(-1); }
    else if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); choose(active); }
    else if (e.key === 'Escape') { e.preventDefault(); close(); trigger.focus(); }
  });
  // If code (or the native control) changes the value, keep the trigger in sync.
  select.addEventListener('change', renderTrigger);

  renderTrigger();
}

export function installDarkListbox(root?: ParentNode): void {
  if (typeof document === 'undefined') return;
  const scope: ParentNode = root ?? document;
  const run = (): void => {
    // Auto-enhance every real setup / lobby / settings dropdown. Single-choice
    // dropdowns only (skip multiple / listbox-sized). Idempotent via the
    // per-select `darkListboxReady` guard, so the observer can never create
    // duplicate/orphan listboxes.
    for (const s of Array.from(scope.querySelectorAll<HTMLSelectElement>('select.dark-select, select.settings-v2-select'))) {
      if (s.multiple || s.size > 1 || s.classList.contains('dark-listbox-native')) continue;
      enhanceDarkSelect(s);
    }
  };
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', run, { once: true });
  else run();
  new MutationObserver(run).observe(document.documentElement, { childList: true, subtree: true });
}

installDarkListbox();
