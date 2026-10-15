import { ConnectivityProvider } from '@/components/offline/ConnectivityProvider';

// HO-XCT-002 §8.1, ADR-FE-144 — the offline group has NO session guard and fetches no server data:
// a guard would bounce to sign-in exactly when sign-in is unreachable (the same reason
// `/staff/sign-in` sits outside `(staff)`). It only runs the connectivity monitor, so the board
// notices when the server returns.
export default function OfflineLayout({ children }: { children: React.ReactNode }) {
  return (
    <ConnectivityProvider>
      <main id="main-content" className="mx-auto min-h-screen max-w-5xl bg-surface-page px-4 py-6 sm:px-6">
        {children}
      </main>
    </ConnectivityProvider>
  );
}
