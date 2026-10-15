import type { RetentionActionAtEnd, RetentionRunStatus } from '@/lib/api/retention';

/**
 * `lib/retention/labels.ts` — HO-XCT-002 §11.5. Plain-language wording for the retention and
 * erasure screens. Codes from the API are looked up here and never shown raw (NFR-USA-4); an
 * unknown code gets a generic gloss rather than the code itself.
 */

export const RECORD_CLASS_INFO: Record<string, { title: string; covers: string }> = {
  Clinical: { title: 'Clinical records', covers: 'Visits, notes, diagnoses, prescriptions and results.' },
  Financial: { title: 'Financial records', covers: 'Invoices, payments, refunds and receipts.' },
  Regulatory: { title: 'Regulatory records', covers: 'Controlled-drug register and similar statutory registers.' },
  Audit: { title: 'Audit trail', covers: 'Who did what, and when, across the system.' },
  CustomerPersonal: { title: 'Customer personal data', covers: 'Name, phone, email, addresses and marketing consent.' },
  CurrentState: { title: 'Current-state records', covers: 'Settings and catalogues that always show their present value.' },
  Operational: { title: 'Operational records', covers: 'Day-to-day working data such as rosters and bookings.' },
  Notification: { title: 'Message contents', covers: 'The text of emails and messages already sent.' },
  Security: { title: 'Security artefacts', covers: 'Sessions and password-reset tokens.' },
  Media: { title: 'Boarding media', covers: 'Photos and clips shared with owners during a stay.' },
  Telemetry: { title: 'Performance measurements', covers: 'Anonymous web-vitals samples.' },
};

export function recordClassInfo(recordClass: string): { title: string; covers: string } {
  return RECORD_CLASS_INFO[recordClass] ?? { title: recordClass, covers: '' };
}

export const ACTION_LABEL: Record<RetentionActionAtEnd, string> = {
  ReviewOnly: 'Review only',
  Purge: 'Purge',
  Redact: 'Redact',
  AnonymiseOnRequest: 'Anonymise on request',
  Keep: 'Kept',
};

export const ACTION_EXPLANATION: Record<RetentionActionAtEnd, string> = {
  ReviewOnly: 'Counted for review at the end of the period. Nothing is deleted automatically.',
  Purge: 'Removed automatically once the period has passed.',
  Redact: 'The text is blanked once the period has passed; the fact it was sent is kept.',
  AnonymiseOnRequest: 'Anonymised when the customer asks, except what the law says we must keep.',
  Keep: 'No retention period; kept while it is in use.',
};

export function formatPeriod(period: number | null, unit: string | null): string {
  if (period === null || unit === null) return 'No fixed period';
  const singular = unit.endsWith('s') ? unit.slice(0, -1) : unit;
  return `${period} ${period === 1 ? singular : unit}`;
}

export const RUN_STATUS_LABEL: Record<RetentionRunStatus, string> = {
  Requested: 'Waiting to start',
  Running: 'Running',
  Succeeded: 'Succeeded',
  CompletedWithErrors: 'Finished with problems',
};

export const PASS_KIND_LABEL: Record<string, string> = {
  Erasure: 'Customer erasure requests',
  PurgeSessions: 'Expired sign-in sessions',
  PurgeResetTokens: 'Expired password-reset tokens',
  PurgeWebVitals: 'Old performance measurements',
  PurgeBackupRuns: 'Old backup job logs',
  PurgeSources: 'Other purgeable sources',
  RedactNotifications: 'Old message contents',
  ReviewCounts: 'Records eligible for review',
};

export function passKindLabel(kind: string): string {
  return PASS_KIND_LABEL[kind] ?? 'Another retention step';
}

const ERROR_GLOSS: Record<string, string> = {
  ErasureScopeFailing:
    'One part of a customer erasure could not finish and will be tried again at the next run.',
  PurgeSourceNotRegistered:
    'A data source was not set up to be purged, so it was skipped. Nothing was deleted from it.',
  RecordIsRetained: 'A record was kept because the law requires it. This is expected, not a fault.',
};

export function glossErrorCode(code: string): string {
  return (
    ERROR_GLOSS[code] ??
    'This step reported a problem. The record was left as it was and will be tried again.'
  );
}

export const SCOPE_LABEL: Record<string, string> = {
  Accounts: 'Account and sign-in details',
  Billing: 'Billing records',
  Clinical: 'Clinical records',
  Notifications: 'Messages',
  Ordering: 'Orders and delivery',
  Files: 'Uploaded files',
  Administration: 'Policy acceptances',
};

export function scopeLabel(contextKey: string): string {
  return SCOPE_LABEL[contextKey] ?? `${contextKey} records`;
}

export const SOURCE_LABEL: Record<string, string> = {
  SelfService: 'Requested by the customer',
  StaffOnBehalf: 'Taken by staff',
  RestoreReconciliation: 'Re-applied after a restore',
};

export const ERASURE_CHANNEL_LABEL = {
  InPerson: 'In person',
  Phone: 'By phone',
  Email: 'By email',
  Letter: 'By letter',
} as const;

/** How identity was checked: four fixed choices, never free text (HO-XCT-001 2026-10-12 fix). */
export const ERASURE_IDENTITY_CHECK_LABEL = {
  RegisteredMobileCallback: 'Called back on the registered mobile number',
  RegisteredEmailReply: 'Reply received from the registered email address',
  InPersonPhotoId: 'Photo ID checked in person',
  WrittenRequestSigned: 'Signed written request',
} as const;
