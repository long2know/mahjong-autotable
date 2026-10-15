/** null is the disabled mode, including absent, invalid and duplicate flags. */
export type MobileLayoutMode = 'camera' | 'hybrid' | 'readable' | null;

export interface MobileLayoutViewport {
  width: number;
  height: number;
  changsha: boolean;
}

export function parseMobileLayout(search: string): MobileLayoutMode {
  const values = new URLSearchParams(search).getAll('mobileLayout');
  if (values.length !== 1) return null;
  return values[0] === 'camera' || values[0] === 'hybrid' || values[0] === 'readable' ? values[0] : null;
}

export function resolveMobileLayout(
  mode: MobileLayoutMode, { width, height, changsha }: MobileLayoutViewport,
): MobileLayoutMode {
  return (mode === 'camera' || mode === 'hybrid' || mode === 'readable') && changsha
    && Number.isFinite(width) && Number.isFinite(height)
    && width > 0 && width <= 600 && height > width ? mode : null;
}

export function isReadableBoardPhase(
  phase: string | undefined, validResult: boolean, gameComplete: boolean,
): boolean {
  const current = phase?.toLowerCase();
  return current === 'awaitingdiscard' || current === 'awaitingclaim'
    || (validResult && (current === 'scoring' || current === 'endhand' || current === 'wallexhausted'))
    || (gameComplete && current === 'gamecomplete');
}
