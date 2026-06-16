import { flagUrl } from '../utils/flagUrl.ts';

export interface StandingsMatchInput {
  homeTeamId: string;
  homeTeamName: string;
  awayTeamId: string;
  awayTeamName: string;
  homeScore: number | null;
  awayScore: number | null;
}

interface TeamStandingRow {
  teamId: string;
  teamName: string;
  played: number;
  won: number;
  drawn: number;
  lost: number;
  goalsFor: number;
  goalsAgainst: number;
  points: number;
}

function buildStandings(matches: StandingsMatchInput[]): TeamStandingRow[] {
  const rows = new Map<string, TeamStandingRow>();
  const ensure = (id: string, name: string) => {
    let row = rows.get(id);
    if (!row) {
      row = { teamId: id, teamName: name, played: 0, won: 0, drawn: 0, lost: 0, goalsFor: 0, goalsAgainst: 0, points: 0 };
      rows.set(id, row);
    }
    return row;
  };

  for (const m of matches) {
    ensure(m.homeTeamId, m.homeTeamName);
    ensure(m.awayTeamId, m.awayTeamName);
  }

  for (const m of matches) {
    if (m.homeScore == null || m.awayScore == null) continue;
    const home = ensure(m.homeTeamId, m.homeTeamName);
    const away = ensure(m.awayTeamId, m.awayTeamName);

    home.played++; away.played++;
    home.goalsFor += m.homeScore; home.goalsAgainst += m.awayScore;
    away.goalsFor += m.awayScore; away.goalsAgainst += m.homeScore;

    if (m.homeScore > m.awayScore) { home.won++; home.points += 3; away.lost++; }
    else if (m.homeScore < m.awayScore) { away.won++; away.points += 3; home.lost++; }
    else { home.drawn++; away.drawn++; home.points++; away.points++; }
  }

  return [...rows.values()].sort((a, b) => {
    if (b.points !== a.points) return b.points - a.points;
    const gdA = a.goalsFor - a.goalsAgainst;
    const gdB = b.goalsFor - b.goalsAgainst;
    if (gdB !== gdA) return gdB - gdA;
    if (b.goalsFor !== a.goalsFor) return b.goalsFor - a.goalsFor;
    return a.teamName.localeCompare(b.teamName);
  });
}

interface GroupStandingsTableProps {
  matches: StandingsMatchInput[];
}

export function GroupStandingsTable({ matches }: GroupStandingsTableProps) {
  const standings = buildStandings(matches);
  if (standings.length === 0) return null;

  return (
    <div className="rounded-card bg-surface border border-border overflow-hidden mb-3">
      <div className="overflow-x-auto">
        <table className="w-full text-[12px]">
          <thead>
            <tr className="text-fg-muted text-[10px] font-display font-bold uppercase tracking-wider border-b border-border">
              <th className="px-2 py-2 text-left w-6">#</th>
              <th className="px-2 py-2 text-left">Team</th>
              <th className="px-1.5 py-2 text-center" title="Played">P</th>
              <th className="px-1.5 py-2 text-center" title="Won">W</th>
              <th className="px-1.5 py-2 text-center" title="Drawn">D</th>
              <th className="px-1.5 py-2 text-center" title="Lost">L</th>
              <th className="px-1.5 py-2 text-center" title="Goals For">GF</th>
              <th className="px-1.5 py-2 text-center" title="Goals Against">GA</th>
              <th className="px-2 py-2 text-center" title="Points">Pts</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {standings.map((row, i) => (
              <tr key={row.teamId} className="text-fg-secondary">
                <td className="px-2 py-2 font-display font-bold text-fg-muted tnum">{i + 1}</td>
                <td className="px-2 py-2">
                  <div className="flex items-center gap-2 min-w-0">
                    {flagUrl(row.teamId) && (
                      <img src={flagUrl(row.teamId)} alt="" width={20} height={14} className="flag shrink-0" />
                    )}
                    <span className="truncate font-semibold text-fg">{row.teamName}</span>
                  </div>
                </td>
                <td className="px-1.5 py-2 text-center tnum">{row.played}</td>
                <td className="px-1.5 py-2 text-center tnum">{row.won}</td>
                <td className="px-1.5 py-2 text-center tnum">{row.drawn}</td>
                <td className="px-1.5 py-2 text-center tnum">{row.lost}</td>
                <td className="px-1.5 py-2 text-center tnum">{row.goalsFor}</td>
                <td className="px-1.5 py-2 text-center tnum">{row.goalsAgainst}</td>
                <td className="px-2 py-2 text-center font-display font-bold tnum">{row.points}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
