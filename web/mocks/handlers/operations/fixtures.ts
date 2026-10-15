import type { OfflineSnapshotBody } from '@/lib/api/offline';
import type {
  ErasureRequest,
  RetentionRun,
  RetentionRunSummary,
  RetentionSchedule,
} from '@/lib/api/retention';
import type { BackupStatus, WebVitalsSummary } from '@/lib/api/operations';
import { accountsFixtureIds } from '../areas/accounts';
import { administrationFixtureIds } from '../admin/fixtures';
import type { BackupScenario, SnapshotScenario } from './state';

/** `mocks/handlers/operations/fixtures.ts` — HO-XCT-002 §12.3. Generated from HO-XCT-001 section
 *  12.4 (and the shapes the merged API really returns); a fixture never carries a field the
 *  contract does not specify. */

export const xctFixtureIds = {
  staffAccountId: accountsFixtureIds.adminStaffId,
  otherStaffAccountId: accountsFixtureIds.nurseStaffId,
  branchId: administrationFixtureIds.mainBranchId,
  erasedCustomerId: 'c0000000-0000-0000-0000-0000000e0001',
  mergedCustomerId: 'c0000000-0000-0000-0000-0000000e0002',
  failingCustomerId: 'c0000000-0000-0000-0000-0000000e0003',
  completedCustomerId: 'c0000000-0000-0000-0000-0000000e0004',
  openRunId: 'f0000000-0000-0000-0000-000000000001',
  errorRunId: 'f0000000-0000-0000-0000-000000000002',
} as const;

function isoPlusHours(hours: number): string {
  return new Date(Date.now() + hours * 3_600_000).toISOString();
}

function todayLocalDate(): string {
  return new Date().toISOString().slice(0, 10);
}

export function buildSnapshot(
  scenario: SnapshotScenario,
  branchId: string,
  version: number,
): OfflineSnapshotBody {
  const day = todayLocalDate();
  const at = (hh: string) => `${day}T${hh}:00+06:00`;
  const appointments = {
    items: [
      {
        appointmentId: 'a0000000-0000-0000-0000-000000000002',
        startsOn: at('11:30'),
        endsOn: at('12:00'),
        status: version > 1 ? 'InConsultation' : 'CheckedIn',
        isWalkIn: false,
        triagePriority: null,
        catName: 'Mitu',
        catIsHighRisk: true,
        handlingNote: 'Bites when restrained. Use a towel wrap.',
        customerName: 'Farhana Akter',
        customerPhone: '+8801711000222',
        serviceName: 'Vaccination',
        performerName: 'Dr Rahman',
        roomName: 'Consult 1',
      },
      {
        appointmentId: 'a0000000-0000-0000-0000-000000000001',
        startsOn: at('09:00'),
        endsOn: at('09:30'),
        status: 'Booked',
        isWalkIn: false,
        triagePriority: null,
        catName: 'Luna',
        catIsHighRisk: false,
        handlingNote: null,
        customerName: 'Imran Hossain',
        customerPhone: '+8801711000111',
        serviceName: 'Consultation',
        performerName: 'Dr Rahman',
        roomName: 'Consult 2',
      },
    ],
  };
  const boardingTasks = {
    items: [
      {
        taskId: 'b0000000-0000-0000-0000-0000000000b2',
        dueOn: at('18:00'),
        taskType: 'Medication',
        taskLabel: 'Evening medication',
        unitName: 'Suite 3',
        catName: 'Pixel',
        catIsHighRisk: false,
        handlingNote: null,
        instructions: 'Half a tablet with food',
        status: 'Open',
        completedOn: null,
        assigneeName: 'Nasrin',
      },
      {
        taskId: 'b0000000-0000-0000-0000-0000000000b1',
        dueOn: at('08:00'),
        taskType: 'Feeding',
        taskLabel: 'Morning feed',
        unitName: 'Suite 3',
        catName: 'Pixel',
        catIsHighRisk: false,
        handlingNote: null,
        instructions: null,
        status: 'Completed',
        completedOn: at('08:10'),
        assigneeName: 'Nasrin',
      },
    ],
  };

  const omitted: OfflineSnapshotBody['omittedSections'] = [];
  const sections: OfflineSnapshotBody['sections'] = {};
  if (scenario === 'appointmentsNotPermitted') {
    omitted.push({ section: 'appointments', reason: 'NotPermitted' });
  } else {
    sections.appointments = appointments;
  }
  if (scenario === 'boardingNotAvailable') {
    omitted.push({ section: 'boardingTasks', reason: 'NotAvailable' });
  } else {
    sections.boardingTasks = boardingTasks;
  }

  const expired = scenario === 'expired';
  return {
    schemaVersion: 1,
    branch: { id: branchId, name: 'Main clinic', timeZoneId: 'Asia/Dhaka' },
    localDate: day,
    generatedOn: new Date().toISOString(),
    validUntil: expired ? isoPlusHours(-3) : isoPlusHours(14),
    staffAccountId:
      scenario === 'otherPerson' ? xctFixtureIds.otherStaffAccountId : xctFixtureIds.staffAccountId,
    idleTimeoutMinutes: 30,
    refreshAfterSeconds: 60,
    sections,
    omittedSections: omitted,
  };
}

