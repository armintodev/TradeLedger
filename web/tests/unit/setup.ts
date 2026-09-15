import '@testing-library/jest-dom/vitest';
import { afterAll, afterEach, beforeAll, vi } from 'vitest';
import { server } from './msw/server';

// jsdom has no layout, so it ships no scrollIntoView. Mantine's Combobox calls it when
// it highlights an option, and the throw takes the whole dropdown down with it.
Element.prototype.scrollIntoView = vi.fn();

// jsdom implements neither, and Mantine reaches for both.
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: vi.fn(),
    removeListener: vi.fn(),
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  }),
});

class ResizeObserverStub {
  observe() {}
  unobserve() {}
  disconnect() {}
}

window.ResizeObserver = ResizeObserverStub as unknown as typeof ResizeObserver;

window.scrollTo = vi.fn() as unknown as typeof window.scrollTo;

// jsdom has no FontFaceSet, and Mantine's autosizing textarea listens on it.
if (!document.fonts) {
  Object.defineProperty(document, 'fonts', {
    writable: true,
    value: { addEventListener: vi.fn(), removeEventListener: vi.fn(), ready: Promise.resolve() },
  });
}

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => {
  server.resetHandlers();
  window.localStorage.clear();
});
afterAll(() => server.close());
