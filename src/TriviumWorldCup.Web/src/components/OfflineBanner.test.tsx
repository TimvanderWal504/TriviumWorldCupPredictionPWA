import { describe, it, expect, afterEach } from 'vitest';
import { render, screen, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { OfflineBanner } from './OfflineBanner';

function setOnlineState(online: boolean) {
  Object.defineProperty(navigator, 'onLine', {
    writable: true,
    configurable: true,
    value: online,
  });
}

function fireOnlineEvent() {
  window.dispatchEvent(new Event('online'));
}

function fireOfflineEvent() {
  window.dispatchEvent(new Event('offline'));
}

describe('OfflineBanner', () => {
  afterEach(() => {
    setOnlineState(true);
  });

  it('renders nothing when online', () => {
    setOnlineState(true);
    render(<OfflineBanner />);
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('shows the offline banner when navigator.onLine is false', () => {
    setOnlineState(false);
    render(<OfflineBanner />);
    const alert = screen.getByRole('alert');
    expect(alert).toBeTruthy();
    expect(alert.textContent).toContain('offline');
    expect(alert.textContent).toContain('predictions require a connection');
  });

  it('shows the banner when the offline event fires', async () => {
    setOnlineState(true);
    render(<OfflineBanner />);
    expect(screen.queryByRole('alert')).toBeNull();

    act(() => {
      setOnlineState(false);
      fireOfflineEvent();
    });

    expect(screen.getByRole('alert')).toBeTruthy();
  });

  it('hides the banner when connectivity is restored', async () => {
    setOnlineState(false);
    render(<OfflineBanner />);
    expect(screen.getByRole('alert')).toBeTruthy();

    act(() => {
      setOnlineState(true);
      fireOnlineEvent();
    });

    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('can be dismissed with the close button', async () => {
    setOnlineState(false);
    render(<OfflineBanner />);
    expect(screen.getByRole('alert')).toBeTruthy();

    const user = userEvent.setup();
    await user.click(screen.getByRole('button', { name: /dismiss/i }));

    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('re-shows after dismiss if connectivity drops again', async () => {
    setOnlineState(false);
    render(<OfflineBanner />);

    // Dismiss the banner
    const user = userEvent.setup();
    await user.click(screen.getByRole('button', { name: /dismiss/i }));
    expect(screen.queryByRole('alert')).toBeNull();

    // Come back online then go offline again — banner should re-appear
    act(() => {
      setOnlineState(true);
      fireOnlineEvent();
    });
    act(() => {
      setOnlineState(false);
      fireOfflineEvent();
    });

    expect(screen.getByRole('alert')).toBeTruthy();
  });
});