export function snapshotEtag(scenario: SnapshotScenario, version: number): string {
  return `"xct-${scenario}-v${version}"`;
}

const CLASS_ROWS: Array<
  [string, number | null, 'years' | 'months' | 'days' | null, string, number, string]
> = [
  ['Clinical', 7, 'years', 'ReviewOnly', 41, 'Statutory clinical-record retention'],
  ['Financial', 10, 'years', 'ReviewOnly', 18, 'Statutory financial-record retention'],
  ['Regulatory', 10, 'years', 'ReviewOnly', 6, 'Controlled-drug register'],
  ['Audit', null, null, 'ReviewOnly', 5, 'Audit trail'],
  ['CustomerPersonal', null, null, 'AnonymiseOnRequest', 12, 'Erased on request'],
  ['CurrentState', null, null, 'Keep', 30, 'Current state'],
  ['Operational', null, null, 'Keep', 22, 'Operational data'],
  ['Notification', 6, 'months', 'Redact', 3, 'Message contents'],
  ['Security', 30, 'days', 'Purge', 4, 'Sessions and reset tokens'],
  ['Media', 12, 'months', 'Purge', 2, 'Boarding media'],
  ['Telemetry', 90, 'days', 'Purge', 2, 'Web vitals'],
];

export function buildSchedule(): RetentionSchedule {
  return {
    classes: CLASS_ROWS.map(([recordClass, period, unit, action, tableCount, basis]) => ({
      recordClass,
      hardDeletePolicies: ['Never'],
      bases: [basis],
      effectivePeriod: period,
      periodUnit: unit,
      actionAtEnd: action as RetentionSchedule['classes'][number]['actionAtEnd'],
      tableCount,
      tables:
        recordClass === 'Audit'
          ? [
              { schema: 'audit', table: 'audit_entry', hardDeletePolicy: 'Never', basis },
              { schema: 'audit', table: 'audit_entry_detail', hardDeletePolicy: 'Never', basis },
            ]
          : [],
    })),
  };
}

export const retentionReviewWithRows = {
  asOf: todayLocalDate(),
  rows: [
    {
      recordClass: 'Clinical',
      recordKind: 'Closed visit notes',
      count: 14,
      oldestAnchor: '2019-02-11',
      sourceKey: 'emr.visit',
    },
    {
      recordClass: 'Financial',
      recordKind: 'Settled invoices',
      count: 3,
      oldestAnchor: '2015-06-30',
      sourceKey: 'billing.invoice',
    },
  ],
  note: 'Eligible for review. Nothing is deleted automatically.',
};

