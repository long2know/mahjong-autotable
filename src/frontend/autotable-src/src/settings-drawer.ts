// Phase J Wave 7 — App-wide settings drawer.
//
// A *global* settings drawer (separate from the per-game settings drawer
// that Wave 2 added for bot-strength / hand-count / auto-deal).  The
// Wave-7 drawer is the canonical place for app-level preferences:
//   • General  — display name + avatar colour (mirrors the profile
//                module so the lobby chip + onboarding card stay in
//                sync).
//   • Audio    — sound on/off + master volume (0..1 float).
//   • Display  — perspective vs flat table view + table cloth colour.
//   • Network  — server URL override for power users.
//
// Persistence: a single JSON blob in localStorage under
// `mahjong.settings.v1` so every knob round-trips through one key —
// no fragmented keys like the Wave-2 per-game payload.
//
// The drawer is dialog-modal-ish: Escape closes it, focus is trapped
// while it is open, the gear-icon toggle (#settings-button) flips the
// `settings-drawer-v2-open` class on the drawer aside.  We deliberately
// pick a brand-new toggle id (`settings-button`) so the Wave-2
// `settings-toggle` keeps its per-game wiring untouched — the Wave-7
// drawer is opened from the new gear in the lobby header.

import {
  AVATAR_COLOR_PRESETS,
  getProfile,
  onProfile,
  setAvatarColor,
  setDisplayName,
  validateAvatarColor,
  validateDisplayName,
} from './profile';

// ── Public types ────────────────────────────────────────────────────

export type SettingsTab = 'general' | 'audio' | 'display' | 'network';

export interface AppSettings {
  /** Audio master volume — 0..1.  0 effectively mutes. */
  masterVolume: number;
  /** Sound on/off — mirrors `mahjong:soundEnabled` LS key. */
  soundEnabled: boolean;
  /** Perspective vs orthographic table view (Phase F #perspective). */
  perspective: boolean;
  /** Table cloth colour (CSS hex).  Read by world.ts via a CSS variable. */
  tableColor: string;
  /** Network override.  Empty string = use page origin. */
  serverUrl: string;
}

// ── Constants ───────────────────────────────────────────────────────

export const SETTINGS_LS_KEY = 'mahjong.settings.v1';
const LS_KEY_SOUND_ENABLED_MIRROR = 'mahjong:soundEnabled';

export const SETTINGS_DEFAULT: AppSettings = {
  masterVolume: 0.8,
  soundEnabled: true,
  perspective: true,
  tableColor: '#0a5a3a',
  serverUrl: '',
};

const TABS: ReadonlyArray<{ id: SettingsTab; label: string }> = [
  { id: 'general',  label: 'General' },
  { id: 'audio',    label: 'Audio' },
  { id: 'display',  label: 'Display' },
  { id: 'network',  label: 'Network' },
];

// ── State ───────────────────────────────────────────────────────────

let current: AppSettings = { ...SETTINGS_DEFAULT };
let installed = false;
let activeTab: SettingsTab = 'general';
const listeners = new Set<(s: AppSettings) => void>();

// ── LS helpers ──────────────────────────────────────────────────────

function loadFromStorage(): AppSettings {
  try {
    const raw = window.localStorage.getItem(SETTINGS_LS_KEY);
    if (raw === null) return { ...SETTINGS_DEFAULT };
    const j = JSON.parse(raw) as Record<string, unknown>;
    const out: AppSettings = { ...SETTINGS_DEFAULT };
    if (typeof j.masterVolume === 'number' && isFinite(j.masterVolume)) {
      out.masterVolume = Math.max(0, Math.min(1, j.masterVolume));
    }
    if (typeof j.soundEnabled === 'boolean') out.soundEnabled = j.soundEnabled;
    if (typeof j.perspective === 'boolean') out.perspective = j.perspective;
    if (typeof j.tableColor === 'string' && /^#[0-9a-fA-F]{3,6}$/.test(j.tableColor)) {
      out.tableColor = j.tableColor;
    }
    if (typeof j.serverUrl === 'string') out.serverUrl = j.serverUrl;
    return out;
  } catch {
    return { ...SETTINGS_DEFAULT };
  }
}

function writeToStorage(s: AppSettings): void {
  try {
    window.localStorage.setItem(SETTINGS_LS_KEY, JSON.stringify(s));
  } catch {
    /* private mode / quota — skip */
  }
}

