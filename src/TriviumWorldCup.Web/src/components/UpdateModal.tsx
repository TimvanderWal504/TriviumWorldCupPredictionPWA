import { RefreshCw, Sparkles } from 'lucide-react';
import type { Changelog } from '../hooks/useAppUpdate';

interface Props {
  changelog: Changelog;
  pendingReload: boolean;
  onDismiss: () => void;
  onReload: () => void;
}

export function UpdateModal({ changelog, pendingReload, onDismiss, onReload }: Props) {
  return (
    <div
      className="fixed inset-0 z-50 flex items-end justify-center p-4 sm:items-center"
      style={{ background: 'var(--overlay)' }}
    >
      <div className="bg-surface rounded-sheet shadow-sheet w-full max-w-sm p-6 border border-border">
        <div className="flex items-center gap-2 mb-3">
          {pendingReload
            ? <RefreshCw size={18} className="text-primary" />
            : <Sparkles size={18} className="text-primary" />
          }
          <h2 className="font-display font-bold text-lg tracking-tight">
            {pendingReload ? 'Update ready' : "What's new"}
          </h2>
        </div>

        <p className="text-sm text-fg-secondary mb-5 leading-relaxed whitespace-pre-wrap">
          {changelog.notes || 'The app has been updated to the latest version.'}
        </p>
        {changelog.reminder && (
          <p className="text-sm font-semibold mb-3 leading-relaxed whitespace-pre-wrap">
            {changelog.reminder}
          </p>
        )}
        <div className="flex gap-2 justify-end">
          {pendingReload ? (
            <>
              <button
                onClick={onDismiss}
                className="px-4 py-2 rounded-input text-[13px] font-semibold bg-surface-3 text-fg-muted hover:text-fg transition-colors"
              >
                Later
              </button>
              <button
                onClick={onReload}
                className="px-4 py-2 rounded-input text-[13px] font-semibold transition-colors"
                style={{ background: 'var(--primary-fill)', color: 'var(--fg-onbrand)' }}
              >
                Reload
              </button>
            </>
          ) : (
            <button
              onClick={onDismiss}
              className="px-4 py-2 rounded-input text-[13px] font-semibold transition-colors"
              style={{ background: 'var(--primary-fill)', color: 'var(--fg-onbrand)' }}
            >
              Got it
            </button>
          )}
        </div>
      </div>
    </div>
  );
}
