/**
 * Vitest global setup.
 * - Registers @testing-library/jest-dom matchers (toBeInTheDocument, etc.)
 * - Polyfills window.matchMedia (Fluent UI 9 hits it during render)
 */
import '@testing-library/jest-dom/vitest';

if (typeof window !== 'undefined' && typeof window.matchMedia !== 'function') {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: (query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: () => undefined,
      removeListener: () => undefined,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
      dispatchEvent: () => false,
    }),
  });
}
