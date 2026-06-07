import { useEffect, useState } from 'react';
import { Radio, ListChecks, Trophy, BarChart3, User, NotebookPen } from 'lucide-react';
import { AuthProvider } from './auth/AuthContext.tsx';
import { OfflineBanner } from './components/OfflineBanner.tsx';
import { ProfileSetupModal } from './auth/ProfileSetupModal.tsx';
import { LoginPage } from './auth/LoginPage.tsx';
import { SignUpPage } from './auth/SignUpPage.tsx';
import { AdminPage } from './pages/AdminPage.tsx';
import { GroupPredictionsPage } from './pages/GroupPredictionsPage.tsx';
import { KnockoutBracketPage } from './pages/KnockoutBracketPage.tsx';
import { LeaderboardPage } from './pages/LeaderboardPage.tsx';
import { LiveScoresPage } from './pages/LiveScoresPage.tsx';
import { ProfilePage } from './pages/ProfilePage.tsx';
import { RulesPage } from './pages/RulesPage.tsx';
import { StandingsPage } from './pages/StandingsPage.tsx';
import { TournamentPredictionPage } from './pages/TournamentPredictionPage.tsx';
import { useAuth } from './auth/useAuth.ts';


type Tab = 'live' | 'predict' | 'bracket' | 'ranks' | 'rules' | 'me';
// 'tournament' is a sub-page within the 'predict' tab, not a top-level subPage
type SubPage = 'profile' | 'admin' | null;
type PredictView = 'group' | 'tournament';

const ALL_TABS: { id: Tab; label: string; Icon: React.FC<{ size?: number }> }[] = [
  { id: 'live',    label: 'Live',    Icon: Radio },
  { id: 'predict', label: 'Predict', Icon: ListChecks },
  { id: 'bracket', label: 'Bracket', Icon: Trophy },
  { id: 'ranks',   label: 'Ranks',   Icon: BarChart3 },
  { id: 'rules',   label: 'Rules',   Icon: NotebookPen },
  { id: 'me',      label: 'Me',      Icon: User },
];

const TAB_TITLES: Record<Tab, string> = {
  live:    'Live Scores',
  predict: 'Predictions',
  bracket: 'Knockout Bracket',
  ranks:   'Leaderboard',
  rules:   'Rules & Scoring',
  me:      'My Standings',
};

const SUB_TITLES: Record<NonNullable<SubPage>, string> = {
  profile: 'Profile',
  admin:   'Admin',
};

type AuthView = 'login' | 'signup';

function AuthGateway() {
  const { reload } = useAuth();
  const [view, setView] = useState<AuthView>('login');

  return (
    <div className="flex flex-col items-center justify-center min-h-screen py-20 px-4">
      <p className="font-display font-black text-[13px] tracking-[0.25em] text-pitch-500 uppercase mb-3">TWC 2026</p>
      <h1 className="font-display font-black text-4xl tracking-tight mb-8 text-center">
        Trivium World Cup<br />2026
      </h1>
      {view === 'login'
        ? <LoginPage onLoggedIn={reload} onSwitchToSignUp={() => setView('signup')} />
        : <SignUpPage onSwitchToLogin={() => setView('login')} />
      }
    </div>
  );
}

