'use client';

import { useEffect } from 'react';
import { onlineManager, useQueryClient } from '@tanstack/react-query';
import { onRecovered, startMonitor } from '@/lib/connectivity/monitor';
import { useConnectivity } from '@/lib/hooks/useConnectivity';

/**
 * `components/offline/ConnectivityProvider.tsx` — HO-XCT-002 §11.1, ADR-FE-142/145. Mounted in the
 * `(staff)` and `(offline)` layouts: starts the ping loop, makes TanStack Query's notion of
 * "online" the SERVER's answer (so queries pause instead of failing and polling stops while the
 * server is unreachable), makes mutations fail fast instead of pausing invisibly (ADR-FE-143), and
 * on recovery refetches everything (ADR-FE-145). It renders its children untouched.
 */
export function ConnectivityProvider({ children }: { children: React.ReactNode }) {
  const queryClient = useQueryClient();
  const { status } = useConnectivity();

  useEffect(() => startMonitor(), []);

  // ADR-FE-143: a mutation that cannot reach the server must fail at once (`client.ts` refuses it
  // with `OfflineError`), not sit paused and fire minutes later against a changed day.
  // Restored on unmount: leaving the staff routes must not leave the whole SPA with these changes.
  useEffect(() => {
    const defaults = queryClient.getDefaultOptions();
    queryClient.setDefaultOptions({
      ...defaults,
      mutations: { ...defaults.mutations, networkMode: 'always' },
    });
    return () => {
      queryClient.setDefaultOptions(defaults);
    };
  }, [queryClient]);

  useEffect(() => {
    onlineManager.setOnline(status !== 'offline');
  }, [status]);

  useEffect(
    () => () => {
      // Back to the browser's own idea of online, which TanStack follows when nobody overrides it.
      onlineManager.setOnline(typeof navigator === 'undefined' ? true : navigator.onLine);
    },
    [],
  );

  useEffect(
    () =>
      onRecovered(() => {
        onlineManager.setOnline(true);
        void queryClient.invalidateQueries();
      }),
    [queryClient],
  );

  return <>{children}</>;
}
