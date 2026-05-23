// Phase K Wave 2 — Voice chat UI.
//
// Lazy-loaded when a player joins a table with voice opt-in (Bishop's
// VoiceHub will broadcast a `voiceEnabled` flag once it's live; the
// initial gate is `?voice=1` on the game URL so the WebRTC code only
// loads for tables that are explicitly opted in).
//
// Surface:
//   • Mic toggle button (`data-testid="voice-mic-toggle"`).
//   • Per-peer status pill (`voice-peer-{connectionId}`) carrying one
//     of "Connecting", "Connected", "Failed".
//   • Per-peer volume slider (`voice-volume-{connectionId}`).
//
// Wire contract:
//   • ICE servers from `GET /api/turn` — Bishop's endpoint returns
//     `{ iceServers: [{ urls, username?, credential? }, ...] }`.
//   • SignalR `VoiceHub` (mapped at `/hubs/voice`) for signalling:
//       - server → client: `Offer`, `Answer`, `IceCandidate`, `PeerJoined`, `PeerLeft`
//       - client → server: `SendOffer`, `SendAnswer`, `SendIceCandidate`
//   • Up to 4 peers per table (full mesh — N(N-1)/2 = 6 connections
//     in the worst case).  Each peer gets its own `RTCPeerConnection`
//     keyed by their SignalR `connectionId`.
//
// Failure modes:
//   • `getUserMedia` rejected (no mic / permission denied) — render a
//     disabled mic button with tooltip; peers can still hear the user
//     but they can't speak.
//   • `/api/turn` 404 / network error — fall back to a public STUN
//     server (`stun:stun.l.google.com:19302`) so the mesh still works
//     on benign NATs.  Symmetric NATs will fail without a TURN.
//   • `VoiceHub` connection fails — log + render a single "Voice
//     unavailable" status pill so the user knows the mic toggle is
//     a no-op.

import type { Client } from './client';
import { setElHidden } from './dom-utils';
import { showVoiceToast } from './toast';

const FALLBACK_ICE: RTCIceServer[] = [
  { urls: 'stun:stun.l.google.com:19302' },
];

interface IceConfig {
  iceServers: RTCIceServer[];
}

// Phase K Wave 3 — Per-game voice-enabled flag.
//
// Bishop's Wave-3 backend exposes `Game.VoiceEnabled` (default `false`)
// through `GET /api/games/{id}/settings` and a settings-write endpoint
// at `POST /api/games/{id}/settings/voice`.  The voice module probes
// the GET on install — when the flag is `false` we still mount the mic
// button (so the table-creator's settings drawer has somewhere to
// flip the flag from) but render it disabled with the
// "Voice not enabled for this table" tooltip.
//
// `?voice=1` on the URL remains as the E2E / self-hosted override —
// when present we skip the probe and treat voice as enabled.

interface GameVoiceSettings {
  voiceEnabled?: boolean;
  VoiceEnabled?: boolean;
}

const VOICE_ENABLED_URL_OVERRIDE_RE = /[?&]voice=1\b/;

function gameIdFromUrl(): string | null {
  try {
    const params = new URLSearchParams(window.location.search);
    const id = params.get('gameId');
    return id !== null && id !== '' ? id : null;
  } catch {
    return null;
  }
}

async function probeVoiceEnabled(): Promise<boolean> {
  if (VOICE_ENABLED_URL_OVERRIDE_RE.test(window.location.search)) return true;
  const gameId = gameIdFromUrl();
  if (gameId === null) return false;
  try {
    const r = await fetch(
      `/api/games/${encodeURIComponent(gameId)}/settings`,
      { credentials: 'same-origin', headers: { Accept: 'application/json' } },
    );
    if (!r.ok) return false;
    const body = (await r.json()) as GameVoiceSettings;
    return body.voiceEnabled === true || body.VoiceEnabled === true;
  } catch {
    return false;
  }
}

interface SignallingMessage {
  fromConnectionId: string;
  toConnectionId?: string;
  sdp?: RTCSessionDescriptionInit;
  candidate?: RTCIceCandidateInit;
}

interface PeerState {
  connectionId: string;
  displayName: string;
  pc: RTCPeerConnection;
  audioEl: HTMLAudioElement;
  statusPill: HTMLElement;
  volumeSlider: HTMLInputElement;
  status: 'connecting' | 'connected' | 'failed';
}

let installed = false;

const peers: Map<string, PeerState> = new Map();
let localStream: MediaStream | null = null;
let muted = true;
let panel: HTMLElement | null = null;
let micBtn: HTMLButtonElement | null = null;
let peersList: HTMLElement | null = null;
let signaller: VoiceSignaller | null = null;
let iceServers: RTCIceServer[] = FALLBACK_ICE;
// Phase K Wave 3 — Per-game flag.  When `false` we render a disabled
// mic button so the table-creator's settings drawer has a visible
// affordance to flip it on from, but skip the WebRTC mesh.
let voiceEnabledForGame = false;

