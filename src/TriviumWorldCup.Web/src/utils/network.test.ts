import { describe, it, expect, vi, afterEach } from 'vitest';
import { isOnline, requiresConnectivity } from './network';

describe('isOnline', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('returns true when navigator.onLine is true', () => {
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true);
    expect(isOnline()).toBe(true);
  });

  it('returns false when navigator.onLine is false', () => {
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false);
    expect(isOnline()).toBe(false);
  });
});

describe('requiresConnectivity', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('returns true when online', () => {
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true);
    expect(requiresConnectivity('Submit prediction')).toBe(true);
  });

  it('throws with a clear message when offline', () => {
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false);
    expect(() => requiresConnectivity('Submit prediction')).toThrowError(
      '"Submit prediction" requires an internet connection. Please check your connection and try again.',
    );
  });

  it('includes the action name in the error message', () => {
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false);
    expect(() => requiresConnectivity('Save group predictions')).toThrowError(
      '"Save group predictions"',
    );
  });
});
