// Animation utilities for tile movement and easing

export function easeInOutCubic(t: number): number {
  return t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2;
}

export function easeOutQuad(t: number): number {
  return 1 - (1 - t) * (1 - t);
}

export interface TweenTarget {
  [key: string]: number;
}

export interface TweenState {
  target: TweenTarget;
  from: TweenTarget;
  to: TweenTarget;
  duration: number;
  startTime: number;
  onComplete?: () => void;
  easing: (t: number) => number;
}

export class TweenManager {
  private activeTweens: Map<string, TweenState> = new Map();

  tween(
    id: string,
    target: TweenTarget,
    to: TweenTarget,
    duration: number,
    easing: (t: number) => number = easeInOutCubic,
    onComplete?: () => void
  ): void {
    const from: TweenTarget = {};
    for (const key in to) {
      from[key] = target[key] ?? 0;
    }

    this.activeTweens.set(id, {
      target,
      from,
      to,
      duration,
      startTime: Date.now(),
      onComplete,
      easing,
    });
  }

  update(): void {
    const now = Date.now();
    const completed: string[] = [];

    for (const [id, tween] of this.activeTweens) {
      const elapsed = now - tween.startTime;
      const progress = Math.min(elapsed / tween.duration, 1);
      const easedProgress = tween.easing(progress);

      // Update target properties
      for (const key in tween.to) {
        const fromValue = tween.from[key];
        const toValue = tween.to[key];
        if (fromValue !== undefined && toValue !== undefined) {
          tween.target[key] = fromValue + (toValue - fromValue) * easedProgress;
        }
      }

      if (progress >= 1) {
        completed.push(id);
        if (tween.onComplete) {
          tween.onComplete();
        }
      }
    }

    // Remove completed tweens
    for (const id of completed) {
      this.activeTweens.delete(id);
    }
  }

  cancel(id: string): void {
    this.activeTweens.delete(id);
  }

  clear(): void {
    this.activeTweens.clear();
  }
}

export const globalTweenManager = new TweenManager();
