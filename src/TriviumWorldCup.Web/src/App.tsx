import { useEffect, useState } from 'react';
import { Radio, ListChecks, Trophy, BarChart3, User, NotebookPen, ClipboardCheck } from 'lucide-react';
import triviumLogomark from './assets/_Trivium_Logos/trivium_logomark_transparent.svg';
import heroBg from './assets/hero-2000-DW7OUruS.svg';
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
import { ResultsPage } from './pages/ResultsPage.tsx';
import { ProfilePage } from './pages/ProfilePage.tsx';
import { RulesPage } from './pages/RulesPage.tsx';
import { StandingsPage } from './pages/StandingsPage.tsx';
import { TournamentPredictionPage } from './pages/TournamentPredictionPage.tsx';
import { useAuth } from './auth/useAuth.ts';
import { useAppUpdate } from './hooks/useAppUpdate.ts';
import { UpdateModal } from './components/UpdateModal.tsx';


// Branding — sourced from build-time env vars; defaults reproduce the WC 2026 build.
// VITE_APP_NAV_LABEL uses a pipe (|) to separate the two display lines shown in
// the nav header and the login screen (e.g. "World Cup|2026").
const [NAV_LINE1, NAV_LINE2] = (import.meta.env.VITE_APP_NAV_LABEL ?? 'World Cup|2026').split('|');

type Tab = 'live' | 'predict' | 'results' | 'bracket' | 'ranks' | 'rules' | 'me';
// 'tournament' is a sub-page within the 'predict' tab, not a top-level subPage
type SubPage = 'profile' | 'admin' | null;
type PredictView = 'group' | 'tournament';

const ALL_TABS: { id: Tab; label: string; Icon: React.FC<{ size?: number }> }[] = [
  { id: 'live',    label: 'Live',    Icon: Radio },
  { id: 'predict', label: 'Predict', Icon: ListChecks },
  { id: 'results', label: 'Results', Icon: ClipboardCheck },
  { id: 'bracket', label: 'Bracket', Icon: Trophy },
  { id: 'ranks',   label: 'Ranks',   Icon: BarChart3 },
  { id: 'rules',   label: 'Rules',   Icon: NotebookPen },
  { id: 'me',      label: 'Me',      Icon: User },
];

