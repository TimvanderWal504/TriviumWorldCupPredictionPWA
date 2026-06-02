/**
 * RulesPage — full scoring explainer for Trivium World Cup 2026.
 *
 * All scoring values are canonical and must match the scoring engine exactly.
 * Source of truth: Confluence "Rules & Scoring (canonical)".
 */
export function RulesPage() {
  return (
    <div className="max-w-3xl mx-auto p-6 space-y-10">
      <h1 className="text-3xl font-bold text-white">Rules &amp; Scoring</h1>

      {/* ------------------------------------------------------------------ */}
      {/* Tournament Format                                                    */}
      {/* ------------------------------------------------------------------ */}
      <section className="space-y-3">
        <h2 className="text-xl font-semibold text-blue-400 border-b border-slate-700 pb-2">
          Tournament Format
        </h2>
        <p className="text-slate-300 leading-relaxed">
          FIFA World Cup 2026 — 48 teams, 12 groups (A–L) of four teams each, 104 total matches.
          The top two from each group plus the eight best third-placed teams advance to a{' '}
          <span className="text-white font-medium">Round of 32</span>, then{' '}
          <span className="text-white font-medium">Round of 16</span>, quarter-finals, semi-finals,
          third-place play-off, and the Final.
        </p>
        <ul className="text-slate-300 space-y-1 list-disc list-inside">
          <li>Group stage: 11–27 June 2026</li>
          <li>Knockout rounds begin: 28 June 2026</li>
          <li>Final: 19 July 2026</li>
        </ul>
      </section>

      {/* ------------------------------------------------------------------ */}
      {/* Group Match Scoring                                                  */}
      {/* ------------------------------------------------------------------ */}
      <section className="space-y-4">
        <h2 className="text-xl font-semibold text-blue-400 border-b border-slate-700 pb-2">
          Group Match Scoring
        </h2>
        <p className="text-slate-400 text-sm">
          Award the single best tier — tiers are <strong className="text-slate-300">not</strong> cumulative.
        </p>

        <table className="w-full text-sm border-collapse">
          <thead>
            <tr className="bg-slate-800 text-slate-300 text-left">
              <th className="px-4 py-2 font-medium rounded-tl-lg">Prediction result</th>
              <th className="px-4 py-2 font-medium rounded-tr-lg text-right">Points</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-700">
            <tr className="bg-slate-800/50">
              <td className="px-4 py-2 text-slate-300">Exact score</td>
              <td className="px-4 py-2 text-right font-semibold text-white">10</td>
            </tr>
            <tr className="bg-slate-800/30">
              <td className="px-4 py-2 text-slate-300">Correct goal difference (not exact)</td>
              <td className="px-4 py-2 text-right font-semibold text-white">7</td>
            </tr>
            <tr className="bg-slate-800/50">
              <td className="px-4 py-2 text-slate-300">Correct outcome only (W/D/L)</td>
              <td className="px-4 py-2 text-right font-semibold text-white">3</td>
            </tr>
            <tr className="bg-slate-800/30">
              <td className="px-4 py-2 text-slate-300">Wrong</td>
              <td className="px-4 py-2 text-right font-semibold text-white">0</td>
            </tr>
          </tbody>
        </table>

        <div className="bg-slate-800/60 rounded-lg p-4 space-y-2">
          <h3 className="text-sm font-semibold text-slate-200">Team-tally bonus</h3>
          <p className="text-slate-400 text-sm leading-relaxed">
            <span className="text-white font-medium">+1 point</span> if exactly one team's goal count
            was predicted correctly. This bonus can only add to the correct-outcome tier (3 → 4) or
            the wrong tier (0 → 1).
          </p>
          <ul className="text-slate-400 text-sm space-y-1 list-disc list-inside">
            <li>
              Two correct tallies = exact score — already captured in the 10-point tier.
            </li>
            <li>
              A correct goal difference that is not exact makes any individual tally mathematically
              impossible, so the bonus cannot apply there.
            </li>
          </ul>
        </div>

        {/* Worked example callout */}
        <div className="bg-blue-950/60 border border-blue-700 rounded-lg p-5 space-y-2">
          <h3 className="text-sm font-bold text-blue-300 uppercase tracking-wide">
            Worked Example
          </h3>
          <p className="text-slate-200 text-sm leading-relaxed">
            Predicted <span className="font-semibold text-white">2–1</span>, actual result{' '}
            <span className="font-semibold text-white">2–2</span>.
          </p>
          <ul className="text-slate-300 text-sm space-y-1 list-disc list-inside">
            <li>Outcome: wrong — predicted a home win, actual was a draw.</li>
            <li>Goal difference: wrong (predicted +1, actual 0).</li>
            <li>Base tier: <span className="font-semibold text-white">0 points</span>.</li>
            <li>Home team tally of 2 was correct → team-tally bonus applies.</li>
          </ul>
          <p className="text-blue-300 font-semibold text-sm">
            Total: 1 point
          </p>
        </div>
      </section>

      {/* ------------------------------------------------------------------ */}
      {/* Knockout Match Scoring                                               */}
      {/* ------------------------------------------------------------------ */}
      <section className="space-y-4">
        <h2 className="text-xl font-semibold text-blue-400 border-b border-slate-700 pb-2">
          Knockout Match Scoring
        </h2>
        <p className="text-slate-400 text-sm">
          Per match, before the round multiplier:
        </p>
        <ul className="text-slate-300 text-sm space-y-2 list-disc list-inside">
          <li>
            <span className="text-white font-semibold">5 points</span> — correct advancing team
            (who actually progresses, including after extra time / penalties)
          </li>
          <li>
            <span className="text-white font-semibold">+3 points</span> — exact 90-minute score
            bonus (normal time only)
          </li>
        </ul>

        <h3 className="text-sm font-semibold text-slate-200 pt-2">Round multipliers</h3>
        <table className="w-full text-sm border-collapse">
          <thead>
            <tr className="bg-slate-800 text-slate-300 text-left">
              <th className="px-4 py-2 font-medium rounded-tl-lg">Round</th>
              <th className="px-4 py-2 font-medium rounded-tr-lg text-right">Multiplier</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-700">
            <tr className="bg-slate-800/50">
              <td className="px-4 py-2 text-slate-300">Round of 32</td>
              <td className="px-4 py-2 text-right font-semibold text-white">×1.0</td>
            </tr>
            <tr className="bg-slate-800/30">
              <td className="px-4 py-2 text-slate-300">Round of 16</td>
              <td className="px-4 py-2 text-right font-semibold text-white">×1.5</td>
            </tr>
            <tr className="bg-slate-800/50">
              <td className="px-4 py-2 text-slate-300">Quarter-final</td>
              <td className="px-4 py-2 text-right font-semibold text-white">×2.0</td>
            </tr>
            <tr className="bg-slate-800/30">
              <td className="px-4 py-2 text-slate-300">Semi-final &amp; third-place play-off</td>
              <td className="px-4 py-2 text-right font-semibold text-white">×2.5</td>
            </tr>
            <tr className="bg-slate-800/50">
              <td className="px-4 py-2 text-slate-300">Final</td>
              <td className="px-4 py-2 text-right font-semibold text-white">×3.0</td>
            </tr>
          </tbody>
        </table>

        <div className="bg-slate-800/60 rounded-lg px-4 py-3 text-sm text-slate-300">
          Example: In the Final, correct winner + exact 90-min score ={' '}
          <span className="text-white font-semibold">(5 + 3) × 3.0 = 24 points</span>.
        </div>
      </section>

      {/* ------------------------------------------------------------------ */}
      {/* Champion                                                             */}
      {/* ------------------------------------------------------------------ */}
      <section className="space-y-3">
        <h2 className="text-xl font-semibold text-blue-400 border-b border-slate-700 pb-2">
          Champion
        </h2>
        <p className="text-slate-300 text-sm leading-relaxed">
          <span className="text-white font-semibold">100 points</span> if your predicted champion
          wins the tournament. No partial credit for finalists or semi-finalists.
        </p>
        <p className="text-slate-400 text-sm">
          Locks at the first kickoff: <span className="text-white font-medium">11 June 2026</span>.
        </p>
      </section>

      {/* ------------------------------------------------------------------ */}
      {/* Golden Six                                                           */}
      {/* ------------------------------------------------------------------ */}
      <section className="space-y-4">
        <h2 className="text-xl font-semibold text-blue-400 border-b border-slate-700 pb-2">
          Golden Six (top scorers)
        </h2>
        <p className="text-slate-300 text-sm leading-relaxed">
          Pick exactly 6 players as your top-scorer team. Picks are fully independent — any player,
          no per-team restriction, duplicates across members allowed. Locked for the whole tournament
          at the first kickoff.
        </p>

        <h3 className="text-sm font-semibold text-slate-200">Points per goal scored by a picked player</h3>
        <table className="w-full text-sm border-collapse">
          <thead>
            <tr className="bg-slate-800 text-slate-300 text-left">
              <th className="px-4 py-2 font-medium rounded-tl-lg">Position</th>
              <th className="px-4 py-2 font-medium rounded-tr-lg text-right">Points per goal</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-700">
            <tr className="bg-slate-800/50">
              <td className="px-4 py-2 text-slate-300">Forward</td>
              <td className="px-4 py-2 text-right font-semibold text-white">3</td>
            </tr>
            <tr className="bg-slate-800/30">
              <td className="px-4 py-2 text-slate-300">Midfielder</td>
              <td className="px-4 py-2 text-right font-semibold text-white">5</td>
            </tr>
            <tr className="bg-slate-800/50">
              <td className="px-4 py-2 text-slate-300">Defender</td>
              <td className="px-4 py-2 text-right font-semibold text-white">8</td>
            </tr>
            <tr className="bg-slate-800/30">
              <td className="px-4 py-2 text-slate-300">Goalkeeper</td>
              <td className="px-4 py-2 text-right font-semibold text-white">15</td>
            </tr>
          </tbody>
        </table>

        <div className="bg-slate-800/60 rounded-lg p-4 space-y-1 text-sm text-slate-400">
          <p>
            Goals counted from <span className="text-slate-300">regular time and extra time</span>
            {' '}(open play + in-match penalty kicks).
          </p>
          <p>
            <span className="text-slate-300">Penalty-shootout goals and own goals do NOT count.</span>
          </p>
          <p>
            A player's position is fixed to the position listed in the tournament data.
          </p>
        </div>
      </section>

      {/* ------------------------------------------------------------------ */}
      {/* Locks & Visibility                                                   */}
      {/* ------------------------------------------------------------------ */}
      <section className="space-y-3">
        <h2 className="text-xl font-semibold text-blue-400 border-b border-slate-700 pb-2">
          Locks &amp; Visibility
        </h2>
        <ul className="text-slate-300 text-sm space-y-2 list-disc list-inside leading-relaxed">
          <li>
            Each match locks at its own kickoff (server time) — predictions cannot be edited
            after that.
          </li>
          <li>
            Champion and Golden Six lock at the first kickoff:{' '}
            <span className="text-white font-medium">11 June 2026</span>.
          </li>
          <li>
            Another member's prediction for a match becomes visible only after that match has
            locked.
          </li>
        </ul>
      </section>
    </div>
  );
}
