'use client';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { Toaster } from '@/components/ui/toaster';
import { apiFetch } from '@/lib/api/client';
import { registerQueryClient } from '@/lib/branch';

// The single QueryClient for the app (HO-CAT-002 §8.4 — server data lives in TanStack Query,
// never copied into useState). No global store of our own is created here or anywhere else.
//
// ARCH gate HIGH fix (HO-ADM-002-ARCHITECTURE, owner decision 2026-09-28) — `lib/branch.ts`'s
// `getActiveBranchId()` reads the active branch straight out of this instance's cache
// (`['staff', 'active-branch']`, §8.3) but is called synchronously from `apiFetch` and so cannot
// use `useQueryClient()` (not a hook). `registerQueryClient()` (below, `lib/branch.ts`) is called
// once this instance is the one actually committed, purely so that one non-React call site can
// reach it — see that module's own comment for why the registration flows this direction (`app/`
// calling into `lib/`) and not the reverse.
//
// The call is inside the `useEffect` below, not inside this `useState` initialiser — reproduced
// locally: Next's dev build runs with React Strict Mode (`next.config.ts`), which deliberately
// calls a lazy `useState` initialiser twice on mount and keeps only ONE call's result as the real
// state. A `registerQueryClient(client)` call made *inside* the initialiser runs for both calls,
// and the later of the two can end up registering the discarded client, permanently orphaning the
// one React actually uses — `getActiveBranchId()` would then read a client with nothing ever
// written to it. An effect body runs only against the already-settled `queryClient` value, so this
// registers exactly the instance passed to `QueryClientProvider` below.
//
// Also starts browser-side MSW (HO-CAT-002 §5.1) in development, only when
// NEXT_PUBLIC_USE_MOCKS=true (web/.env.local.example) — see web/README.md. Dynamically imported
// so `msw/browser` is never bundled into a production build. `worker.start()` is async and this
// effect fires after first paint, so a request made in the same tick as mount can race the
// worker's registration; that is a documented MSW/Next.js limitation, not a bug to fix here.
//
// HO-ACC-002 addition: `window.__mswReady` is set once `worker.start()` resolves. Most queries
// tolerate the race above because the default `retry: 1` (below) re-fires once the worker is
// ready. `useCaptcha()` deliberately sets `retry: false` (ADR-FE-006 — a failed fetch shows
// `CaptchaField`'s own "couldn't load" retry, not a silent auto-retry), which has no way to
// recover from losing this specific race on its own. `tests/e2e/fixtures.ts` waits on this flag
// (best-effort, and only meaningful in this mocked mode) before letting a test's own steps run.
export function Providers({ children }: { children: React.ReactNode }) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            staleTime: 30_000,
            retry: 1,
          },
        },
      }),
  );

  useEffect(() => {
    registerQueryClient(queryClient);
  }, [queryClient]);

  useEffect(() => {
    // ARCH gate HIGH 2 (first pass), tests/e2e/fixtures.ts — `NEXT_PUBLIC_*` variables are only
    // ever inlined into bundled app code by Next.js's own build/dev pipeline; a Playwright
    // `page.evaluate()` call is NOT bundled app code, so it has no way to read
    // `NEXT_PUBLIC_API_BASE_URL` itself. Exposed on `window` (mirroring `__mswReady` immediately
    // below) purely so the e2e fixture's own sign-in helper can build an absolute URL that
    // actually reaches the real API in a real-API run (`NEXT_PUBLIC_USE_MOCKS=false`), instead of
    // a relative path that silently resolves against the wrong (Next.js) origin there.
    //
    // ARCH gate LOW (second pass) — this used to be set unconditionally, including in a real
    // production build (the value itself is already public, `NEXT_PUBLIC_*`, so there was no
    // exposure, but it is still test plumbing with no reason to exist in app code a real customer
    // or staff member ever runs). Every e2e run — MSW or real-API — starts `next dev`
    // (`playwright.config.ts`'s own `webServer.command`), never a production build, so gating this
    // on `NODE_ENV !== 'production'` (the same test `__mswReady` already effectively gets, one line
    // down) keeps it out of production without adding a new dependency (`dotenv`) to read
    // `NEXT_PUBLIC_API_BASE_URL` from the Playwright process's own env instead.
    if (process.env.NODE_ENV !== 'production') {
      (window as unknown as { __apiBaseUrl?: string }).__apiBaseUrl =
        process.env.NEXT_PUBLIC_API_BASE_URL;

      // CODE_REVIEW suggestion (second pass) — `tests/e2e/staff-auth.spec.ts` used to wait out
      // `useStaffSession()`'s real 10s `staleTime` with a real `page.waitForTimeout(11_000)`
      // before dispatching a `visibilitychange`, purely so `refetchOnWindowFocus` would actually
      // refetch (it only does if the cached data is already stale). `invalidateQueries()` marks a
      // query stale AND refetches every active observer immediately, with no real wait needed —
      // gated the same way `__apiBaseUrl` is, for the same reason (test plumbing, not app
      // behaviour a real customer or staff member ever triggers).
      (
        window as unknown as {
          __invalidateQueries?: (queryKey: readonly unknown[]) => void;
        }
      ).__invalidateQueries = (queryKey) => {
        void queryClient.invalidateQueries({ queryKey });
      };

      // TEST gate (HO-ACC-002) — `tests/e2e/staff-auth.spec.ts`'s "locking while genuinely
      // offline" test needs a staff-audience call that reaches `client.ts`'s session-error
      // handler WITHOUT going through a `useQuery`/`useMutation` observer: those default to
      // `networkMode: 'online'`, which — correctly, matching production — refuses to even start
      // once `context.setOffline(true)`'s real `offline` event has flipped `onlineManager`
      // false, exactly the deadlock ARCHITECTURE gate fifth pass's `networkMode: 'always'` fix
      // targets on the re-sign-in mutation specifically. A raw `apiFetch()` call (mirroring
      // `idle-lock.test.tsx`'s own jsdom-level `apiFetch('/api/some-unrelated-staff-endpoint')`)
      // bypasses that gate entirely — it is a plain `fetch()` under the hood, still served by the
      // MSW service worker regardless of the browser's simulated offline state — the same way a
      // real idle-timeout reached via the local countdown timer (also network-independent) would.
      (
        window as unknown as {
          __apiFetch?: <T>(path: string, init?: Parameters<typeof apiFetch>[1]) => Promise<T>;
        }
      ).__apiFetch = apiFetch;
    }

    if (process.env.NODE_ENV === 'development' && process.env.NEXT_PUBLIC_USE_MOCKS === 'true') {
      void import('@/mocks/browser').then(({ worker }) =>
        worker.start({ onUnhandledRequest: 'bypass' }).then(() => {
          (window as unknown as { __mswReady?: boolean }).__mswReady = true;
        }),
      );
    }
    // `queryClient` in the deps array below is stable (its own `useState` initialiser only ever
    // runs once), so listing it does not make this effect ever re-run; it is here only because it
    // is now genuinely referenced in the body (the new `__invalidateQueries` hook), which the
    // Rules-of-Hooks linter would otherwise (correctly) flag as a stale-closure risk.
  }, [queryClient]);

  return (
    <QueryClientProvider client={queryClient}>
      {children}
      <Toaster />
    </QueryClientProvider>
  );
}