export async function installVoicePanel(_client: Client): Promise<void> {
  if (installed) return;
  installed = true;

  buildPanel();
  // Phase K Wave 3 — Probe Bishop's per-game voiceEnabled flag.  When
  // it returns false we still leave the panel mounted in disabled
  // state so the table-creator can find the "Enable voice" toggle in
  // the settings drawer; voice signalling + WebRTC are skipped until
  // a future `mahjong:voice-enabled` event flips the gate.
  voiceEnabledForGame = await probeVoiceEnabled();
  setVoiceEnabled(voiceEnabledForGame);
  if (!voiceEnabledForGame) {
    // Listen for settings updates that flip the flag while the user
    // sits at the table.  Owners flipping the toggle in the settings
    // drawer fire this event so the mic button enables in-place.
    window.addEventListener('mahjong:voice-enabled', () => {
      voiceEnabledForGame = true;
      setVoiceEnabled(true);
      void startSignalling();
    }, { once: true });
    return;
  }
  await startSignalling();
}

async function startSignalling(): Promise<void> {
  // Fire-and-forget — failures fall through to disabled-mic state.
  iceServers = await fetchIceServers();
  signaller = await connectVoiceHub();
  if (signaller === null) {
    renderStatus('Voice unavailable (signalling)');
    return;
  }
  wireSignaller(signaller);
}

// Phase K Wave 3 — Disable / re-enable the mic button to mirror the
// per-game `voiceEnabled` flag.  When disabled the click handler
// surfaces a toast instead of starting `getUserMedia`.
function setVoiceEnabled(enabled: boolean): void {
  if (micBtn === null) return;
  if (enabled) {
    micBtn.disabled = false;
    micBtn.classList.remove('voice-mic-disabled');
    if (micBtn.dataset.deniedReason !== 'permission') {
      micBtn.title = muted
        ? 'Mic is muted — click to unmute'
        : 'Mic is live — click to mute';
    }
  } else {
    micBtn.disabled = true;
    micBtn.classList.add('voice-mic-disabled');
    micBtn.title = 'Voice not enabled for this table';
  }
}

// ── Panel scaffolding ─────────────────────────────────────────────

function buildPanel(): void {
  panel = document.createElement('aside');
  panel.id = 'voice-panel';
  panel.setAttribute('data-testid', 'voice-panel');
  panel.className = 'voice-panel';
  panel.setAttribute('aria-label', 'Voice chat');

  micBtn = document.createElement('button');
  micBtn.type = 'button';
  micBtn.setAttribute('data-testid', 'voice-mic-toggle');
  micBtn.className = 'voice-mic-toggle voice-mic-muted';
  micBtn.setAttribute('aria-pressed', 'false');
  micBtn.title = 'Mic is muted — click to unmute';
  micBtn.textContent = '🎙️ Mute';
  micBtn.addEventListener('click', () => { void toggleMic(); });
  panel.appendChild(micBtn);

  peersList = document.createElement('ul');
  peersList.className = 'voice-peers';
  peersList.setAttribute('role', 'list');
  panel.appendChild(peersList);

  document.body.appendChild(panel);
}

function renderStatus(text: string): void {
  if (peersList === null) return;
  let li = peersList.querySelector<HTMLElement>('.voice-status-row');
  if (li === null) {
    li = document.createElement('li');
    li.className = 'voice-status-row';
    li.setAttribute('role', 'status');
    li.setAttribute('aria-live', 'polite');
    peersList.appendChild(li);
  }
  li.textContent = text;
}

// ── Mic + getUserMedia ───────────────────────────────────────────

