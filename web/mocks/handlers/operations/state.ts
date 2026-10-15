/**
 * `mocks/handlers/operations/state.ts` — HO-XCT-002 §12.3. The switches a test flips to put the
 * Cross-cutting mocks into the situation it needs, without a service worker or a second server
 * (§5.2): a failing ping, a named snapshot scenario, a named backup scenario, and the telemetry the
 * mock has received (E-B5 inspects it). Everything is module state; in a real browser it is also
 * reachable as `window.__xctMock`, because MSW's handlers run in the page, so Playwright can reach
 * it with `page.evaluate`. Nothing here ships to production: `mocks/**` is only imported when
 * `NEXT_PUBLIC_USE_MOCKS=true` (app/providers.tsx) or by Vitest.
 */

import { loadPersisted, savePersisted } from '../admin/fixtures';

const SNAPSHOT_STAFF_KEY = '__purrcare_xct_snapshot_staff__';

export type SnapshotScenario =
  | 'both'
  | 'appointmentsNotPermitted'
  | 'boardingNotAvailable'
  | 'expired'
  | 'otherPerson'
  | 'forbidden';

export type BackupScenario = 'healthy' | 'stale' | 'neverTested' | 'overdue' | 'dueLater';

export interface XctMockState {
  pingMode: 'ok' | 'fail' | 'serverError';
  snapshotScenario: SnapshotScenario;
  /** Bumped by `changeSnapshot()` so the ETag changes ("a status change"). */
  snapshotVersion: number;
  /** Who the snapshot says fetched it; null = the seeded Administrator. Set by the person-change e2e. */
  snapshotStaffAccountId: string | null;
  snapshotRequests: Array<{ ifNoneMatch: string | null; status: number }>;
  backupScenario: BackupScenario;
  retentionRunInProgress: boolean;
  erasedCustomerIds: Set<string>;
  telemetry: Array<{ contentType: string | null; hasCookie: boolean; hasCsrf: boolean; body: unknown }>;
}

export const xctMock: XctMockState = {
  pingMode: 'ok',
  snapshotScenario: 'both',
  snapshotVersion: 1,
  // Survives a full page load in the tab (the person-change e2e signs in as someone else, then reloads).
  snapshotStaffAccountId: loadPersisted<string>(SNAPSHOT_STAFF_KEY),
  snapshotRequests: [],
  backupScenario: 'healthy',
  retentionRunInProgress: false,
  erasedCustomerIds: new Set<string>(),
  telemetry: [],
};

export function resetXctMock(): void {
  xctMock.pingMode = 'ok';
  xctMock.snapshotScenario = 'both';
  xctMock.snapshotVersion = 1;
  xctMock.snapshotStaffAccountId = null;
  xctMock.snapshotRequests = [];
  xctMock.backupScenario = 'healthy';
  xctMock.retentionRunInProgress = false;
  xctMock.erasedCustomerIds = new Set<string>();
  xctMock.telemetry = [];
  restoreTestStore.length = 0;
  restoreTestStore.push(...seedRestoreTests());
}

export const restoreTestStore: Array<{
  id: string;
  performedOn: string;
  outcome: 'Passed' | 'Failed';
  achievedRpoMinutes: number;
  achievedRtoMinutes: number;
  environment: string;
  backupSetLabel: string;
  supersedesId: string | null;
  detail: Record<string, unknown>;
}> = [];

export function seedRestoreTests() {
  const base = {
    performedByStaffId: 's0000000-0000-0000-0000-000000000001',
    environment: 'restore-drill',
    notes: null,
    supersededByIds: [],
    checks: [
      { name: 'Row counts match', passed: true, detail: 'All schemas within tolerance.' },
      { name: 'Erasure journal reapplied', passed: true, detail: null },
    ],
  };
  return [
    {
      id: 'e1000000-0000-0000-0000-000000000002',
      performedOn: daysAgoDate(120),
      outcome: 'Failed' as const,
      achievedRpoMinutes: 22,
      achievedRtoMinutes: 310,
      environment: 'restore-drill',
      backupSetLabel: '20260615-020000F',
      supersedesId: null,
      detail: {
        ...base,
        id: 'e1000000-0000-0000-0000-000000000002',
        performedOn: daysAgoDate(120),
        backupSetLabel: '20260615-020000F',
        simulatedFailureAt: '2026-06-15T09:00:00+06:00',
        recoveredTo: '2026-06-15T08:38:00+06:00',
        restoreStartedAt: '2026-06-15T09:10:00+06:00',
        restoreCompletedAt: '2026-06-15T14:20:00+06:00',
        achievedRpoMinutes: 22,
        achievedRtoMinutes: 310,
        outcome: 'Failed',
        drillOutput: '{"result":"slow","notes":"<script>alert(1)</script> shown as text"}',
        supersedesId: null,
      },
    },
    {
      id: 'e1000000-0000-0000-0000-000000000001',
      performedOn: daysAgoDate(30),
      outcome: 'Passed' as const,
      achievedRpoMinutes: 9,
      achievedRtoMinutes: 140,
      environment: 'restore-drill',
      backupSetLabel: '20260915-020000F',
      supersedesId: null,
      detail: {
        ...base,
        id: 'e1000000-0000-0000-0000-000000000001',
        performedOn: daysAgoDate(30),
        backupSetLabel: '20260915-020000F',
        simulatedFailureAt: '2026-09-15T09:00:00+06:00',
        recoveredTo: '2026-09-15T08:51:00+06:00',
        restoreStartedAt: '2026-09-15T09:10:00+06:00',
        restoreCompletedAt: '2026-09-15T11:30:00+06:00',
        achievedRpoMinutes: 9,
        achievedRtoMinutes: 140,
        outcome: 'Passed',
        drillOutput: '{"result":"ok"}',
        supersedesId: null,
      },
    },
  ];
}

export function daysAgoDate(days: number): string {
  const d = new Date(Date.now() - days * 86_400_000);
  return d.toISOString().slice(0, 10);
}

restoreTestStore.push(...seedRestoreTests());

if (typeof window !== 'undefined') {
  (window as unknown as { __xctMock?: unknown }).__xctMock = {
    state: xctMock,
    reset: resetXctMock,
    setPing: (mode: XctMockState['pingMode']) => {
      xctMock.pingMode = mode;
    },
    setSnapshotScenario: (scenario: SnapshotScenario) => {
      xctMock.snapshotScenario = scenario;
    },
    setSnapshotStaff: (id: string | null) => {
      xctMock.snapshotStaffAccountId = id;
      savePersisted(SNAPSHOT_STAFF_KEY, id);
    },
    changeSnapshot: () => {
      xctMock.snapshotVersion += 1;
    },
    setBackupScenario: (scenario: BackupScenario) => {
      xctMock.backupScenario = scenario;
    },
    telemetry: () => xctMock.telemetry,
  };
}
