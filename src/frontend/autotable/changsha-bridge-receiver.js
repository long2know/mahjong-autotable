/* eslint-disable */
/**
 * Changsha → Autotable bridge receiver.
 * Loaded into the autotable iframe alongside the bundled Parcel app.
 * Phase 2 scope: ONE-WAY only (parent → child).
 * Protocol: see docs/rules/changsha-autotable-bridge.md
 */
(function () {
  'use strict';

  var PROTO = 'changsha-bridge/1';

  function ensureOverlay() {
    var overlay = document.getElementById('changsha-bridge-overlay');
    if (overlay) return overlay;
    overlay = document.createElement('div');
    overlay.id = 'changsha-bridge-overlay';
    overlay.style.cssText = [
      'position:fixed', 'top:8px', 'left:8px', 'z-index:10000',
      'padding:6px 10px',
      'background:rgba(15,23,42,0.78)', 'color:#e2e8f0',
      'font:600 12px/1.3 system-ui, sans-serif',
      'border-radius:6px',
      'box-shadow:0 2px 8px rgba(0,0,0,0.35)',
      'pointer-events:none', 'max-width:320px',
    ].join(';');
    overlay.textContent = '🀄 Changsha bridge: connected';
    document.body.appendChild(overlay);
    return overlay;
  }

  function setOverlay(text) {
    var el = ensureOverlay();
    el.textContent = '🀄 ' + text;
  }

  function tileLabel(tileId) {
    var suit = Math.floor(tileId / 4 / 9);
    var rank = (Math.floor(tileId / 4) % 9) + 1;
    var suitName = ['萬', '筒', '条'][suit] || '?';
    return rank + suitName;
  }

  var sceneState = {
    gameId: '', phase: 'lobby', dice: null, breakPoint: null,
    seatTileCounts: [0, 0, 0, 0], discards: []
  };

  function updateOverlayFromState() {
    var s = sceneState;
    var lines = ['Changsha bridge — phase: ' + s.phase];
    if (s.dice) lines.push('Dice: ' + s.dice.die1 + ' + ' + s.dice.die2 + ' = ' + s.dice.sum);
    if (s.breakPoint) lines.push('Break: wall ' + s.breakPoint.wallIndex + ' / stack ' + s.breakPoint.stackIndex);
    lines.push('Tiles per seat: ' + s.seatTileCounts.join(', '));
    if (s.discards.length) {
      var last = s.discards.slice(-6).map(function (d) { return tileLabel(d.tileId); }).join(' ');
      lines.push('Last discards: ' + last);
    }
    var el = ensureOverlay();
    el.innerHTML = lines.map(function (l) { return l.replace(/</g, '&lt;'); }).join('<br>');
  }

  function dispatchAutotableEvent(name, detail) {
    try { window.dispatchEvent(new CustomEvent('changsha-bridge:' + name, { detail: detail })); }
    catch (e) { /* ignore */ }
  }

  function handleMessage(msg) {
    if (!msg || msg.proto !== PROTO) return;
    switch (msg.type) {
      case 'hello':
        sceneState.gameId = msg.gameId;
        sceneState.discards = [];
        sceneState.seatTileCounts = [0, 0, 0, 0];
        break;
      case 'reset':
        sceneState = {
          gameId: '', phase: 'lobby', dice: null, breakPoint: null,
          seatTileCounts: [0, 0, 0, 0], discards: []
        };
        break;
      case 'phase':
        sceneState.phase = msg.phase;
        if (msg.phase === 'rollingDice') dispatchAutotableEvent('camera', { focus: 'center' });
        if (msg.phase === 'awaitingDiscard') dispatchAutotableEvent('camera', { focus: 'seat-0' });
        break;
      case 'dice':
        sceneState.dice = { die1: msg.die1, die2: msg.die2, sum: msg.sum };
        var diceImg = document.getElementById('dice-img');
        if (diceImg) diceImg.style.opacity = '1';
        break;
      case 'breakPoint':
        sceneState.breakPoint = {
          wallIndex: msg.wallIndex,
          stackIndex: msg.stackIndex,
          tileIndex: msg.tileIndex,
        };
        break;
      case 'tilesDealt':
        if (typeof msg.seatIndex === 'number' && msg.seatIndex >= 0 && msg.seatIndex < 4) {
          sceneState.seatTileCounts[msg.seatIndex] = msg.tileCount;
        }
        dispatchAutotableEvent('tilesDealt', msg);
        break;
      case 'tileDiscarded':
        sceneState.discards.push({ seatIndex: msg.seatIndex, tileId: msg.tileId });
        dispatchAutotableEvent('tileDiscarded', msg);
        break;
      case 'claimMade':
        dispatchAutotableEvent('claimMade', msg);
        break;
    }
    updateOverlayFromState();
  }

  function init() {
    ensureOverlay();
    updateOverlayFromState();
    window.addEventListener('message', function (ev) {
      try { handleMessage(ev.data); }
      catch (e) { console.error('[changsha-bridge] handler error', e); }
    });
    // Phase 3 hook: when ready to publish canvas events back, send messages
    // upstream via window.parent.postMessage({ proto: PROTO, type: ... }, '*').
    try {
      if (window.parent && window.parent !== window) {
        window.parent.postMessage({ proto: PROTO, type: 'ready' }, '*');
      }
    } catch (e) { /* parent not accessible */ }
    setOverlay('Changsha bridge: ready (Phase 2, parent → child only)');
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
