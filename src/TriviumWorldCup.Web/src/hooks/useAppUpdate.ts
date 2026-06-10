import { useEffect, useState } from 'react';

export type Changelog = { version: string; notes: string; reminder?: string };

type UpdateState = { changelog: Changelog; pendingReload: boolean };

const STORAGE_KEY = 'twc-seen-version';

async function fetchChangelog(): Promise<Changelog | null> {
  try {
    const r = await fetch('/changelog.json', { cache: 'no-store' });
    if (!r.ok) return null;
    return await r.json() as Changelog;
  } catch {
    return null;
  }
}

export function useAppUpdate() {
  const [update, setUpdate] = useState<UpdateState | null>(null);

  useEffect(() => {
    // Path 1: first open after a new deploy
    fetchChangelog().then(cl => {
      if (!cl) return;
      if (cl.version !== localStorage.getItem(STORAGE_KEY)) {
        setUpdate({ changelog: cl, pendingReload: false });
      }
    });

    // Path 2: new SW takes control while the app is open
    if (!('serviceWorker' in navigator)) return;
    const handler = () => {
      fetchChangelog().then(cl => {
        if (cl) setUpdate({ changelog: cl, pendingReload: true });
      });
    };
    navigator.serviceWorker.addEventListener('controllerchange', handler);
    return () => navigator.serviceWorker.removeEventListener('controllerchange', handler);
  }, []);

  function dismiss() {
    if (update) localStorage.setItem(STORAGE_KEY, update.changelog.version);
    setUpdate(null);
  }

  function reload() {
    if (update) localStorage.setItem(STORAGE_KEY, update.changelog.version);
    window.location.reload();
  }

  return { update, dismiss, reload };
}
