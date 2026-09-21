import type { Client } from './client';
import type { World } from './world';
import { getSettings, onSettingsChange, setSettings } from './settings-drawer';
import { handSortMode } from './hand-sort';
import { onLanguageChange, t } from './i18n';
import './hand-view.css';
import { COMPACT_VIEW_QUERY } from './mobile-overlay-policy';

/** A touch-sized view of the same concealed tiles, never a second hand. */
export class HandView {
  private host = document.createElement('section');
  private tiles = document.createElement('div');
  private order = document.createElement('select');
  private compact = window.matchMedia(COMPACT_VIEW_QUERY);
  private pointers = new Set<number>();
  private buttons = new Map<number, HTMLButtonElement>();

  constructor(private client: Client, private world: World) {
    this.host.id = 'own-hand-tray';
    this.host.hidden = true;
    this.host.setAttribute('data-testid', 'own-hand-tray');
    const toolbar = document.createElement('label');
    toolbar.className = 'hand-tray-toolbar';
    const title = document.createElement('span');
    title.textContent = t('hand.order');
    this.order.setAttribute('data-testid', 'hand-sort');
    this.order.id = 'hand-sort';
    this.order.setAttribute('aria-label', t('hand.order'));
    this.order.className = 'dark-select';
    const localize = (): void => {
      title.textContent = t('hand.order');
      this.host.setAttribute('aria-label', t('hand.yours'));
      this.order.replaceChildren();
      for (const mode of ['suit', 'groups'] as const) {
        const option = document.createElement('option');
        option.value = mode;
        option.textContent = t(`hand.sort_${mode}`);
        this.order.appendChild(option);
      }
      this.order.value = getSettings().handSort;
      this.render();
    };
    this.order.addEventListener('change', () => {
      const mode = handSortMode(this.order.value);
      if (mode !== getSettings().handSort) setSettings({ handSort: mode });
    });
    toolbar.append(title, this.order);
    this.tiles.className = 'hand-tray-tiles';
    this.host.append(toolbar, this.tiles);
    document.body.appendChild(this.host);
    new ResizeObserver(() => {
      document.documentElement.style.setProperty('--hand-tray-height', `${this.host.getBoundingClientRect().height}px`);
    }).observe(this.host);
    const update = (): void => {
      const settings = getSettings();
      this.order.value = settings.handSort;
      this.world.setHandPresentation(settings.handSort, this.compact.matches);
      this.render();
    };
    this.world.onHandPresentationChanged(() => this.render());
    this.compact.addEventListener('change', update);
    onSettingsChange(update);
    onLanguageChange(localize);
    this.client.turn.on('update', () => this.updateEnabled());
    this.client.claim.on('update', () => this.updateEnabled());
    // Hold the ID-to-position mapping through pointerup AND its following click.
    document.addEventListener('pointerdown', e => {
      this.pointers.add(e.pointerId);
      this.world.setHandPointerDown(true);
    }, true);
    const release = (e: PointerEvent): void => {
      this.pointers.delete(e.pointerId);
      requestAnimationFrame(() => this.world.setHandPointerDown(this.pointers.size > 0));
    };
    document.addEventListener('pointerup', release, true);
    document.addEventListener('pointercancel', release, true);
    window.addEventListener('blur', () => {
      this.pointers.clear();
      this.world.setHandPointerDown(false);
    });
    localize();
    update();
  }

  private render(): void {
    const { tiles, drawn } = this.world.ownHandPresentation();
    this.host.hidden = !this.compact.matches || tiles.length === 0;
    document.body.classList.toggle('has-hand-tray', !this.host.hidden);
    const current = new Set(tiles.map(tile => tile.id));
    for (const [id, button] of this.buttons) {
      if (!current.has(id)) {
        button.remove();
        this.buttons.delete(id);
      }
    }
    for (const tile of tiles) {
      let button = this.buttons.get(tile.id);
      if (!button) {
        button = document.createElement('button');
        button.type = 'button';
        button.className = 'hand-tile';
        button.dataset.tileId = String(tile.id);
        button.setAttribute('data-testid', 'hand-tile');
        button.addEventListener('click', () => {
          const info = this.client.things.get(tile.id);
          const thing = this.world.things.get(tile.id);
          if (!this.client.connected() || !thing || thing.hidden || thing.slot.thing !== thing
            || !info || info.slotName !== thing.slot.name
            || !new RegExp(`^hand\\.\\d+@${this.client.seat}$`).test(info.slotName)) return;
          this.world.discardOwnHandTile(tile.id);
        });
        this.buttons.set(tile.id, button);
      }
      button.dataset.face = String(tile.face);
      button.dataset.drawn = String(tile.id === drawn);
      const label = t('hand.tile', { rank: tile.face % 9 + 1, suit: t(`hand.suit_${Math.floor(tile.face / 9)}`) });
      button.setAttribute('aria-label', tile.id === drawn ? `${label} — ${t('hand.drawn')}` : label);
      button.title = button.getAttribute('aria-label')!;
      button.style.backgroundPosition = `${(tile.face % 8) * 100 / 7}% ${Math.floor(tile.face / 8) * 100 / 5.4}%`;
      this.tiles.appendChild(button);
    }
    this.updateEnabled();
  }

  private updateEnabled(): void {
    const turn = this.client.turn.get('current');
    const enabled = this.client.connected() && this.client.seat !== null
      && turn?.activeSeat === this.client.seat && turn.awaitingDiscard === true
      && this.client.claim.get(String(this.client.seat)) === null;
    for (const button of this.buttons.values()) {
      button.setAttribute('aria-disabled', String(!enabled));
      // Keep waiting tiles readable and focusable; click has the same
      // server-authoritative rejection feedback as the desktop canvas.
    }
    this.host.dataset.discardReady = String(enabled);
  }
}