async function toggleMic(): Promise<void> {
  if (micBtn === null) return;
  // Phase K Wave 3 — Honor the per-game voiceEnabled flag.  Clicking
  // a disabled button surfaces a toast so the user sees why nothing
  // happened; the actual `disabled` attribute also stops most clicks
  // from reaching here but we guard defensively.
  if (!voiceEnabledForGame) {
    showVoiceToast('voice not enabled');
    return;
  }
  if (localStream === null) {
    try {
      localStream = await navigator.mediaDevices.getUserMedia({ audio: true });
    } catch {
      micBtn.disabled = true;
      micBtn.classList.add('voice-mic-denied');
      micBtn.textContent = '🎙️ Mic blocked';
      micBtn.title = 'Microphone permission denied';
      micBtn.dataset.deniedReason = 'permission';
      return;
    }
    // Attach new tracks to existing peer connections.
    for (const peer of peers.values()) {
      for (const track of localStream.getAudioTracks()) {
        peer.pc.addTrack(track, localStream);
      }
    }
    // Phase K Wave 3 — Notify the VoiceHub that we want to join.
    // Bishop's `JoinVoice` returns the error reason as a string when
    // the server refuses (`"voice not enabled"` or
    // `"spectators cannot join voice"`); surface it as a toast.
    if (signaller !== null) {
      try {
        const result = await signaller.invoke('JoinVoice');
        if (typeof result === 'string' && result !== '' && result.toLowerCase() !== 'ok') {
          showVoiceToast(result);
          micBtn.disabled = true;
          return;
        }
      } catch (err) {
        const reason = err instanceof Error ? err.message : String(err);
        showVoiceToast(reason);
      }
    }
  }
  muted = !muted;
  for (const track of localStream.getAudioTracks()) {
    track.enabled = !muted;
  }
  if (muted) {
    micBtn.textContent = '🎙️ Mute';
    micBtn.classList.add('voice-mic-muted');
    micBtn.classList.remove('voice-mic-live');
    micBtn.setAttribute('aria-pressed', 'false');
    micBtn.title = 'Mic is muted — click to unmute';
  } else {
    micBtn.textContent = '🔴 Live';
    micBtn.classList.remove('voice-mic-muted');
    micBtn.classList.add('voice-mic-live');
    micBtn.setAttribute('aria-pressed', 'true');
    micBtn.title = 'Mic is live — click to mute';
  }
}

// ── ICE config ───────────────────────────────────────────────────

async function fetchIceServers(): Promise<RTCIceServer[]> {
  try {
    const resp = await fetch('/api/turn', {
      credentials: 'same-origin',
      headers: { Accept: 'application/json' },
    });
    if (!resp.ok) return FALLBACK_ICE;
    const body = (await resp.json()) as unknown;
    if (body !== null && typeof body === 'object') {
      const config = body as IceConfig;
      if (Array.isArray(config.iceServers) && config.iceServers.length > 0) {
        return config.iceServers;
      }
    }
  } catch { /* network error — fall through to fallback STUN */ }
  return FALLBACK_ICE;
}

// ── Signaller (SignalR VoiceHub) ─────────────────────────────────

interface VoiceSignaller {
  connectionId: string;
  on(event: string, handler: (msg: SignallingMessage) => void): void;
  invoke(method: string, ...args: unknown[]): Promise<unknown>;
}

async function connectVoiceHub(): Promise<VoiceSignaller | null> {
  // Lazy import @microsoft/signalr — the lobby chunk already pulls it
  // through Bishop's profile/matchmaking surfaces but the import here
  // keeps the dependency explicit.
  try {
    const { HubConnectionBuilder, LogLevel } = await import('@microsoft/signalr');
    const url = voiceHubUrl();
    const conn = new HubConnectionBuilder()
      .withUrl(url)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    await conn.start();
    const connectionId = conn.connectionId ?? '';
    return {
      connectionId,
      on: (event, handler) => conn.on(event, handler as (...args: unknown[]) => void),
      invoke: (method, ...args) => conn.invoke(method, ...args),
    };
  } catch {
    return null;
  }
}

function voiceHubUrl(): string {
  const params = new URLSearchParams(window.location.search);
  const override = params.get('voiceHub');
  if (override !== null && override !== '') return override;
  return '/hubs/voice';
}

function wireSignaller(s: VoiceSignaller): void {
  s.on('PeerJoined', (msg) => {
    void handlePeerJoined(msg.fromConnectionId);
  });
  s.on('PeerLeft', (msg) => {
    handlePeerLeft(msg.fromConnectionId);
  });
  s.on('Offer', (msg) => {
    void handleOffer(msg);
  });
  s.on('Answer', (msg) => {
    void handleAnswer(msg);
  });
  s.on('IceCandidate', (msg) => {
    void handleIceCandidate(msg);
  });
}

async function handlePeerJoined(peerId: string): Promise<void> {
  if (peers.has(peerId)) return;
  const peer = createPeer(peerId);
  // Polite-peer pattern: only the lexicographically smaller id sends
  // the initial offer.  Avoids glare in a full mesh.
  if (signaller !== null && signaller.connectionId < peerId) {
    const offer = await peer.pc.createOffer();
    await peer.pc.setLocalDescription(offer);
    await signaller.invoke('SendOffer', peerId, offer);
  }
}

function handlePeerLeft(peerId: string): void {
  const peer = peers.get(peerId);
  if (peer === undefined) return;
  peer.pc.close();
  peer.audioEl.remove();
  peer.statusPill.parentElement?.remove();
  peers.delete(peerId);
}

