'use client';

import { useCallback, useSyncExternalStore } from 'react';
import {
  forceCheck,
  getSnapshot,
  subscribe,
  type ConnectivityOutcome,
  type ConnectivityState,
  type ConnectivityStatus,
} from '@/lib/connectivity/monitor';

/**
 * `lib/hooks/useConnectivity.ts` — HO-XCT-002 §11.1/§11.3, ADR-FE-142/143. The one hook every
 * screen uses to learn whether the server can be reached. A device fact read through
 * `useSyncExternalStore`, not server data (so no TanStack query) and not an app store.
 */
export interface Connectivity {
  status: ConnectivityStatus;
  lastOnlineAt: number | null;
  recoveredAt: number | null;
  isOffline: boolean;
  isDegraded: boolean;
  forceCheck: () => Promise<ConnectivityOutcome>;
}

const SERVER_STATE: ConnectivityState = { status: 'online', lastOnlineAt: null, recoveredAt: null };

export function useConnectivity(): Connectivity {
  const state = useSyncExternalStore(subscribe, getSnapshot, () => SERVER_STATE);
  const check = useCallback(() => forceCheck(), []);
  return {
    status: state.status,
    lastOnlineAt: state.lastOnlineAt,
    recoveredAt: state.recoveredAt,
    isOffline: state.status === 'offline',
    isDegraded: state.status === 'degraded',
    forceCheck: check,
  };
}

/** `!isOffline`. Shared submit and confirm controls consume it: disabled, with the visible reason
 *  "Offline — can't save" and `aria-describedby` pointing at it (ADR-FE-143). */
export function useWriteEnabled(): boolean {
  return !useConnectivity().isOffline;
}

export const OFFLINE_CANT_SAVE_LABEL = "Offline — can't save";
