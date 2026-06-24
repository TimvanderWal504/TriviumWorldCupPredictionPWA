function SkeletonBlock({ className = '' }: { className?: string }) {
  return <div className={`animate-pulse rounded bg-surface-2 ${className}`} />;
}

function SkeletonPodium() {
  const slots = [
    { nameW: 'w-16', ptsW: 'w-10', avatarSize: 'w-12 h-12', barH: 'h-18' },
    { nameW: 'w-20', ptsW: 'w-14', avatarSize: 'w-14 h-14', barH: 'h-24' },
    { nameW: 'w-14', ptsW: 'w-9',  avatarSize: 'w-11 h-11', barH: 'h-14' },
  ];
  return (
    <div className="flex items-end justify-center gap-0.5 pt-6 px-1">
      {slots.map((s, i) => (
        <div key={i} className="flex flex-col items-center flex-1 gap-2">
          <SkeletonBlock className={`h-3.5 rounded-full ${s.nameW}`} />
          <SkeletonBlock className={`h-3 rounded-full ${s.ptsW}`} />
          <SkeletonBlock className={`rounded-full ${s.avatarSize} mb-2`} />
          <SkeletonBlock className={`${s.barH} w-full rounded-t-md`} />
        </div>
      ))}
    </div>
  );
}

function SkeletonLeaderboardRow() {
  return (
    <div className="grid grid-cols-[2.25rem_1fr_3.25rem] gap-2.5 px-4 py-3.5 border-t border-border first:border-t-0">
      <div className="flex justify-center items-center">
        <SkeletonBlock className="w-7 h-7 rounded-full" />
      </div>
      <div className="flex items-center gap-2">
        <SkeletonBlock className="w-5 h-3.5 rounded-sm shrink-0" />
        <SkeletonBlock className="h-3 rounded-full flex-1 max-w-[120px]" />
      </div>
      <div className="flex justify-end items-center">
        <SkeletonBlock className="w-8 h-5 rounded" />
      </div>
    </div>
  );
}

export function SkeletonRankCard() {
  return (
    <div className="rounded-card bg-surface border border-border p-5 flex items-center justify-between">
      <div className="space-y-2">
        <SkeletonBlock className="h-2.5 w-20 rounded-full" />
        <SkeletonBlock className="h-9 w-16 rounded" />
      </div>
      <div className="flex flex-col items-end space-y-2">
        <SkeletonBlock className="h-2.5 w-20 rounded-full" />
        <SkeletonBlock className="h-9 w-12 rounded" />
      </div>
    </div>
  );
}

export function SkeletonLeaderboard() {
  return (
    <div className="rounded-card bg-surface border border-border overflow-hidden">
      <SkeletonPodium />
      <div className="grid grid-cols-[2.25rem_1fr_3.25rem] gap-2.5 px-4 py-2.5 bg-surface-2 text-[10px] font-display font-bold uppercase tracking-wider text-fg-muted">
        <span className="text-center">#</span>
        <span>Member</span>
        <span className="text-right">Pts</span>
      </div>
      <div>
        {Array.from({ length: 7 }).map((_, i) => <SkeletonLeaderboardRow key={i} />)}
      </div>
    </div>
  );
}
