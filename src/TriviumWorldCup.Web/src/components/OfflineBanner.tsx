import { useEffect, useState } from 'react';

export function OfflineBanner() {
  const [offline, setOffline] = useState<boolean>(!navigator.onLine);
  const [dismissed, setDismissed] = useState<boolean>(false);

  useEffect(() => {
    function handleOnline() {
      setOffline(false);
      setDismissed(false);
    }
    function handleOffline() {
      setOffline(true);
      setDismissed(false);
    }

    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);

    return () => {
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('offline', handleOffline);
    };
  }, []);

  if (!offline || dismissed) {
    return null;
  }

  return (
    <div
      role="alert"
      className="bg-amber-400 text-amber-900 px-4 py-2 flex items-center justify-between text-sm font-medium"
    >
      <span>
        You&apos;re offline. Read-only data is available, but predictions require a connection.
      </span>
      <button
        onClick={() => setDismissed(true)}
        aria-label="Dismiss offline banner"
        className="ml-4 text-amber-800 hover:text-amber-900 font-bold leading-none"
      >
        &times;
      </button>
    </div>
  );
}