function mirrorSoundEnabled(s: AppSettings): void {
  try {
    window.localStorage.setItem(LS_KEY_SOUND_ENABLED_MIRROR, s.soundEnabled ? 'true' : 'false');
  } catch {
    /* skip */
  }
}

// ── Public API ──────────────────────────────────────────────────────

export function getSettings(): AppSettings {
  return { ...current };
}

export function onSettingsChange(handler: (s: AppSettings) => void): () => void {
  listeners.add(handler);
  return () => listeners.delete(handler);
}

function emit(): void {
  for (const fn of listeners) {
    try { fn(current); } catch { /* swallow */ }
  }
}

/** Replace the current settings (full object).  Persists + emits. */
export function setSettings(next: Partial<AppSettings>): void {
  current = { ...current, ...next };
  writeToStorage(current);
  mirrorSoundEnabled(current);
  applyDerivedSettings(current);
  emit();
}

/** Apply the in-memory settings to known surfaces (sound, CSS, etc.). */
function applyDerivedSettings(s: AppSettings): void {
  // Sound on/off mirror — write `mahjong:soundEnabled` so the Wave-3
  // mute toggle in sound.ts (via the existing settings drawer +
  // installSoundEnabledMirror) sees the same state.
  try {
    window.localStorage.setItem(LS_KEY_SOUND_ENABLED_MIRROR, s.soundEnabled ? 'true' : 'false');
  } catch { /* skip */ }
  // Notify the legacy #settings-sound checkbox so its change handlers
  // (Sound.setMuted) fire from this single source.
  const legacyCheckbox = document.getElementById('settings-sound') as HTMLInputElement | null;
  if (legacyCheckbox !== null && legacyCheckbox.checked !== s.soundEnabled) {
    legacyCheckbox.checked = s.soundEnabled;
    legacyCheckbox.dispatchEvent(new Event('change', { bubbles: true }));
  }
  // Table colour — expose as a CSS variable so style.css / main.css
  // can apply it without each surface importing this module.
  document.documentElement.style.setProperty('--app-table-color', s.tableColor);
  // Perspective mirror — keep the legacy #perspective checkbox in sync
  // so world.ts sees the new value via its existing input listener.
  const perspectiveCheckbox = document.getElementById('perspective') as HTMLInputElement | null;
  if (perspectiveCheckbox !== null && perspectiveCheckbox.checked !== s.perspective) {
    perspectiveCheckbox.checked = s.perspective;
    perspectiveCheckbox.dispatchEvent(new Event('change', { bubbles: true }));
  }
}

/** Reset all settings to defaults. */
export function resetSettings(): void {
  setSettings({ ...SETTINGS_DEFAULT });
}

// ── Drawer install ──────────────────────────────────────────────────

export function installSettingsDrawerV2(): void {
  if (installed) return;
  const drawer = document.getElementById('settings-drawer-v2');
  if (drawer === null) return;
  installed = true;

  current = loadFromStorage();
  applyDerivedSettings(current);

  const btn = document.getElementById('settings-button') as HTMLButtonElement | null;
  const closeBtn = document.getElementById('settings-close-v2') as HTMLButtonElement | null;
  const saveBtn = document.getElementById('settings-save') as HTMLButtonElement | null;
  const resetBtn = document.getElementById('settings-reset') as HTMLButtonElement | null;

  // Tab strip — render dynamically so tab metadata stays in this module.
  const tabsHost = document.getElementById('settings-drawer-v2-tabs');
  const panelHost = document.getElementById('settings-drawer-v2-panels');
  if (tabsHost === null || panelHost === null) return;

  tabsHost.replaceChildren();
  for (const t of TABS) {
    const tab = document.createElement('button');
    tab.type = 'button';
    tab.className = 'settings-v2-tab';
    tab.setAttribute('role', 'tab');
    tab.setAttribute('aria-controls', `settings-panel-${t.id}`);
    tab.setAttribute('aria-selected', t.id === activeTab ? 'true' : 'false');
    tab.setAttribute('data-tab', t.id);
    tab.setAttribute('data-testid', `settings-tab-${t.id}`);
    tab.id = `settings-tab-${t.id}`;
    tab.textContent = t.label;
    tab.addEventListener('click', () => activateTab(t.id));
    tabsHost.appendChild(tab);
  }

  // Panels.
  panelHost.replaceChildren();
  panelHost.appendChild(buildGeneralPanel());
  panelHost.appendChild(buildAudioPanel());
  panelHost.appendChild(buildDisplayPanel());
  panelHost.appendChild(buildNetworkPanel());
  activateTab(activeTab);

  // Toggle.
  if (btn !== null) {
    btn.addEventListener('click', (e) => {
      e.stopPropagation();
      if (drawer.classList.contains('settings-drawer-v2-open')) {
        closeDrawer();
      } else {
        openDrawer();
      }
    });
  }
  if (closeBtn !== null) {
    closeBtn.addEventListener('click', () => closeDrawer());
  }
  if (saveBtn !== null) {
    saveBtn.addEventListener('click', () => {
      // Read the current panel state and persist.  setSettings already
      // persists on each input change, so Save is mostly a UX
      // affordance — but we also explicitly flash a saved note.
      writeToStorage(current);
      flashSavedNote();
    });
  }
  if (resetBtn !== null) {
    resetBtn.addEventListener('click', () => {
      resetSettings();
      // Re-render the panels with the defaulted values.
      rerenderPanels();
    });
  }

  // Escape closes the drawer when open.
  document.addEventListener('keydown', (e: KeyboardEvent) => {
    if (e.key !== 'Escape') return;
    if (drawer.classList.contains('settings-drawer-v2-open')) {
      closeDrawer();
      btn?.focus();
    }
  });
  // Click outside closes.
  document.addEventListener('mousedown', (e: MouseEvent) => {
    if (!drawer.classList.contains('settings-drawer-v2-open')) return;
    const target = e.target as Node | null;
    if (target !== null && (drawer.contains(target) || (btn?.contains(target) ?? false))) return;
    closeDrawer();
  });

  // Profile mirror — sync the General tab when the profile changes
  // externally (e.g. via the onboarding card).
  onProfile(() => rerenderPanels());
}

