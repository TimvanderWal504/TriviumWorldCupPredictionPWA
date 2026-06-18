interface SpinnerProps {
  size?: 'sm' | 'md' | 'lg';
  label?: string;
  className?: string;
}

export function Spinner({ size = 'md', label, className = '' }: SpinnerProps) {
  if (size === 'sm') {
    return (
      <svg
        className={`animate-spin shrink-0 ${className}`}
        style={{ animationTimingFunction: 'linear', transformOrigin: 'center' }}
        width="16" height="16" viewBox="0 0 16 16" fill="none"
        aria-hidden="true"
      >
        <circle cx="8" cy="8" r="5.5" stroke="var(--surface-3)" strokeWidth="2" />
        <circle cx="8" cy="8" r="5.5" stroke="var(--primary)" strokeWidth="2"
          strokeDasharray="26 9" strokeLinecap="round" />
      </svg>
    );
  }

  if (size === 'md') {
    return (
      <svg
        className={`animate-spin ${className}`}
        style={{ animationTimingFunction: 'linear', transformOrigin: 'center' }}
        width="36" height="36" viewBox="0 0 36 36" fill="none"
        aria-hidden="true"
      >
        <circle cx="18" cy="18" r="14" stroke="var(--surface-3)" strokeWidth="2.5" />
        <circle cx="18" cy="18" r="14" stroke="var(--primary)" strokeWidth="2.5"
          strokeDasharray="66 22" strokeLinecap="round" />
      </svg>
    );
  }

  // lg — dual-arc page-level spinner
  return (
    <div className={`flex flex-col items-center gap-4 ${className}`}>
      <div className="relative" style={{ width: 64, height: 64 }}>
        {/* Track */}
        <svg className="absolute inset-0" width="64" height="64" viewBox="0 0 64 64" fill="none" aria-hidden="true">
          <circle cx="32" cy="32" r="26" stroke="var(--surface-3)" strokeWidth="4" />
        </svg>
        {/* Primary arc — pitch green, clockwise 1.2s */}
        <svg
          className="absolute inset-0 animate-spin"
          style={{ animationDuration: '1.2s', animationTimingFunction: 'linear', transformOrigin: 'center' }}
          width="64" height="64" viewBox="0 0 64 64" fill="none" aria-hidden="true"
        >
          <circle cx="32" cy="32" r="26" stroke="var(--primary)" strokeWidth="4"
            strokeDasharray="122 41" strokeLinecap="round" />
        </svg>
        {/* Secondary arc — warning/orange, counter-clockwise 1.8s */}
        <svg
          className="absolute inset-0 animate-spin"
          style={{ animationDuration: '1.8s', animationTimingFunction: 'linear', animationDirection: 'reverse', transformOrigin: 'center' }}
          width="64" height="64" viewBox="0 0 64 64" fill="none" aria-hidden="true"
        >
          <circle cx="32" cy="32" r="17" stroke="var(--warning)" strokeWidth="3"
            strokeDasharray="48 59" strokeLinecap="round" />
        </svg>
      </div>
      {label && (
        <p className="text-xs tracking-widest text-fg-muted uppercase font-display font-bold">
          {label}
        </p>
      )}
    </div>
  );
}
