export function RulesPage() {
  const sectionHead = 'text-xl font-display font-bold tracking-tight border-b border-border pb-2 mb-4';
  const tableHead = 'bg-surface-2 text-fg-muted text-left text-[10px] font-display font-bold uppercase tracking-wider';
  const tdBase = 'px-4 py-2.5 text-fg-secondary';
  const tdRight = `${tdBase} text-right font-display font-bold tnum text-fg`;

  return (
    <div className="max-w-3xl mx-auto px-4 py-4 space-y-10">

      <section className="bg-surface rounded-card border border-border p-5 space-y-4">
        <p className="text-fg-secondary text-sm leading-relaxed">Dear colleagues,</p>
        <p className="text-fg-secondary text-sm leading-relaxed">
          The Football World Cup is almost here, with the opening match kicking off on Thursday.
          Around the world, football fans are already making predictions and sharing their views on
          who will make it through the group stages and who will go all the way.
        </p>
        <p className="text-fg-secondary text-sm leading-relaxed">
          To celebrate the occasion, we're launching the <span className="font-semibold text-fg">Trivium World Cup Pool</span>.
        </p>
        <p className="text-fg-secondary text-sm leading-relaxed">
          And, as if eternal glory wasn't enough, there is also a prize for the winner! At the end
          of the tournament, the winner of the pool will receive an{' '}
          <span className="font-semibold text-fg">official jersey of the World Cup-winning nation</span>.
        </p>
        <div className="rounded-input bg-surface-2 border border-border px-4 py-3 text-sm text-fg-secondary">
          <span className="font-semibold text-fg">Tip:</span> you can also install this as an app directly from your browser (look for the install icon in the address bar).
        </div>
        <p className="text-fg-secondary text-sm leading-relaxed">
          If you run into any issues or have suggestions for improvements, feel free to contact{' '}
          <a
            href="https://teams.microsoft.com/l/chat/48:notes/conversations?context=%7B%22contextType%22%3A%22chat%22%7D"
            target="_blank"
            rel="noopener noreferrer"
            className="font-medium text-fg underline underline-offset-2 hover:opacity-80 transition-opacity"
          >
            Tim
          </a>{' '}
          directly via Teams.
        </p>
      </section>

      <section className="bg-surface rounded-card border border-border p-5 space-y-3">
        <h2 className={sectionHead}>Tournament Format</h2>
        <p className="text-fg-secondary leading-relaxed text-sm">
          FIFA World Cup 2026: 48 teams, 12 groups (A–L) of four teams each, 104 total matches.
          The top two from each group plus the eight best third-placed teams advance to a{' '}
          <span className="font-medium text-fg">Round of 32</span>, then{' '}
          <span className="font-medium text-fg">Round of 16</span>, quarter-finals, semi-finals,
          third-place play-off, and the Final.
        </p>
        <ul className="text-fg-secondary text-sm space-y-1 list-disc list-inside">
          <li>Group stage: 11–27 June 2026</li>
          <li>Knockout rounds begin: 28 June 2026</li>
          <li>Final: 19 July 2026</li>
        </ul>
      </section>

      <section className="bg-surface rounded-card border border-border p-5 space-y-4">
        <h2 className={sectionHead}>Group Match Scoring</h2>
        <p className="text-fg-muted text-sm">
          Award the single best tier; tiers are <strong className="text-fg-secondary">not</strong> cumulative.
        </p>

        <table className="w-full text-sm border-collapse rounded-card overflow-hidden border border-border">
          <thead><tr className={tableHead}>
            <th className="px-4 py-2.5">Prediction result</th>
            <th className="px-4 py-2.5 text-right">Points</th>
          </tr></thead>
          <tbody className="divide-y divide-border">
            {[['Exact score', '10'], ['Correct goal difference (not exact)', '7'], ['Correct outcome only (W/D/L)', '3'], ['Wrong', '0']].map(([label, pts]) => (
              <tr key={label} className="even:bg-surface-2">
                <td className={tdBase}>{label}</td>
                <td className={tdRight}>{pts}</td>
              </tr>
            ))}
          </tbody>
        </table>

        <div className="rounded-card bg-surface-2 p-4 space-y-2 border border-border">
          <h3 className="text-sm font-semibold text-fg">Team-tally bonus</h3>
          <p className="text-fg-secondary text-sm leading-relaxed">
            <span className="font-medium text-fg">+1 point</span> if exactly one team's goal count
            was predicted correctly. This bonus can only add to the correct-outcome tier (3 → 4) or
            the wrong tier (0 → 1).
          </p>
          <ul className="text-fg-secondary text-sm space-y-1 list-disc list-inside">
            <li>Two correct tallies = exact score, already captured in the 10-point tier.</li>
            <li>A correct goal difference that is not exact makes any individual tally mathematically impossible.</li>
          </ul>
        </div>

        <div className="rounded-card p-5 space-y-2 border" style={{ background: 'var(--win-soft)', borderColor: 'transparent' }}>
          <h3 className="text-sm font-display font-bold uppercase tracking-wider" style={{ color: 'var(--win)' }}>
            Worked Example
          </h3>
          <p className="text-fg-secondary text-sm leading-relaxed">
            Predicted <span className="font-semibold text-fg">2–1</span>, actual result{' '}
            <span className="font-semibold text-fg">2–2</span>.
          </p>
          <ul className="text-fg-secondary text-sm space-y-1 list-disc list-inside">
            <li>Outcome: wrong (predicted a home win, actual was a draw).</li>
            <li>Goal difference: wrong (predicted +1, actual 0).</li>
            <li>Base tier: <span className="font-semibold text-fg">0 points</span>.</li>
            <li>Home team tally of 2 was correct → team-tally bonus applies.</li>
          </ul>
          <p className="font-semibold text-sm" style={{ color: 'var(--win)' }}>Total: 1 point</p>
        </div>
      </section>

      <section className="bg-surface rounded-card border border-border p-5 space-y-4">
        <h2 className={sectionHead}>Knockout Match Scoring</h2>
        <p className="text-fg-muted text-sm">
          Two <span className="text-fg-secondary font-medium">independent</span> components are scored and summed.
        </p>

        <div className="space-y-3">
          <h3 className="text-sm font-semibold text-fg">Component 1 — score prediction <span className="font-normal text-fg-muted">(not multiplied)</span></h3>
          <p className="text-fg-muted text-sm">Scored using the same group-stage tiers, judged at the end of the match as actually played — the 90-minute score for matches decided in normal time, or the score at the end of extra time for matches that went to ET/AET or were decided on penalties:</p>
          <table className="w-full text-sm border-collapse rounded-card overflow-hidden border border-border">
            <thead><tr className={tableHead}>
              <th className="px-4 py-2.5">Score prediction</th>
              <th className="px-4 py-2.5 text-right">Points</th>
            </tr></thead>
            <tbody className="divide-y divide-border">
              {[['Exact score','10'],['Correct goal difference (not exact)','7'],['Correct outcome only (W/D/L at the applicable cutoff)','3'],['Wrong','0']].map(([label, pts]) => (
                <tr key={label} className="even:bg-surface-2">
                  <td className={tdBase}>{label}</td>
                  <td className={tdRight}>{pts}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <p className="text-fg-muted text-sm">
            Team-tally bonus <span className="font-semibold text-fg-secondary">+1</span> applies here too — same rule as the group stage. This component is never multiplied.
          </p>
        </div>

        <div className="space-y-3">
          <h3 className="text-sm font-semibold text-fg">Component 2 — Advancing team <span className="font-normal text-fg-muted">(streak-multiplied)</span></h3>
          <p className="text-fg-muted text-sm">
            <span className="font-semibold text-fg-secondary">5 × streak points</span> if the correct team advances (including via extra time / penalties).
            Your <em>streak</em> is the number of consecutive correct advancing-team predictions you have made so far in the knockout phase.
            Each correct prediction grows your streak by 1. One wrong prediction resets it to zero.
          </p>
          <table className="w-full text-sm border-collapse rounded-card overflow-hidden border border-border">
            <thead><tr className={tableHead}>
              <th className="px-4 py-2.5">Consecutive correct predictions (streak)</th>
              <th className="px-4 py-2.5 text-right">Advancing-team bonus</th>
            </tr></thead>
            <tbody className="divide-y divide-border">
              {[['1st correct (streak = 1)','5 × 1 = 5 pts'],['2nd in a row (streak = 2)','5 × 2 = 10 pts'],['3rd in a row (streak = 3)','5 × 3 = 15 pts'],['…and so on','5 × streak']].map(([label, bonus]) => (
                <tr key={label} className="even:bg-surface-2">
                  <td className={tdBase}>{label}</td>
                  <td className={tdRight}>{bonus}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <p className="text-fg-muted text-sm">
            A miss (wrong advancing team) earns 0 and resets the streak — the next correct prediction starts back at streak = 1 (5 pts).
            A skipped match leaves your streak unchanged.
          </p>
        </div>

        <div className="rounded-card bg-surface-2 border border-border p-4 space-y-1 text-sm text-fg-muted">
          <p className="font-medium text-fg-secondary">Total = [score-prediction points] + [5 × streak if advancing team correct]</p>
        </div>

        <div className="rounded-card p-5 space-y-2 border" style={{ background: 'var(--win-soft)', borderColor: 'transparent' }}>
          <h3 className="text-sm font-display font-bold uppercase tracking-wider" style={{ color: 'var(--win)' }}>
            Worked Examples
          </h3>
          <ul className="text-fg-secondary text-sm space-y-1.5 list-disc list-inside">
            <li>5th correct in a row, correct winner + exact score: 10 + (5 × 5) = <span className="font-semibold text-fg">35 points</span></li>
            <li>Any match, exact score but wrong winner: 10 + 0 = <span className="font-semibold text-fg">10 points</span> (streak resets)</li>
            <li>1st correct prediction, correct winner but wrong score: 0 + (5 × 1) = <span className="font-semibold text-fg">5 points</span></li>
            <li>Wrong advancing team predicted, exact score at the applicable cutoff: 10 + 0 = <span className="font-semibold text-fg">10 points</span> (streak resets)</li>
            <li>Round of 16 match 1–1 after 90 min, 2–1 after extra time: a 1–1 prediction now scores 0 for Component 1 (judged at the ET cutoff, not 90 minutes) — a 2–1 prediction scores 10</li>
          </ul>
        </div>
      </section>

      <section className="bg-surface rounded-card border border-border p-5 space-y-3">
        <h2 className={sectionHead}>Champion</h2>
        <p className="text-fg-secondary text-sm leading-relaxed">
          <span className="font-semibold text-fg">100 points</span> if your predicted champion
          wins the tournament. No partial credit for finalists or semi-finalists.
        </p>
        <p className="text-fg-muted text-sm">
          Locks at the first kickoff: <span className="font-medium text-fg">11 June 2026</span>.
        </p>
      </section>

      <section className="bg-surface rounded-card border border-border p-5 space-y-4">
        <h2 className={sectionHead}>Golden Six (top scorers)</h2>
        <p className="text-fg-secondary text-sm leading-relaxed">
          Pick exactly 6 players as your top-scorer team. Picks are fully independent: any player,
          no per-team restriction, duplicates across members allowed. Locked at first kickoff.
        </p>

        <h3 className="text-sm font-semibold text-fg">Points per goal scored by a picked player</h3>
        <table className="w-full text-sm border-collapse rounded-card overflow-hidden border border-border">
          <thead><tr className={tableHead}>
            <th className="px-4 py-2.5">Position</th>
            <th className="px-4 py-2.5 text-right">Points per goal</th>
          </tr></thead>
          <tbody className="divide-y divide-border">
            {[['Forward','3'],['Midfielder','5'],['Defender','8'],['Goalkeeper','15']].map(([pos, pts]) => (
              <tr key={pos} className="even:bg-surface-2">
                <td className={tdBase}>{pos}</td>
                <td className={tdRight}>{pts}</td>
              </tr>
            ))}
          </tbody>
        </table>

        <div className="rounded-card bg-surface-2 border border-border p-4 space-y-1 text-sm text-fg-muted">
          <p>Goals counted from <span className="text-fg-secondary">regular time and extra time</span> (open play + in-match penalty kicks).</p>
          <p><span className="text-fg-secondary">Penalty-shootout goals and own goals do not count.</span></p>
          <p>A player's position is fixed to the position listed in the tournament data.</p>
        </div>
      </section>

      <section className="bg-surface rounded-card border border-border p-5 space-y-3">
        <h2 className={sectionHead}>Locks &amp; Visibility</h2>
        <ul className="text-fg-secondary text-sm space-y-2 list-disc list-inside leading-relaxed">
          <li>Each match locks at its own kickoff (server time); predictions cannot be edited after that.</li>
          <li>Champion and Golden Six lock at the first kickoff: <span className="font-medium text-fg">11 June 2026</span>.</li>
          <li>Another member's prediction for a match becomes visible only after that match has locked.</li>
        </ul>
      </section>
    </div>
  );
}