export const retentionReviewEmpty = {
  asOf: todayLocalDate(),
  rows: [],
  note: 'Eligible for review. Nothing is deleted automatically.',
};

export const runSummaries: RetentionRunSummary[] = [
  {
    retentionRunId: xctFixtureIds.openRunId,
    trigger: 'Scheduled',
    status: 'Succeeded',
    requestedOn: isoPlusHours(-20),
    startedOn: isoPlusHours(-20),
    finishedOn: isoPlusHours(-19.9),
    passCount: 8,
    totalAffected: 12,
    totalFailed: 0,
  },
  {
    retentionRunId: xctFixtureIds.errorRunId,
    trigger: 'Manual',
    status: 'CompletedWithErrors',
    requestedOn: isoPlusHours(-44),
    startedOn: isoPlusHours(-44),
    finishedOn: isoPlusHours(-43.9),
    passCount: 8,
    totalAffected: 5,
    totalFailed: 2,
  },
];

export function buildRun(id: string): RetentionRun | null {
  const summary = runSummaries.find((r) => r.retentionRunId === id);
  if (!summary) return null;
  const failing = summary.status === 'CompletedWithErrors';
  return {
    retentionRunId: summary.retentionRunId,
    trigger: summary.trigger,
    status: summary.status,
    requestedOn: summary.requestedOn,
    startedOn: summary.startedOn,
    finishedOn: summary.finishedOn,
    passes: [
      {
        kind: 'Erasure',
        cutoff: null,
        examined: 4,
        affected: failing ? 1 : 2,
        held: 1,
        failed: failing ? 2 : 0,
        errorCodes: failing ? ['ErasureScopeFailing', 'PurgeSourceNotRegistered'] : [],
        durationMs: 840,
      },
      {
        kind: 'PurgeSessions',
        cutoff: isoPlusHours(-24 * 30),
        examined: 60,
        affected: 60,
        held: 0,
        failed: 0,
        errorCodes: [],
        durationMs: 120,
      },
      {
        kind: 'ReviewCounts',
        cutoff: null,
        examined: 59,
        affected: 0,
        held: 0,
        failed: 0,
        errorCodes: [],
        durationMs: 60,
      },
    ],
  };
}

export const erasureRequests: ErasureRequest[] = [
  {
    customerAccountId: xctFixtureIds.erasedCustomerId,
    requestedOn: isoPlusHours(-72),
    source: 'StaffOnBehalf',
    status: 'Open',
    completedOn: null,
    scopes: [
      {
        contextKey: 'Accounts',
        status: 'Completed',
        heldUntil: null,
        basis: null,
        attempts: 1,
        lastErrorCode: null,
        completedOn: isoPlusHours(-72),
      },
      {
        contextKey: 'Billing',
        status: 'Held',
        heldUntil: '2032-03-12',
        basis: 'NFR-LEG-5 acceptance evidence',
        attempts: 1,
        lastErrorCode: null,
        completedOn: null,
      },
      {
        contextKey: 'Clinical',
        status: 'Completed',
        heldUntil: null,
        basis: null,
        attempts: 1,
        lastErrorCode: null,
        completedOn: isoPlusHours(-72),
      },
    ],
  },
  {
    customerAccountId: xctFixtureIds.completedCustomerId,
    requestedOn: isoPlusHours(-24 * 40),
    source: 'SelfService',
    status: 'Completed',
    completedOn: isoPlusHours(-24 * 39),
    scopes: [
      {
        contextKey: 'Accounts',
        status: 'Completed',
        heldUntil: null,
        basis: null,
        attempts: 1,
        lastErrorCode: null,
        completedOn: isoPlusHours(-24 * 39),
      },
    ],
  },
  {
    customerAccountId: xctFixtureIds.failingCustomerId,
    requestedOn: isoPlusHours(-30),
    source: 'RestoreReconciliation',
    status: 'Open',
    completedOn: null,
    scopes: [
      {
        contextKey: 'Notifications',
        status: 'Failing',
        heldUntil: null,
        basis: null,
        attempts: 4,
        lastErrorCode: 'ErasureScopeFailing',
        completedOn: null,
      },
    ],
  },
];