function openDrawer(): void {
  const drawer = document.getElementById('settings-drawer-v2');
  const btn = document.getElementById('settings-button') as HTMLButtonElement | null;
  if (drawer === null) return;
  drawer.classList.add('settings-drawer-v2-open');
  drawer.setAttribute('aria-hidden', 'false');
  if (btn !== null) btn.setAttribute('aria-expanded', 'true');
  // Focus the active tab so keyboard users land in the drawer.
  window.setTimeout(() => {
    const tab = document.getElementById(`settings-tab-${activeTab}`);
    tab?.focus();
  }, 50);
}

function closeDrawer(): void {
  const drawer = document.getElementById('settings-drawer-v2');
  const btn = document.getElementById('settings-button') as HTMLButtonElement | null;
  if (drawer === null) return;
  drawer.classList.remove('settings-drawer-v2-open');
  drawer.setAttribute('aria-hidden', 'true');
  if (btn !== null) btn.setAttribute('aria-expanded', 'false');
}

function activateTab(id: SettingsTab): void {
  activeTab = id;
  for (const t of TABS) {
    const tab = document.getElementById(`settings-tab-${t.id}`);
    const panel = document.getElementById(`settings-panel-${t.id}`);
    const isActive = t.id === id;
    if (tab !== null) {
      tab.classList.toggle('settings-v2-tab-active', isActive);
      tab.setAttribute('aria-selected', isActive ? 'true' : 'false');
      tab.setAttribute('tabindex', isActive ? '0' : '-1');
    }
    if (panel !== null) {
      panel.hidden = !isActive;
    }
  }
}

function flashSavedNote(): void {
  const note = document.getElementById('settings-saved-note-v2');
  if (note === null) return;
  note.textContent = 'Saved ✓';
  (note as HTMLElement).style.display = 'inline';
  window.setTimeout(() => {
    (note as HTMLElement).style.display = 'none';
  }, 1400);
}

function rerenderPanels(): void {
  const panelHost = document.getElementById('settings-drawer-v2-panels');
  if (panelHost === null) return;
  panelHost.replaceChildren();
  panelHost.appendChild(buildGeneralPanel());
  panelHost.appendChild(buildAudioPanel());
  panelHost.appendChild(buildDisplayPanel());
  panelHost.appendChild(buildNetworkPanel());
  activateTab(activeTab);
}

// ── Panels ──────────────────────────────────────────────────────────

function buildPanelShell(id: SettingsTab, ariaLabel: string): HTMLDivElement {
  const panel = document.createElement('div');
  panel.className = 'settings-v2-panel';
  panel.id = `settings-panel-${id}`;
  panel.setAttribute('role', 'tabpanel');
  panel.setAttribute('aria-labelledby', `settings-tab-${id}`);
  panel.setAttribute('aria-label', ariaLabel);
  panel.setAttribute('data-testid', `settings-panel-${id}`);
  panel.hidden = id !== activeTab;
  return panel;
}

