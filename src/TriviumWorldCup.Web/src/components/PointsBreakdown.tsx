interface PointsBreakdownProps {
  groupMatchPoints: number;
  knockoutPoints: number;
  championPoints: number;
  goldenSixPoints: number;
  totalPoints: number;
  /** Renders the trailing Total row. Off where the caller already shows a total. */
  showTotal?: boolean;
}

/**
 * Per-category points breakdown — shared by the standings page and the
 * leaderboard member drill-down so both read the same way.
 */
export function PointsBreakdown({
  groupMatchPoints,
  knockoutPoints,
  championPoints,
  goldenSixPoints,
  totalPoints,
  showTotal = true,
}: PointsBreakdownProps) {
  const rows: [string, number][] = [
    ['Group matches', groupMatchPoints],
    ['Knockout phase', knockoutPoints],
    ['Champion prediction', championPoints],
    ['Golden Six', goldenSixPoints],
  ];

  return (
    <div className="rounded-card bg-surface border border-border overflow-hidden">
      <div className="px-5 py-3 border-b border-border bg-surface-2">
        <h2 className="text-[10px] font-display font-bold uppercase tracking-wider text-fg-muted">Points Breakdown</h2>
      </div>
      <div className="divide-y divide-border">
        {rows.map(([label, pts]) => (
          <div key={label} className="px-5 py-3 flex items-center justify-between">
            <span className="text-sm text-fg-secondary">{label}</span>
            <span className="text-sm text-fg tnum">{pts} pts</span>
          </div>
        ))}
        {showTotal && (
          <div className="px-5 py-3 flex items-center justify-between">
            <span className="font-semibold text-fg">Total</span>
            <span className="font-display font-bold tnum" style={{ color: 'var(--primary)' }}>
              {totalPoints} pts
            </span>
          </div>
        )}
      </div>
    </div>
  );
}
