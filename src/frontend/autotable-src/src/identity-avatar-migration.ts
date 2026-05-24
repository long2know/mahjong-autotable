// Phase K Wave 22 — Hicks (Frontend), bundle-audit §3.7.
//
// Lazy-loaded avatar-migration modal.  Extracted from `identity.ts`
// so the eager bundle no longer carries the modal grid + confirm
// path; only users whose cached avatar matches the legacy `#808080`
// sentinel (sniffed synchronously by index.ts before any lobby code
// runs) pay for this chunk.
//
// Pre-Wave-22 the install path was already dynamic-imported from
// index.ts (`scheduleAvatarMigrationLazyMount` → `import('./identity')
// .then(m => m.installAvatarMigrationModalIfNeeded())`), but because
// identity.ts is also statically imported by lobby.ts the modal code
// still landed in `autotable-src-eager`.  Splitting it into its own
// file lets Rollup hoist it into its own lazy chunk.

import {
  AVATAR_COLOR_PRESETS,
  getProfile,
  onProfile,
  setAvatarColor as setProfileAvatarColor,
  validateAvatarColor,
} from './profile';
import { showEl, hideEl } from './dom-utils';

// The legacy hex `#808080` is the sentinel for "not yet customised".
// Anyone whose persisted avatarColor matches (case-insensitive) is
// shown the modal once their profile loads.  Picking + confirming
// persists via the existing setAvatarColor() Hub RPC.
const LEGACY_AVATAR_COLOR = '#808080';

// Vasquez's Wave-10 testid contract uses friendly colour names
// (e.g. `avatar-migration-pick-emerald`) rather than hex strings.
// Keep this array index-aligned with profile.ts AVATAR_COLOR_PRESETS.
const AVATAR_MIGRATION_NAMES: ReadonlyArray<string> = [
  'red',
  'orange',
  'yellow',
  'emerald',
  'teal',
  'blue',
  'purple',
  'slate',
];

let migrationModalInstalled = false;
let migrationSelected: string | null = null;

function isLegacyAvatarColor(color: string | null | undefined): boolean {
  if (color === null || color === undefined) return false;
  return color.toLowerCase() === LEGACY_AVATAR_COLOR;
}

function renderMigrationGrid(): void {
  const grid = document.getElementById('migrate-avatar-grid');
  const confirmBtn = document.getElementById('migrate-avatar-confirm') as HTMLButtonElement | null;
  if (grid === null) return;
  grid.replaceChildren();
  migrationSelected = null;
  if (confirmBtn !== null) confirmBtn.disabled = true;
  AVATAR_COLOR_PRESETS.forEach((hex, i) => {
    const btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'migrate-avatar-swatch';
    btn.style.backgroundColor = hex;
    btn.setAttribute('data-color', hex);
    const name = AVATAR_MIGRATION_NAMES[i] ?? `preset-${i}`;
    btn.setAttribute('data-testid', `avatar-migration-pick-${name}`);
    btn.setAttribute('role', 'radio');
    btn.setAttribute('aria-checked', 'false');
    btn.setAttribute('aria-label', `Avatar colour ${name}`);
    btn.title = `${name} (${hex})`;
    btn.addEventListener('click', () => {
      migrationSelected = hex;
      for (const sw of grid.querySelectorAll<HTMLButtonElement>('.migrate-avatar-swatch')) {
        const matches = sw.getAttribute('data-color')?.toLowerCase() === hex.toLowerCase();
        sw.classList.toggle('migrate-avatar-swatch-selected', matches);
        sw.setAttribute('aria-checked', matches ? 'true' : 'false');
      }
      if (confirmBtn !== null) confirmBtn.disabled = false;
    });
    grid.appendChild(btn);
  });
}

function showMigrationModal(): void {
  const modal = document.getElementById('migrate-avatar-modal');
  if (modal === null) return;
  renderMigrationGrid();
  showEl(modal);
  modal.setAttribute('aria-hidden', 'false');
}

function hideMigrationModal(): void {
  const modal = document.getElementById('migrate-avatar-modal');
  if (modal === null) return;
  hideEl(modal);
  modal.setAttribute('aria-hidden', 'true');
}

export function installAvatarMigrationModalIfNeeded(): void {
  if (migrationModalInstalled) return;
  const modal = document.getElementById('migrate-avatar-modal');
  const confirmBtn = document.getElementById('migrate-avatar-confirm') as HTMLButtonElement | null;
  const dismissBtn = document.getElementById('migrate-avatar-dismiss') as HTMLButtonElement | null;
  if (modal === null || confirmBtn === null) return;
  migrationModalInstalled = true;

  confirmBtn.addEventListener('click', () => {
    if (migrationSelected === null) return;
    if (!validateAvatarColor(migrationSelected)) return;
    confirmBtn.disabled = true;
    const result = setProfileAvatarColor(migrationSelected);
    if (result.error !== null) {
      // Re-enable so the user can retry.
      confirmBtn.disabled = false;
      return;
    }
    hideMigrationModal();
  });

  // Wave 10 — dismiss button per Vasquez's `avatar-migration-dismiss`
  // contract.  Lets the user defer the choice; the modal will re-show
  // on the next profile load while avatarColor remains the legacy
  // sentinel (so the prompt is recurring, not blocking).
  if (dismissBtn !== null) {
    dismissBtn.addEventListener('click', () => {
      hideMigrationModal();
    });
  }

  const evaluate = (): void => {
    const profile = getProfile();
    if (profile === null) return;
    if (isLegacyAvatarColor(profile.avatarColor)) {
      showMigrationModal();
    } else {
      hideMigrationModal();
    }
  };

  // Listen for profile updates from the Hub.  The initial profile may
  // arrive before or after this install runs; evaluate() handles both.
  onProfile(evaluate);
  evaluate();
}