export function buildBackupStatus(scenario: BackupScenario): BackupStatus {
  const hoursAgo = (h: number) => new Date(Date.now() - h * 3_600_000).toISOString();
  const stale = scenario === 'stale' || scenario === 'overdue' || scenario === 'neverTested';
  const run = (h: number, label: string) => ({
    finishedOn: hoursAgo(h),
    succeeded: true,
    repository: 'repo1',
    label,
    sizeBytes: 1_500_000_000,
  });
  const date = (daysFromNow: number) =>
    new Date(Date.now() + daysFromNow * 86_400_000).toISOString().slice(0, 10);
  const neverTested = scenario === 'neverTested';
  const overdue = scenario === 'overdue' || neverTested;
  return {
    lastFull: run(stale ? 51 : 6, '20261012-020000F'),
    lastDiff: run(stale ? 27 : 3, '20261013-020000D'),
    lastArchiveCheck: run(stale ? 30 : 4, 'archive-check'),
    lastErasureJournal: run(0.1, 'erasure-journal'),
    walArchivedUntil: hoursAgo(stale ? 27 : 0.05),
    isBackupStale: stale,
    isArchiveStale: stale,
    lastRestoreTest: neverTested
      ? null
      : {
          id: 'e1000000-0000-0000-0000-000000000001',
          performedOn: date(overdue ? -120 : -30),
          outcome: 'Passed',
          achievedRpoMinutes: 9,
          achievedRtoMinutes: 140,
        },
    lastPassedRestoreTestOn: neverTested ? null : date(overdue ? -120 : -30),
    nextRestoreTestDueOn: neverTested ? date(-5) : overdue ? date(-30) : date(scenario === 'dueLater' ? 40 : 60),
    isRestoreTestOverdue: overdue,
    objectives: { rpoMinutes: 15, rtoMinutes: 240 },
  };
}

export function buildWebVitals(from: string, to: string): WebVitalsSummary {
  return {
    from,
    to,
    budgets: { lcpMs: 2500, inpMs: 200, cls: 0.1 },
    rows: [
      { routeTemplate: '/', viewportClass: 'Narrow', metric: 'LCP', sampleCount: 412, p75: 2210, verdict: 'Pass' },
      { routeTemplate: '/shop', viewportClass: 'Narrow', metric: 'LCP', sampleCount: 380, p75: 2940, verdict: 'Fail' },
      { routeTemplate: '/shop/[slug]', viewportClass: 'Narrow', metric: 'LCP', sampleCount: 12, p75: 1800, verdict: 'InsufficientData' },
      { routeTemplate: '/', viewportClass: 'Narrow', metric: 'INP', sampleCount: 300, p75: 140, verdict: 'Pass' },
      { routeTemplate: '/', viewportClass: 'Narrow', metric: 'CLS', sampleCount: 300, p75: 0.04, verdict: 'Pass' },
    ],
  };
}

/** A drill JSON the paste parser must accept — the output shape of `restore-drill.sh`. */
export const VALID_DRILL_JSON = JSON.stringify(
  {
    performedOn: '2026-10-10',
    environment: 'restore-drill',
    backupSetLabel: '20261010-020000F',
    simulatedFailureAt: '2026-10-10T09:00:00+06:00',
    recoveredTo: '2026-10-10T08:50:00+06:00',
    restoreStartedAt: '2026-10-10T09:10:00+06:00',
    restoreCompletedAt: '2026-10-10T11:20:00+06:00',
    checks: [
      { name: 'Row counts match', passed: true, detail: 'ok' },
      { name: 'Erasure journal reapplied', passed: true },
    ],
  },
  null,
  2,
);

export const MALFORMED_DRILL_JSON = '{"performedOn": "2026-10-10", "checks": [';
