import { useEffect, useState } from 'react';
import { WifiOff, X } from 'lucide-react';

export function OfflineBanner() {
  const [offline, setOffline] = useState<boolean>(!navigator.onLine);
  const [dismissed, setDismissed] = useState<boolean>(false);

  useEffect(() => {
    function handleOnline()  { setOffline(false); setDismissed(false); }
    function handleOffline() { setOffline(true);  setDismissed(false); }
    window.addEventListener('online',  handleOnline);
    window.addEventListener('offline', handleOffline);
    return () => {
      window.removeEventListener('online',  handleOnline);
      window.removeEventListener('offline', handleOffline);
    };
  }, []);

  if (!offline || dismissed) return null;

  return (
    <div
      role="alert"
      className="relative z-50 flex items-center justify-between gap-3 px-4 py-2 text-[13px] font-medium"
      style={{ background: 'var(--accent-fill)', color: 'var(--fg-ongold)' }}
    >
      <span className="flex items-center gap-2">
        <WifiOff size={15} />
        You&apos;re offline. Predictions need a connection.
      </span>
      <button
        onClick={() => setDismissed(true)}
        aria-label="Dismiss offline banner"
        className="opacity-70 hover:opacity-100 transition-opacity"
      >
        <X size={15} />
      </button>
    </div>
  );
}
