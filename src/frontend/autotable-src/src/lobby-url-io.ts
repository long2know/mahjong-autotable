// ---------------------------------------------------------------------
// Phase K Wave 23 — Hicks (Frontend) — lobby URL + LocalStorage IO chunk.
//
// Extracted from `lobby.ts` so the apply / quick-match click
// handlers can lazy-load the URL builder + the localStorage
// defaults writer.  Both only run AFTER the user clicks Apply or
// Quick Match — well past first paint — so the eager bundle sheds
// the ~1.5 KB of URL-param serialisation + LS persistence code.
//
// The lobby cold-path call-sites in `lobby.ts` await this module
// inside the click handlers (single dynamic import shared across
// both buttons; rollup emits one chunk).
// ---------------------------------------------------------------------

import type { LobbyState } from './lobby';

const LOCAL_STORAGE_KEY = 'mahjong.lobby.defaults';

const RULE_PRESET_LS_KEY = 'mahjong.rule-preset.selected.v1';
const RULE_PRESET_DEFAULT_ID = 'classic-changsha';

function readSelectedPresetIdInline(): string {
  try {
    return window.localStorage.getItem(RULE_PRESET_LS_KEY) ?? RULE_PRESET_DEFAULT_ID;
  } catch {
    return RULE_PRESET_DEFAULT_ID;
  }
}

/**
 * Persist the user's picker state as the next-session default.
 * No-op (caught + swallowed) under privacy mode + serialisation
 * tampering.  Schema is intentionally minimal — anything missing
 * in the next session falls back to the hardcoded DEFAULTS.
 */
export function writeLocalStorageDefaults(state: LobbyState): void {
  try {
    window.localStorage.setItem(LOCAL_STORAGE_KEY, JSON.stringify({
      variant: state.variant,
      dealMode: state.dealMode,
      botCount: state.botCount,
      botDifficulty: state.botDifficulty,
      seed: state.seed,
      handCount: state.handCount,
      seat: state.seat,
    }));
  } catch {
    // Privacy mode or quota — swallow silently.
  }
}

/**
 * Serialise the picker state to a `?variant=…&botCount=…` URL.
 * Preserves an existing `?gameId=` so a lobby Apply doesn't kick
 * the user out of the current game's URL slot.  Emits the
 * `rulePreset` param only when a non-default preset is selected
 * (LS-read inline; no rule-presets module import).
 */
export function buildUrl(state: LobbyState): string {
  const p = new URLSearchParams();
  const currentGameId = new URLSearchParams(window.location.search).get('gameId');
  if (currentGameId !== null && currentGameId !== '') {
    p.set('gameId', currentGameId);
  }
  p.set('variant', state.variant);
  if (state.variant === 'changsha') {
    p.set('dealMode', state.dealMode);
  }
  p.set('botCount', String(state.botCount));
  if (state.botCount > 0) {
    p.set('botDifficulty', state.botDifficulty);
  }
  p.set('handCount', String(state.handCount));
  if (state.seed !== null) {
    p.set('seed', String(state.seed));
  }
  if (state.seat !== null) {
    p.set('seat', String(state.seat));
  }
  try {
    const presetId = readSelectedPresetIdInline();
    if (presetId !== '' && presetId !== 'classic-changsha') {
      p.set('rulePreset', presetId);
    }
  } catch { /* rule-presets module not initialised — skip */ }
  return window.location.pathname + '?' + p.toString();
}
