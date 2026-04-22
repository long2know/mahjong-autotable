// Audio hooks (no-op implementation for Phase 4; ready for Phase 5 audio assets)

export type AudioEvent = 'draw' | 'discard' | 'meld' | 'win';

export function play(event: AudioEvent): void {
  console.debug(`[Audio] play(${event})`);
  // Phase 5: load and play audio file based on event type
  // For now, this is a no-op hook for future implementation
}