const TAB_TITLES: Record<Tab, string> = {
  live:    'Live Scores',
  predict: 'Predictions',
  results: 'Results',
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
    <div
      className="flex flex-col items-center justify-center min-h-screen py-20 px-4"
    >
      <img src={triviumLogomark} alt="Trivium" className="w-32 h-32" />
      <p className="font-mono text-[32px] tracking-[0.15em] text-fg-muted uppercase mb-1">{NAV_LINE1}</p>
      <h1 className="font-display font-black text-5xl tracking-tight mb-8 text-center text-fg">
        {NAV_LINE2}
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
  const { update, dismiss, reload } = useAppUpdate();
  const [tab, setTab] = useState<Tab>('predict');
  const [subPage, setSubPage] = useState<SubPage>(null);
  const [predictView, setPredictView] = useState<PredictView>('group');
  const [groupViewMode, setGroupViewMode] = useState<'group' | 'date'>('group');
  const [resultsViewMode, setResultsViewMode] = useState<'group' | 'date'>('date');

  // Visibility gates for Live, Results, and Bracket tabs
  const [liveActive, setLiveActive] = useState(false);
  const [hasResults, setHasResults] = useState(false);
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
        if (!active) setTab(prev => prev === 'live' ? 'predict' : prev);
      })
      .catch(() => {});

    // Check if any fixtures have been completed
    fetch('/fixtures', { credentials: 'include' })
      .then(r => r.json())
      .then((fixtures: { status: string }[]) => {
        const completed = fixtures.some(f => f.status === 'Completed');
        setHasResults(completed);
        if (!completed) setTab(prev => prev === 'results' ? 'predict' : prev);
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
    if (t.id === 'results' && !hasResults) return false;
    if (t.id === 'bracket' && !bracketOpen) return false;
    return true;
  });

  const pageTitle = subPage
    ? SUB_TITLES[subPage]
    : tab === 'predict' && predictView === 'tournament'
    ? 'Tournament Prediction'
    : TAB_TITLES[tab];

  return (
    <div
      className="min-h-screen bg-bg text-fg font-sans flex flex-col"
      style={{ backgroundImage: `url(${heroBg})`, backgroundSize: 'cover', backgroundPosition: 'center', backgroundAttachment: 'fixed' }}
    >

      <OfflineBanner />

      {/* ── Top bar ── */}
      {user && hasProfile && (
        <header className="sticky top-0 z-30 bg-bg-elevated border-b border-border px-4 py-3 flex items-center justify-between gap-2">
          {/* Logo */}
          <div className="flex items-center shrink-0 cursor-pointer select-none" onClick={() => goTab('predict')}>
            <img src={triviumLogomark} alt="Trivium" className="h-12 w-12" />
            <span className="font-mono text-[11px] leading-tight text-fg-muted tracking-[0.15em] uppercase">{NAV_LINE1}<br />{NAV_LINE2}</span>
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

        ) : tab === 'results' ? (
          <div>
            <div className="max-w-3xl mx-auto px-4 pt-4 pb-2 flex items-center justify-end gap-2">
              <div className="flex gap-0.5 bg-surface-3 rounded-input p-0.5">
                {(['group', 'date'] as const).map(mode => (
                  <button
                    key={mode}
                    onClick={() => setResultsViewMode(mode)}
                    className="px-3 py-1.5 rounded-input text-[12px] font-semibold whitespace-nowrap transition-colors"
                    style={resultsViewMode === mode
                      ? { background: 'var(--secondary-fill)', color: 'var(--fg-onblue)' }
                      : { color: 'var(--fg-secondary)' }}
                  >
                    {mode === 'group' ? 'By Group' : 'By Date'}
                  </button>
                ))}
              </div>
            </div>
            <ResultsPage viewMode={resultsViewMode} />
          </div>

        ) : tab === 'predict' ? (
          <div>
            <div className="max-w-3xl mx-auto px-4 pt-4 pb-2 flex flex-col gap-2">
              <div className="flex items-center justify-between gap-2">
                <div className="flex gap-2">
                  <SubPill active={predictView === 'group'} onClick={() => setPredictView('group')}>Group Stage</SubPill>
                  <SubPill active={predictView === 'tournament'} onClick={() => setPredictView('tournament')}>Tournament</SubPill>
                </div>
                {predictView === 'group' && (
                  <div className="flex gap-0.5 bg-surface-3 rounded-input p-0.5">
                    {(['group', 'date'] as const).map(mode => (
                      <button
                        key={mode}
                        onClick={() => setGroupViewMode(mode)}
                        className="px-3 py-1.5 rounded-input text-[12px] font-semibold whitespace-nowrap transition-colors"
                        style={groupViewMode === mode
                          ? { background: 'var(--secondary-fill)', color: 'var(--fg-onblue)' }
                          : { color: 'var(--fg-secondary)' }}
                      >
                        {mode === 'group' ? 'By Group' : 'By Date'}
                      </button>
                    ))}
                  </div>
                )}
              </div>
            </div>
            {predictView === 'tournament'
              ? <TournamentPredictionPage />
              : <GroupPredictionsPage viewMode={groupViewMode} onAllGroupsComplete={() => setPredictView('tournament')} />}
          </div>

        ) : tab === 'bracket' ? (
          <KnockoutBracketPage />

        ) : tab === 'ranks' ? (
          <LeaderboardPage />

        ) : tab === 'rules' ? (
          <RulesPage />

        ) : tab === 'me' ? (
          <div>
            <div className="max-w-2xl mx-auto px-4 pt-4 pb-2 flex flex-wrap gap-2">
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
                  active ? 'text-primary' : 'text-fg-muted'
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

      {update && (
        <UpdateModal
          changelog={update.changelog}
          pendingReload={update.pendingReload}
          onDismiss={dismiss}
          onReload={reload}
        />
      )}
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
        className="px-3.5 py-1.5 rounded-input text-[13px] font-semibold whitespace-nowrap transition-colors"
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
      className={`px-3.5 py-1.5 rounded-input text-[13px] font-semibold whitespace-nowrap transition-colors bg-surface-3 hover:text-fg ${
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
