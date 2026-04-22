// Toast notification system for game events

interface Toast {
  id: string;
  message: string;
  timestamp: number;
  element: HTMLElement;
}

export class ToastManager {
  private container: HTMLElement;
  private toasts: Map<string, Toast> = new Map();
  private nextId = 0;
  private readonly maxToasts = 3;
  private readonly autoHideDuration = 3000;

  constructor() {
    // Create toast container
    this.container = document.createElement('div');
    this.container.id = 'toast-container';
    this.container.style.cssText = `
      position: fixed;
      bottom: 100px;
      left: 50%;
      transform: translateX(-50%);
      display: flex;
      flex-direction: column;
      gap: 10px;
      z-index: 1000;
      pointer-events: none;
    `;
    document.body.appendChild(this.container);
  }

  show(message: string): void {
    // Remove oldest toast if at max capacity
    if (this.toasts.size >= this.maxToasts) {
      const firstKey = this.toasts.keys().next().value;
      if (firstKey !== undefined) {
        this.hide(firstKey);
      }
    }

    const id = `toast-${this.nextId++}`;
    const element = document.createElement('div');
    element.id = id;
    element.textContent = message;
    element.style.cssText = `
      background: rgba(0, 0, 0, 0.85);
      color: white;
      padding: 12px 20px;
      border-radius: 6px;
      font-family: monospace;
      font-size: 14px;
      box-shadow: 0 4px 6px rgba(0, 0, 0, 0.3);
      opacity: 0;
      transition: opacity 0.2s ease-in-out;
      white-space: nowrap;
    `;

    this.container.appendChild(element);

    // Fade in
    requestAnimationFrame(() => {
      element.style.opacity = '1';
    });

    const toast: Toast = {
      id,
      message,
      timestamp: Date.now(),
      element,
    };

    this.toasts.set(id, toast);

    // Auto-hide after duration
    setTimeout(() => {
      this.hide(id);
    }, this.autoHideDuration);
  }

  private hide(id: string): void {
    const toast = this.toasts.get(id);
    if (!toast) {
      return;
    }

    // Fade out
    toast.element.style.opacity = '0';

    setTimeout(() => {
      if (toast.element.parentNode === this.container) {
        this.container.removeChild(toast.element);
      }
      this.toasts.delete(id);
    }, 200);
  }

  clear(): void {
    for (const id of this.toasts.keys()) {
      this.hide(id);
    }
  }
}

export const globalToastManager = new ToastManager();