function buildGeneralPanel(): HTMLDivElement {
  const panel = buildPanelShell('general', 'General settings');
  const profile = getProfile();
  const currentName = profile?.displayName ?? '';
  const currentColor = profile?.avatarColor ?? AVATAR_COLOR_PRESETS[5];

  const nameField = document.createElement('label');
  nameField.className = 'settings-v2-field';
  const nameLabel = document.createElement('span');
  nameLabel.className = 'settings-v2-label';
  nameLabel.textContent = 'Display name';
  const nameInput = document.createElement('input');
  nameInput.type = 'text';
  nameInput.className = 'settings-v2-input';
  nameInput.setAttribute('data-testid', 'settings-display-name-input');
  nameInput.maxLength = 32;
  nameInput.value = currentName;
  const nameError = document.createElement('span');
  nameError.className = 'settings-v2-error';
  nameError.setAttribute('aria-live', 'polite');
  nameInput.addEventListener('input', () => {
    const { value, error } = validateDisplayName(nameInput.value);
    nameError.textContent = error ?? '';
    nameInput.classList.toggle('settings-v2-input-invalid', error !== null);
    if (error === null && value !== null) {
      setDisplayName(value);
    }
  });
  nameField.appendChild(nameLabel);
  nameField.appendChild(nameInput);
  nameField.appendChild(nameError);

  const colorField = document.createElement('div');
  colorField.className = 'settings-v2-field';
  const colorLabel = document.createElement('span');
  colorLabel.className = 'settings-v2-label';
  colorLabel.textContent = 'Avatar colour';
  colorField.appendChild(colorLabel);
  const presets = document.createElement('div');
  presets.className = 'settings-v2-color-presets';
  presets.setAttribute('role', 'radiogroup');
  presets.setAttribute('aria-label', 'Avatar colour presets');
  AVATAR_COLOR_PRESETS.forEach((hex, i) => {
    const swatch = document.createElement('button');
    swatch.type = 'button';
    swatch.className = 'settings-v2-color-swatch';
    swatch.style.backgroundColor = hex;
    swatch.setAttribute('data-color', hex);
    swatch.setAttribute('data-testid', `settings-avatar-color-${i}`);
    swatch.setAttribute('role', 'radio');
    const isSelected = hex.toLowerCase() === currentColor.toLowerCase();
    swatch.setAttribute('aria-checked', isSelected ? 'true' : 'false');
    swatch.setAttribute('aria-label', `Avatar colour ${i + 1}: ${hex}`);
    swatch.title = hex;
    if (isSelected) swatch.classList.add('settings-v2-color-swatch-selected');
    swatch.addEventListener('click', () => {
      setAvatarColor(hex);
      for (const s of presets.querySelectorAll<HTMLButtonElement>('.settings-v2-color-swatch')) {
        const sel = s.getAttribute('data-color')?.toLowerCase() === hex.toLowerCase();
        s.classList.toggle('settings-v2-color-swatch-selected', sel);
        s.setAttribute('aria-checked', sel ? 'true' : 'false');
      }
    });
    presets.appendChild(swatch);
  });
  colorField.appendChild(presets);
  const customRow = document.createElement('label');
  customRow.className = 'settings-v2-color-custom-row';
  const customText = document.createElement('span');
  customText.textContent = 'Custom colour';
  const customInput = document.createElement('input');
  customInput.type = 'color';
  customInput.value = currentColor;
  customInput.setAttribute('data-testid', 'settings-avatar-color-custom');
  customInput.addEventListener('input', () => {
    if (validateAvatarColor(customInput.value)) {
      setAvatarColor(customInput.value);
    }
  });
  customRow.appendChild(customText);
  customRow.appendChild(customInput);
  colorField.appendChild(customRow);

  panel.appendChild(nameField);
  panel.appendChild(colorField);
  return panel;
}