function AppShell() {
  const { user, isLoading, hasProfile, signOut } = useAuth();
  const [tab, setTab] = useState<Tab>('predict');
  const [subPage, setSubPage] = useState<SubPage>(null);
  const [predictView, setPredictView] = useState<PredictView>('group');

  // Visibility gates for Live and Bracket tabs
  const [liveActive, setLiveActive] = useState(false);
  const [bracketOpen, setBracketOpen] = useState(false);

  useEffect(() => {
    if (!user) return;

    // Check live window status
    fetch('/fixtures/live', { credentials: 'include' })
      .then(r => r.json())
      .then((d: { liveWindowActive?: boolean; fixtures?: { status: string }[] }) => {
        const active = d.liveWindowActive === true ||
          (d.fixtures ?? []).some(f => f.status === 'InProgress');
        setLiveActive(active);
        // If we were on the live tab but it's no longer active, fall back to predict
        if (!active) setTab(prev => prev === 'live' ? 'predict' : prev);
      })
      .catch(() => {});

    // Check if bracket is open (any slot has teams determined)
    fetch('/knockout/slots', { credentials: 'include' })
      .then(r => r.json())
      .then((slots: { homeTeamId: string | null }[]) => {
        setBracketOpen(slots.some(s => s.homeTeamId !== null));
      })
      .catch(() => {});
  }, [user]);

  function goTab(t: Tab) {
    setTab(t);
    setSubPage(null);
    if (t === 'predict') setPredictView('group');
  }

  if (isLoading) {
    return (
      <div className="min-h-screen bg-bg text-fg flex items-center justify-center">
        <p className="text-fg-muted font-sans">Loading…</p>
      </div>
    );
  }

  const visibleTabs = ALL_TABS.filter(t => {
    if (t.id === 'live' && !liveActive) return false;
    if (t.id === 'bracket' && !bracketOpen) return false;
    return true;
  });

  const pageTitle = subPage
    ? SUB_TITLES[subPage]
    : tab === 'predict' && predictView === 'tournament'
    ? 'Tournament Prediction'
    : TAB_TITLES[tab];

  return (
    <div className="min-h-screen bg-bg text-fg font-sans flex flex-col">

      <OfflineBanner />

      {/* ── Top bar ── */}
      {user && hasProfile && (
        <header className="sticky top-0 z-30 bg-bg-elevated border-b border-border px-4 py-3 flex items-center justify-between gap-2">
          {/* Logo */}
          <div className="flex items-center gap-1.5 shrink-0 cursor-pointer select-none" onClick={() => goTab('predict')}>
            <span className="font-display font-black text-[20px] tracking-tight text-fg">
              T<span className="text-pitch-500">W</span>C
            </span>
            <span className="font-mono text-[10px] text-fg-muted tracking-[0.2em] pt-0.5">2026</span>
          </div>

          {/* Page title (centered) */}
          <h1 className="font-display font-bold text-[17px] tracking-tight flex-1 text-center truncate">
            {pageTitle}
          </h1>

          {/* Right — spacer keeps title centred */}
          <div className="shrink-0 w-[52px]" />
        </header>
      )}

      {/* ── Main content ── */}
      <main className="flex-1 overflow-y-auto pb-[4.25rem]">
        {!user ? (
          <div data-testid="signin-prompt">
            <AuthGateway />
          </div>

        ) : tab === 'live' ? (
          <LiveScoresPage />

        ) : tab === 'predict' ? (
          <div>
            <div className="px-4 pt-4 pb-2 flex gap-2">
              <SubPill active={predictView === 'group'} onClick={() => setPredictView('group')}>Group Stage</SubPill>
              <SubPill active={predictView === 'tournament'} onClick={() => setPredictView('tournament')}>Tournament</SubPill>
            </div>
            {predictView === 'tournament'
              ? <TournamentPredictionPage />
              : <GroupPredictionsPage onAllGroupsComplete={() => setPredictView('tournament')} />}
          </div>

        ) : tab === 'bracket' ? (
          <KnockoutBracketPage />

        ) : tab === 'ranks' ? (
          <LeaderboardPage />

        ) : tab === 'rules' ? (
          <RulesPage />

        ) : tab === 'me' ? (
          <div>
            <div className="px-4 pt-4 pb-2 flex flex-wrap gap-2">
              <SubPill active={subPage === null} onClick={() => setSubPage(null)}>Standings</SubPill>
              <SubPill active={subPage === 'profile'} onClick={() => setSubPage('profile')}>Profile</SubPill>
              {user.roles?.includes('admin') && (
                <SubPill active={subPage === 'admin'} onClick={() => setSubPage('admin')} accent>Admin</SubPill>
              )}
              <button
                onClick={signOut}
                className="px-3.5 py-1.5 rounded-input text-[13px] font-semibold transition-colors bg-surface-3 text-fg-muted hover:text-fg ml-auto"
              >
                Sign out
              </button>
            </div>
            {subPage === 'profile' ? <ProfilePage />
              : subPage === 'admin'   ? <AdminPage />
              : <StandingsPage />}
          </div>

        ) : null}
      </main>

      {/* ── Bottom tab bar ── */}
      {user && hasProfile && (
        <nav
          data-testid="app-nav"
          className="fixed bottom-0 inset-x-0 z-40 bg-bg-elevated/95 backdrop-blur-sm border-t border-border pb-safe pt-1.5"
          style={{
            display: 'grid',
            gridTemplateColumns: `repeat(${visibleTabs.length}, 1fr)`,
            boxShadow: '0 -8px 30px -16px rgba(0,0,0,0.4)',
          }}
        >
          {visibleTabs.map(({ id, label, Icon }) => {
            const active = tab === id && subPage === null;
            return (
              <button
                key={id}
                onClick={() => goTab(id)}
                className={`flex flex-col items-center gap-0.5 py-1.5 transition-colors ${
                  active ? 'text-pitch-600 dark:text-pitch-400' : 'text-fg-muted'
                }`}
              >
                <Icon size={22} />
                <span className={`text-[10px] ${active ? 'font-bold' : 'font-medium'}`}>{label}</span>
              </button>
            );
          })}
        </nav>
      )}

      {user && !hasProfile && <ProfileSetupModal />}
    </div>
  );
}

function SubPill({
  active, onClick, accent = false, children,
}: {
  active: boolean; onClick: () => void; accent?: boolean; children: React.ReactNode;
}) {
  if (active) {
    return (
      <button
        onClick={onClick}
        className="px-3.5 py-1.5 rounded-input text-[13px] font-semibold transition-colors"
        style={{
          background: accent ? 'var(--accent-fill)' : 'var(--secondary-fill)',
          color: accent ? 'var(--fg-ongold)' : 'var(--fg-onblue)',
        }}
      >
        {children}
      </button>
    );
  }
  return (
    <button
      onClick={onClick}
      className={`px-3.5 py-1.5 rounded-input text-[13px] font-semibold transition-colors bg-surface-3 hover:text-fg ${
        accent ? 'text-accent' : 'text-fg-secondary'
      }`}
    >
      {children}
    </button>
  );
}

export default function App() {
  return (
    <AuthProvider>
      <AppShell />
    </AuthProvider>
  );
}
