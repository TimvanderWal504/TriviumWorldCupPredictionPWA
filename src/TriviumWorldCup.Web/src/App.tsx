import { useState } from 'react';
import { AuthProvider } from './auth/AuthContext.tsx';
import { DevUserSwitcher } from './auth/DevUserSwitcher.tsx';
import { OfflineBanner } from './components/OfflineBanner.tsx';
import { ProfileSetupModal } from './auth/ProfileSetupModal.tsx';
import { GroupPredictionsPage } from './pages/GroupPredictionsPage.tsx';
import { LeaderboardPage } from './pages/LeaderboardPage.tsx';
import { ProfilePage } from './pages/ProfilePage.tsx';
import { RulesPage } from './pages/RulesPage.tsx';
import { StandingsPage } from './pages/StandingsPage.tsx';
import { TournamentPredictionPage } from './pages/TournamentPredictionPage.tsx';
import { useAuth } from './auth/useAuth.ts';

const IS_PROD = import.meta.env.PROD;

type Page = 'home' | 'profile' | 'predictions' | 'tournament' | 'rules' | 'standings' | 'leaderboard';

function AppShell() {
  const { user, isLoading, hasProfile, signOut } = useAuth();
  const [page, setPage] = useState<Page>('home');

  if (isLoading) {
    return (
      <div className="min-h-screen bg-slate-900 text-white flex items-center justify-center">
        <p className="text-slate-400">Loading…</p>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-slate-900 text-white flex flex-col">
      <OfflineBanner />
      {/* Nav — only shown when authenticated */}
      {user && hasProfile && (
        <nav className="bg-slate-800 border-b border-slate-700 px-6 py-3 flex items-center justify-between">
          <button
            onClick={() => setPage('home')}
            className="text-lg font-bold text-white hover:text-blue-400 transition-colors"
          >
            TWC 2026
          </button>
          <div className="flex items-center gap-4 text-sm">
            <button
              onClick={() => setPage('predictions')}
              className="text-slate-300 hover:text-white transition-colors"
            >
              Predictions
            </button>
            <button
              onClick={() => setPage('tournament')}
              className="text-slate-300 hover:text-white transition-colors"
            >
              Tournament
            </button>
            <button
              onClick={() => setPage('standings')}
              className="text-slate-300 hover:text-white transition-colors"
            >
              My Standings
            </button>
            <button
              onClick={() => setPage('leaderboard')}
              className="text-slate-300 hover:text-white transition-colors"
            >
              Leaderboard
            </button>
            <button
              onClick={() => setPage('rules')}
              className="text-slate-300 hover:text-white transition-colors"
            >
              Rules
            </button>
            <button
              onClick={() => setPage('profile')}
              className="text-slate-300 hover:text-white transition-colors"
            >
              {user.displayName}
            </button>
            <button
              onClick={signOut}
              className="text-slate-400 hover:text-white transition-colors"
            >
              Sign out
            </button>
          </div>
        </nav>
      )}

      {/* Page content */}
      <main className="flex-1">
        {!user ? (
          // Not signed in — placeholder until TWC-3 sign-in UI is fuller
          <div className="flex flex-col items-center justify-center h-full py-20 text-center px-4">
            <h1 className="text-4xl font-bold tracking-tight mb-2">Trivium World Cup 2026</h1>
            <p className="text-slate-400 text-lg">Prediction pool — sign in to start predicting.</p>
            {!IS_PROD && <DevUserSwitcher />}
          </div>
        ) : page === 'predictions' ? (
          <GroupPredictionsPage />
        ) : page === 'tournament' ? (
          <TournamentPredictionPage />
        ) : page === 'standings' ? (
          <StandingsPage />
        ) : page === 'leaderboard' ? (
          <LeaderboardPage />
        ) : page === 'rules' ? (
          <RulesPage />
        ) : page === 'profile' ? (
          <ProfilePage />
        ) : (
          // Home placeholder — feature screens come in later stories
          <div className="flex flex-col items-center justify-center h-full py-20 text-center px-4">
            <h1 className="text-4xl font-bold tracking-tight mb-2">Trivium World Cup 2026</h1>
            <p className="text-slate-400 text-lg mb-2">Prediction pool — coming soon.</p>
            {user && (
              <p className="text-slate-500 text-sm">
                Signed in as <span className="text-white font-medium">{user.displayName}</span>
              </p>
            )}
          </div>
        )}
      </main>

      {/* Profile setup — shown when authenticated but no profile yet */}
      {user && !hasProfile && <ProfileSetupModal />}

      {/* Dev switcher overlay — outside nav so it's always accessible in dev */}
      {!IS_PROD && user && hasProfile && <DevUserSwitcher />}
    </div>
  );
}

function App() {
  return (
    <AuthProvider>
      <AppShell />
    </AuthProvider>
  );
}

export default App;