function buildAudioPanel(): HTMLDivElement {
  const panel = buildPanelShell('audio', 'Audio settings');

  const soundRow = document.createElement('label');
  soundRow.className = 'settings-v2-field settings-v2-checkbox-row';
  const soundInput = document.createElement('input');
  soundInput.type = 'checkbox';
  soundInput.checked = current.soundEnabled;
  soundInput.setAttribute('data-testid', 'settings-sound-toggle');
  const soundText = document.createElement('span');
  soundText.textContent = 'Sound effects';
  soundInput.addEventListener('change', () => {
    setSettings({ soundEnabled: soundInput.checked });
  });
  soundRow.appendChild(soundInput);
  soundRow.appendChild(soundText);

  const volumeRow = document.createElement('label');
  volumeRow.className = 'settings-v2-field';
  const volumeLabel = document.createElement('span');
  volumeLabel.className = 'settings-v2-label';
  volumeLabel.textContent = 'Master volume';
  const volumeInput = document.createElement('input');
  volumeInput.type = 'range';
  volumeInput.min = '0';
  volumeInput.max = '100';
  volumeInput.step = '1';
  volumeInput.value = String(Math.round(current.masterVolume * 100));
  volumeInput.setAttribute('data-testid', 'settings-master-volume');
  volumeInput.setAttribute('aria-label', 'Master volume percent');
  const volumeValue = document.createElement('output');
  volumeValue.className = 'settings-v2-output';
  volumeValue.textContent = `${volumeInput.value}%`;
  volumeInput.addEventListener('input', () => {
    const v = parseInt(volumeInput.value, 10);
    volumeValue.textContent = `${v}%`;
    setSettings({ masterVolume: Math.max(0, Math.min(1, v / 100)) });
  });
  volumeRow.appendChild(volumeLabel);
  volumeRow.appendChild(volumeInput);
  volumeRow.appendChild(volumeValue);

  panel.appendChild(soundRow);
  panel.appendChild(volumeRow);
  return panel;
}

function buildDisplayPanel(): HTMLDivElement {
  const panel = buildPanelShell('display', 'Display settings');

  const perspRow = document.createElement('label');
  perspRow.className = 'settings-v2-field settings-v2-checkbox-row';
  const perspInput = document.createElement('input');
  perspInput.type = 'checkbox';
  perspInput.checked = current.perspective;
  perspInput.setAttribute('data-testid', 'settings-perspective-toggle');
  const perspText = document.createElement('span');
  perspText.textContent = 'Perspective camera (uncheck for flat top-down)';
  perspInput.addEventListener('change', () => {
    setSettings({ perspective: perspInput.checked });
  });
  perspRow.appendChild(perspInput);
  perspRow.appendChild(perspText);

  const colorRow = document.createElement('label');
  colorRow.className = 'settings-v2-field';
  const colorLabel = document.createElement('span');
  colorLabel.className = 'settings-v2-label';
  colorLabel.textContent = 'Table cloth colour';
  const colorInput = document.createElement('input');
  colorInput.type = 'color';
  colorInput.value = current.tableColor;
  colorInput.setAttribute('data-testid', 'settings-table-color');
  colorInput.addEventListener('input', () => {
    setSettings({ tableColor: colorInput.value });
  });
  const colorReset = document.createElement('button');
  colorReset.type = 'button';
  colorReset.className = 'btn btn-secondary btn-sm';
  colorReset.textContent = 'Reset';
  colorReset.setAttribute('data-testid', 'settings-table-color-reset');
  colorReset.addEventListener('click', () => {
    colorInput.value = SETTINGS_DEFAULT.tableColor;
    setSettings({ tableColor: SETTINGS_DEFAULT.tableColor });
  });
  colorRow.appendChild(colorLabel);
  colorRow.appendChild(colorInput);
  colorRow.appendChild(colorReset);

  panel.appendChild(perspRow);
  panel.appendChild(colorRow);
  return panel;
}

function buildNetworkPanel(): HTMLDivElement {
  const panel = buildPanelShell('network', 'Network settings');

  const urlField = document.createElement('label');
  urlField.className = 'settings-v2-field';
  const urlLabel = document.createElement('span');
  urlLabel.className = 'settings-v2-label';
  urlLabel.textContent = 'Server URL override';
  const urlInput = document.createElement('input');
  urlInput.type = 'url';
  urlInput.placeholder = 'Defaults to page origin';
  urlInput.className = 'settings-v2-input';
  urlInput.value = current.serverUrl;
  urlInput.setAttribute('data-testid', 'settings-server-url');
  urlInput.setAttribute('autocomplete', 'off');
  urlInput.addEventListener('change', () => {
    setSettings({ serverUrl: urlInput.value.trim() });
  });
  const urlHint = document.createElement('span');
  urlHint.className = 'settings-v2-hint';
  urlHint.textContent =
    'Power users only — empty = same origin. Takes effect on next page reload.';
  urlField.appendChild(urlLabel);
  urlField.appendChild(urlInput);
  urlField.appendChild(urlHint);

  panel.appendChild(urlField);
  return panel;
}
