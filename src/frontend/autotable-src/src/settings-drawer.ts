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
import { renderEditorPanel as renderRulePresetsEditor } from './rule-presets';
import {
  getMotionPreference,
  getThemePreference,
  setMotionPreference,
  setThemePreference,
  type MotionPreference,
  type ThemePreference,
} from './theme';
import {
  getLanguage,
  setLanguage,
  onLanguageChange,
  t,
  type LanguagePreference,
} from './i18n';
import { hideEl } from './dom-utils';

// ── Public types ────────────────────────────────────────────────────

export type SettingsTab = 'general' | 'audio' | 'display' | 'network' | 'rule-presets';

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

const TABS: ReadonlyArray<{ id: SettingsTab; labelKey: string; fallback: string }> = [
  { id: 'general',       labelKey: 'settings.tab.general',       fallback: 'General' },
  { id: 'audio',         labelKey: 'settings.tab.audio',         fallback: 'Audio' },
  { id: 'display',       labelKey: 'settings.tab.display',       fallback: 'Display' },
  { id: 'network',       labelKey: 'settings.tab.network',       fallback: 'Network' },
  { id: 'rule-presets',  labelKey: 'settings.tab.rule_presets',  fallback: 'Rule presets' },
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
  for (const tab_ of TABS) {
    const tab = document.createElement('button');
    tab.type = 'button';
    tab.className = 'settings-v2-tab';
    tab.setAttribute('role', 'tab');
    tab.setAttribute('aria-controls', `settings-panel-${tab_.id}`);
    tab.setAttribute('aria-selected', tab_.id === activeTab ? 'true' : 'false');
    tab.setAttribute('data-tab', tab_.id);
    tab.setAttribute('data-testid', `settings-tab-${tab_.id}`);
    tab.id = `settings-tab-${tab_.id}`;
    tab.textContent = t(tab_.labelKey) || tab_.fallback;
    tab.addEventListener('click', () => activateTab(tab_.id));
    tabsHost.appendChild(tab);
  }

  // Panels.
  panelHost.replaceChildren();
  panelHost.appendChild(buildGeneralPanel());
  panelHost.appendChild(buildAudioPanel());
  panelHost.appendChild(buildDisplayPanel());
  panelHost.appendChild(buildNetworkPanel());
  panelHost.appendChild(buildRulePresetsPanel());
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

  // Phase J Wave 9 — re-render the drawer's tab strip + panel labels
  // whenever the active locale changes (Language picker change).
  onLanguageChange(() => {
    // Re-render tabs (textContent depends on t()).
    for (const tab_ of TABS) {
      const tabEl = document.getElementById(`settings-tab-${tab_.id}`);
      if (tabEl !== null) tabEl.textContent = t(tab_.labelKey) || tab_.fallback;
    }
    rerenderPanels();
  });
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
  for (const tab_ of TABS) {
    const tab = document.getElementById(`settings-tab-${tab_.id}`);
    const panel = document.getElementById(`settings-panel-${tab_.id}`);
    const isActive = tab_.id === id;
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
  note.textContent = t('settings.saved') || 'Saved ✓';
  (note as HTMLElement).hidden = false;
  (note as HTMLElement).style.display = 'inline';
  window.setTimeout(() => {
    hideEl(note as HTMLElement);
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
  panelHost.appendChild(buildRulePresetsPanel());
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
  const panel = buildPanelShell('general', t('settings.tab.general') || 'General settings');
  const profile = getProfile();
  const currentName = profile?.displayName ?? '';
  const currentColor = profile?.avatarColor ?? AVATAR_COLOR_PRESETS[5];

  const nameField = document.createElement('label');
  nameField.className = 'settings-v2-field';
  const nameLabel = document.createElement('span');
  nameLabel.className = 'settings-v2-label';
  nameLabel.textContent = t('settings.display_name') || 'Display name';
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
  colorLabel.textContent = t('settings.avatar_color') || 'Avatar colour';
  colorField.appendChild(colorLabel);
  const presets = document.createElement('div');
  presets.className = 'settings-v2-color-presets';
  presets.setAttribute('role', 'radiogroup');
  presets.setAttribute('aria-label', t('settings.avatar_color') || 'Avatar colour presets');
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

  // Phase J Wave 9 — Language selector.  Persists to
  // `mahjong.settings.v1.lang` via i18n.ts; default Auto follows
  // navigator.language family detection.
  const langRow = document.createElement('label');
  langRow.className = 'settings-v2-field';
  const langLabel = document.createElement('span');
  langLabel.className = 'settings-v2-label';
  langLabel.textContent = t('settings.language') || 'Language';
  const langSelect = document.createElement('select');
  langSelect.className = 'dark-select form-control form-control-sm';
  langSelect.setAttribute('data-testid', 'settings-language-select');
  langSelect.setAttribute('aria-label', t('settings.language') || 'Language preference');
  for (const opt of [
    { v: 'auto',    label: t('settings.language_auto')    || 'Auto (browser default)' },
    { v: 'en',      label: t('settings.language_en')      || 'English' },
    { v: 'zh-Hans', label: t('settings.language_zh_hans') || '简体中文' },
    { v: 'zh-Hant', label: t('settings.language_zh_hant') || '繁體中文' },
  ] as ReadonlyArray<{ v: LanguagePreference; label: string }>) {
    const o = document.createElement('option');
    o.value = opt.v;
    o.textContent = opt.label;
    langSelect.appendChild(o);
  }
  langSelect.value = getLanguage();
  langSelect.addEventListener('change', () => {
    setLanguage(langSelect.value as LanguagePreference);
  });
  langRow.appendChild(langLabel);
  langRow.appendChild(langSelect);

  panel.appendChild(nameField);
  panel.appendChild(colorField);
  panel.appendChild(langRow);
  return panel;
}

function buildAudioPanel(): HTMLDivElement {
  const panel = buildPanelShell('audio', t('settings.tab.audio') || 'Audio settings');

  const soundRow = document.createElement('label');
  soundRow.className = 'settings-v2-field settings-v2-checkbox-row';
  const soundInput = document.createElement('input');
  soundInput.type = 'checkbox';
  soundInput.checked = current.soundEnabled;
  soundInput.setAttribute('data-testid', 'settings-sound-toggle');
  const soundText = document.createElement('span');
  soundText.textContent = t('settings.sound_effects') || 'Sound effects';
  soundInput.addEventListener('change', () => {
    setSettings({ soundEnabled: soundInput.checked });
  });
  soundRow.appendChild(soundInput);
  soundRow.appendChild(soundText);

  const volumeRow = document.createElement('label');
  volumeRow.className = 'settings-v2-field';
  const volumeLabel = document.createElement('span');
  volumeLabel.className = 'settings-v2-label';
  volumeLabel.textContent = t('settings.master_volume') || 'Master volume';
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
  const panel = buildPanelShell('display', t('settings.tab.display') || 'Display settings');

  const perspRow = document.createElement('label');
  perspRow.className = 'settings-v2-field settings-v2-checkbox-row';
  const perspInput = document.createElement('input');
  perspInput.type = 'checkbox';
  perspInput.checked = current.perspective;
  perspInput.setAttribute('data-testid', 'settings-perspective-toggle');
  const perspText = document.createElement('span');
  perspText.textContent = t('settings.perspective') || 'Perspective camera (uncheck for flat top-down)';
  perspInput.addEventListener('change', () => {
    setSettings({ perspective: perspInput.checked });
  });
  perspRow.appendChild(perspInput);
  perspRow.appendChild(perspText);

  const colorRow = document.createElement('label');
  colorRow.className = 'settings-v2-field';
  const colorLabel = document.createElement('span');
  colorLabel.className = 'settings-v2-label';
  colorLabel.textContent = t('settings.table_color') || 'Table cloth colour';
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
  colorReset.textContent = t('common.reset') || 'Reset';
  colorReset.setAttribute('data-testid', 'settings-table-color-reset');
  colorReset.addEventListener('click', () => {
    colorInput.value = SETTINGS_DEFAULT.tableColor;
    setSettings({ tableColor: SETTINGS_DEFAULT.tableColor });
  });
  colorRow.appendChild(colorLabel);
  colorRow.appendChild(colorInput);
  colorRow.appendChild(colorReset);

  // Phase J Wave 8 — Motion preference (Auto/Reduced/Full).  Honours
  // prefers-reduced-motion when Auto; overrides the OS preference on
  // Reduced/Full.  Stored in theme.ts so the choice persists across
  // page reloads.
  const motionRow = document.createElement('label');
  motionRow.className = 'settings-v2-field';
  const motionLabel = document.createElement('span');
  motionLabel.className = 'settings-v2-label';
  motionLabel.textContent = t('settings.motion') || 'Motion';
  const motionSelect = document.createElement('select');
  motionSelect.className = 'dark-select form-control form-control-sm';
  motionSelect.setAttribute('data-testid', 'settings-motion-select');
  motionSelect.setAttribute('aria-label', t('settings.motion') || 'Motion preference');
  for (const opt of [
    { v: 'auto',    label: t('settings.motion_auto')    || 'Auto (follow OS preference)' },
    { v: 'reduced', label: t('settings.motion_reduced') || 'Reduced (disable animations)' },
    { v: 'full',    label: t('settings.motion_full')    || 'Full (always animate)' },
  ] as ReadonlyArray<{ v: MotionPreference; label: string }>) {
    const o = document.createElement('option');
    o.value = opt.v;
    o.textContent = opt.label;
    motionSelect.appendChild(o);
  }
  motionSelect.value = getMotionPreference();
  motionSelect.addEventListener('change', () => {
    setMotionPreference(motionSelect.value as MotionPreference);
  });
  motionRow.appendChild(motionLabel);
  motionRow.appendChild(motionSelect);

  // Phase J Wave 8 — Theme preference (Auto/Light/Dark).  Honours
  // prefers-color-scheme: dark when Auto; the 3D table itself is
  // textured and is NOT recolored.
  const themeRow = document.createElement('label');
  themeRow.className = 'settings-v2-field';
  const themeLabel = document.createElement('span');
  themeLabel.className = 'settings-v2-label';
  themeLabel.textContent = t('settings.theme') || 'Theme';
  const themeSelect = document.createElement('select');
  themeSelect.className = 'dark-select form-control form-control-sm';
  themeSelect.setAttribute('data-testid', 'settings-theme-select');
  themeSelect.setAttribute('aria-label', t('settings.theme') || 'Theme preference');
  for (const opt of [
    { v: 'auto',  label: t('settings.theme_auto')  || 'Auto (follow OS preference)' },
    { v: 'light', label: t('settings.theme_light') || 'Light' },
    { v: 'dark',  label: t('settings.theme_dark')  || 'Dark' },
  ] as ReadonlyArray<{ v: ThemePreference; label: string }>) {
    const o = document.createElement('option');
    o.value = opt.v;
    o.textContent = opt.label;
    themeSelect.appendChild(o);
  }
  themeSelect.value = getThemePreference();
  themeSelect.addEventListener('change', () => {
    setThemePreference(themeSelect.value as ThemePreference);
  });
  themeRow.appendChild(themeLabel);
  themeRow.appendChild(themeSelect);

  panel.appendChild(perspRow);
  panel.appendChild(colorRow);
  panel.appendChild(motionRow);
  panel.appendChild(themeRow);
  return panel;
}

function buildNetworkPanel(): HTMLDivElement {
  const panel = buildPanelShell('network', t('settings.tab.network') || 'Network settings');

  const urlField = document.createElement('label');
  urlField.className = 'settings-v2-field';
  const urlLabel = document.createElement('span');
  urlLabel.className = 'settings-v2-label';
  urlLabel.textContent = t('settings.server_url') || 'Server URL override';
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

  // Phase K Wave 3 — Per-game "Enable voice" toggle.  Only rendered
  // when the viewer is in a live game (URL has `?gameId=…`) and the
  // GET /api/games/{id}/settings call reports `viewerIsOwner: true`.
  // The toggle posts to /api/games/{id}/settings/voice and fires a
  // `mahjong:voice-enabled` event so voice.ts can flip its mic
  // button live without a page reload.
  const voiceSection = buildVoiceEnableToggle();
  if (voiceSection !== null) {
    panel.appendChild(voiceSection);
  }

  return panel;
}

// Phase K Wave 3 — Per-game voice settings (owner-only).
//
// Bishop's Wave-3 backend exposes:
//   GET  /api/games/{id}/settings
//     → 200 { voiceEnabled: bool, viewerIsOwner: bool, … } | 404
//   POST /api/games/{id}/settings/voice
//     body: { enabled: bool }  → 204 | 403 (not owner) | 404
//
// The toggle is hidden when:
//   • There is no `gameId` in the URL (lobby-only context).
//   • GET returns 404 (endpoint not deployed).
//   • `viewerIsOwner` is false (the user is a guest at someone else's
//     table — the server enforces this anyway via 403).

interface GameSettingsResponse {
  voiceEnabled?: boolean;
  VoiceEnabled?: boolean;
  viewerIsOwner?: boolean;
  ViewerIsOwner?: boolean;
}

function currentGameIdFromUrl(): string | null {
  try {
    const id = new URLSearchParams(window.location.search).get('gameId');
    return id !== null && id !== '' ? id : null;
  } catch {
    return null;
  }
}

function buildVoiceEnableToggle(): HTMLElement | null {
  const gameId = currentGameIdFromUrl();
  if (gameId === null) return null;

  const wrap = document.createElement('div');
  wrap.className = 'settings-v2-field settings-v2-voice-enable';
  // Start hidden — the async GET decides whether to surface it.
  wrap.hidden = true;

  const heading = document.createElement('span');
  heading.className = 'settings-v2-section-heading';
  heading.textContent = t('settings.voice.section') || 'Voice chat (this table)';
  wrap.appendChild(heading);

  const row = document.createElement('label');
  row.className = 'settings-v2-toggle-row';

  const input = document.createElement('input');
  input.type = 'checkbox';
  input.id = 'settings-voice-enable';
  input.setAttribute('data-testid', 'voice-enable-toggle');
  input.disabled = true;
  row.appendChild(input);

  const labelText = document.createElement('span');
  labelText.className = 'settings-v2-toggle-label';
  labelText.textContent = t('settings.voice.toggle') || 'Enable voice chat for this table';
  row.appendChild(labelText);

  const hint = document.createElement('span');
  hint.className = 'settings-v2-hint';
  hint.setAttribute('data-testid', 'voice-enable-hint');
  hint.textContent =
    'Hosts only — when on, players at this table can talk via WebRTC voice.';

  wrap.appendChild(row);
  wrap.appendChild(hint);

  input.addEventListener('change', () => {
    void postVoiceEnable(gameId, input);
  });

  void primeVoiceToggle(gameId, wrap, input);

  return wrap;
}

async function primeVoiceToggle(
  gameId: string,
  wrap: HTMLElement,
  input: HTMLInputElement,
): Promise<void> {
  try {
    const r = await fetch(
      `/api/games/${encodeURIComponent(gameId)}/settings`,
      { credentials: 'same-origin', headers: { Accept: 'application/json' } },
    );
    if (!r.ok) {
      // 404 → endpoint not deployed; leave the toggle hidden.
      return;
    }
    const body = (await r.json()) as GameSettingsResponse;
    const isOwner = body.viewerIsOwner === true || body.ViewerIsOwner === true;
    if (!isOwner) return;
    const enabled = body.voiceEnabled === true || body.VoiceEnabled === true;
    input.checked = enabled;
    input.disabled = false;
    wrap.hidden = false;
  } catch {
    // Network error — leave the toggle hidden.
  }
}

async function postVoiceEnable(
  gameId: string,
  input: HTMLInputElement,
): Promise<void> {
  // Optimistic update — keep the current checked state, roll back on failure.
  const desired = input.checked;
  input.disabled = true;
  try {
    const r = await fetch(
      `/api/games/${encodeURIComponent(gameId)}/settings/voice`,
      {
        method: 'POST',
        credentials: 'same-origin',
        headers: {
          Accept: 'application/json',
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ enabled: desired }),
      },
    );
    if (!r.ok) {
      input.checked = !desired;
      const { showToast } = await import('./toast');
      showToast(
        desired
          ? 'Could not enable voice — server rejected the request.'
          : 'Could not disable voice — server rejected the request.',
        'error',
      );
      return;
    }
    const { showToast } = await import('./toast');
    showToast(
      desired ? 'Voice enabled for this table.' : 'Voice disabled for this table.',
      'success',
    );
    // Notify voice.ts so the mic toggle enables in place — voice.ts
    // listens for this event when it mounted in the disabled state.
    if (desired) {
      window.dispatchEvent(new CustomEvent('mahjong:voice-enabled'));
    } else {
      window.dispatchEvent(new CustomEvent('mahjong:voice-disabled'));
    }
  } catch {
    input.checked = !desired;
    const { showToast } = await import('./toast');
    showToast('Voice settings request failed.', 'error');
  } finally {
    input.disabled = false;
  }
}

// Phase J Wave 8 — Rule presets panel.  The body of this tab is
// rendered by rule-presets.ts:renderEditorPanel() so the editor's
// state (current draft, picker selection) survives a settings
// drawer close+reopen without leaking back into this module.
function buildRulePresetsPanel(): HTMLDivElement {
  const panel = buildPanelShell('rule-presets', t('settings.tab.rule_presets') || 'Rule presets');
  // The host element is the panel itself; rule-presets.ts looks up
  // `#settings-panel-rule-presets` and populates its body in-place.
  renderRulePresetsEditor();
  return panel;
}
