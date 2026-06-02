import { AuthProvider } from './auth/AuthContext.tsx';
import { DevUserSwitcher } from './auth/DevUserSwitcher.tsx';

/**
 * Vite exposes import.meta.env.PROD as a boolean — true only for production builds.
 * DevUserSwitcher is excluded from production bundles entirely via this check.
 */
const IS_PROD = import.meta.env.PROD;

function AppShell() {
  return (
    <div className="min-h-screen bg-slate-900 text-white flex flex-col items-center justify-center">
      <header className="text-center px-4">
        <h1 className="text-4xl font-bold tracking-tight mb-2">
          Trivium World Cup 2026
        </h1>
        <p className="text-slate-400 text-lg">
          Prediction pool — coming soon
        </p>
      </header>

      {/* Dev user switcher — excluded in production builds */}
      {!IS_PROD && <DevUserSwitcher />}
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