async function handleOffer(msg: SignallingMessage): Promise<void> {
  if (msg.sdp === undefined || signaller === null) return;
  let peer = peers.get(msg.fromConnectionId);
  if (peer === undefined) {
    peer = createPeer(msg.fromConnectionId);
  }
  await peer.pc.setRemoteDescription(new RTCSessionDescription(msg.sdp));
  const answer = await peer.pc.createAnswer();
  await peer.pc.setLocalDescription(answer);
  await signaller.invoke('SendAnswer', msg.fromConnectionId, answer);
}

async function handleAnswer(msg: SignallingMessage): Promise<void> {
  if (msg.sdp === undefined) return;
  const peer = peers.get(msg.fromConnectionId);
  if (peer === undefined) return;
  await peer.pc.setRemoteDescription(new RTCSessionDescription(msg.sdp));
}

async function handleIceCandidate(msg: SignallingMessage): Promise<void> {
  if (msg.candidate === undefined) return;
  const peer = peers.get(msg.fromConnectionId);
  if (peer === undefined) return;
  try { await peer.pc.addIceCandidate(new RTCIceCandidate(msg.candidate)); }
  catch { /* race: stale candidate, ignore */ }
}

function createPeer(connectionId: string): PeerState {
  const pc = new RTCPeerConnection({ iceServers });
  const audioEl = document.createElement('audio');
  audioEl.autoplay = true;
  audioEl.setAttribute('data-peer', connectionId);
  document.body.appendChild(audioEl);

  pc.ontrack = (ev) => {
    audioEl.srcObject = ev.streams[0] ?? null;
  };
  pc.onicecandidate = (ev) => {
    if (ev.candidate === null || signaller === null) return;
    void signaller.invoke('SendIceCandidate', connectionId, ev.candidate.toJSON());
  };
  pc.onconnectionstatechange = () => {
    let next: PeerState['status'] = 'connecting';
    if (pc.connectionState === 'connected') next = 'connected';
    else if (pc.connectionState === 'failed' || pc.connectionState === 'closed') next = 'failed';
    const peer = peers.get(connectionId);
    if (peer !== undefined) {
      peer.status = next;
      peer.statusPill.textContent = statusLabel(next);
      peer.statusPill.className = `voice-peer-status voice-peer-status-${next}`;
    }
  };

  if (localStream !== null) {
    for (const track of localStream.getAudioTracks()) {
      pc.addTrack(track, localStream);
    }
  }

  const row = document.createElement('li');
  row.className = 'voice-peer-row';
  row.setAttribute('data-connection-id', connectionId);

  const label = document.createElement('span');
  label.className = 'voice-peer-name';
  label.textContent = connectionId.slice(0, 8);
  row.appendChild(label);

  const statusPill = document.createElement('span');
  statusPill.setAttribute('data-testid', `voice-peer-${connectionId}`);
  statusPill.className = 'voice-peer-status voice-peer-status-connecting';
  statusPill.textContent = 'Connecting';
  row.appendChild(statusPill);

  const volumeSlider = document.createElement('input');
  volumeSlider.type = 'range';
  volumeSlider.min = '0';
  volumeSlider.max = '1';
  volumeSlider.step = '0.05';
  volumeSlider.value = '1';
  volumeSlider.setAttribute('data-testid', `voice-volume-${connectionId}`);
  volumeSlider.setAttribute('aria-label', `Volume for peer ${connectionId.slice(0, 8)}`);
  volumeSlider.addEventListener('input', () => {
    const v = parseFloat(volumeSlider.value);
    audioEl.volume = isNaN(v) ? 1 : Math.min(1, Math.max(0, v));
  });
  row.appendChild(volumeSlider);

  peersList?.appendChild(row);

  const peer: PeerState = {
    connectionId,
    displayName: connectionId,
    pc,
    audioEl,
    statusPill,
    volumeSlider,
    status: 'connecting',
  };
  peers.set(connectionId, peer);
  return peer;
}

function statusLabel(s: PeerState['status']): string {
  switch (s) {
    case 'connected': return 'Connected';
    case 'failed':    return 'Failed';
    default:          return 'Connecting';
  }
}

// Exported for unit / Playwright drivers — surfaces the current
// muted state without forcing test code to grovel the DOM.
export function isMuted(): boolean {
  return muted;
}

// Tear-down for tests + page transitions.  Idempotent.
export function shutdownVoice(): void {
  for (const peer of peers.values()) {
    try { peer.pc.close(); } catch { /* ignore */ }
    peer.audioEl.remove();
  }
  peers.clear();
  if (localStream !== null) {
    for (const track of localStream.getTracks()) {
      try { track.stop(); } catch { /* ignore */ }
    }
    localStream = null;
  }
  if (panel !== null) {
    setElHidden(panel, true);
  }
}
