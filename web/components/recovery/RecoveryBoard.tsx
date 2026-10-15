'use client';

import Link from 'next/link';
import { CheckCircle2, HeartPulse, Phone, ShieldCheck } from 'lucide-react';
import { EmptyState } from '@/components/common/EmptyState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { CompactCatHeader } from '@/components/front-desk/CompactCatHeader';
import { RECOVERY_ORDER, recoveryLabel } from '@/lib/format/surgery';
import { timeOnlyInZone } from '@/lib/format/time';
import { cn } from '@/lib/utils';
import type {
  RecoveryBoard as Board,
  RecoveryBoardEntry,
  RecoveryStatus,
} from '@/lib/api/surgeries';

const ICONS: Record<RecoveryStatus, typeof HeartPulse> = {
  ReadyForDischarge: CheckCircle2,
  Recovering: HeartPulse,
  Stable: ShieldCheck,
};

/** Sorted *Ready for discharge* → *Recovering* → *Stable*, oldest status first within each (ADR-FE-073). */
export function sortRecovery(entries: RecoveryBoardEntry[]): RecoveryBoardEntry[] {
  const rank = (status: string) =>
    RECOVERY_ORDER.indexOf(status as (typeof RECOVERY_ORDER)[number]);
  return [...entries].sort(
    (a, b) =>
      rank(a.recoveryStatus) - rank(b.recoveryStatus) ||
      (a.statusChangedOn ?? '').localeCompare(b.statusChangedOn ?? ''),
  );
}

/**
 * ⭐ `RecoveryBoard` — HO-SRG-002 §11.8, ADR-FE-073, US-SRG-10. The front desk's Recovery tab: one card
 * per cat in recovery with the compact cat header, the owner (and a `tel:` link for when they ring), the
 * procedure, *"Ended 09:55"* and the status as TEXT with *"since 10:15 · set by Nurse Akter"*. The header
 * says when it was last updated; the data refreshes every 30 s (UI-5). Empty: *"No cats in recovery."*
 */
export function RecoveryBoard({
  board,
  zone,
  isLoading,
  isError,
  onRetry,
}: {
  board: Board | undefined;
  zone: string;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}) {
  if (isLoading)
    return <LoadingState variant="card" rows={3} label="Loading the recovery board…" />;
  if (isError && !board)
    return <ErrorState title="We couldn’t load the recovery board." onRetry={onRetry} />;
  const entries = sortRecovery(board?.entries ?? []);
  return (
    <section aria-labelledby="recovery-board-title" className="flex flex-col gap-3">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 id="recovery-board-title" className="font-display text-lg font-semibold text-ink">
          Recovery
        </h2>
        {board && (
          <p className="text-sm text-ink-muted" aria-live="off">
            Updated {timeOnlyInZone(board.generatedOn, zone)}
          </p>
        )}
      </div>
      {entries.length === 0 ? (
        <EmptyState title="No cats in recovery." />
      ) : (
        <ul className="grid gap-3 md:grid-cols-2">
          {entries.map((entry) => {
            const Icon = ICONS[entry.recoveryStatus];
            return (
              <li
                key={entry.surgeryId}
                className={cn(
                  'flex flex-col gap-2 rounded-[var(--radius-md)] border bg-surface p-3',
                  entry.recoveryStatus === 'ReadyForDischarge' ? 'border-accent' : 'border-border',
                )}
              >
                <CompactCatHeader name={entry.catName} isHighRisk={entry.catIsHighRisk} />
                <p className="text-sm text-ink">
                  {entry.procedure}
                  {entry.procedureEndedOn && (
                    <span className="text-ink-muted">
                      {' '}
                      · Ended {timeOnlyInZone(entry.procedureEndedOn, zone)}
                    </span>
                  )}
                </p>
                <p className="flex flex-wrap items-center gap-x-2 text-sm font-semibold text-ink">
                  <Icon size={16} aria-hidden="true" className="text-accent" />
                  {recoveryLabel(entry.recoveryStatus)}
                  {entry.statusChangedOn && (
                    <span className="font-normal text-ink-muted">
                      since {timeOnlyInZone(entry.statusChangedOn, zone)}
                      {entry.statusChangedByName ? ` · set by ${entry.statusChangedByName}` : ''}
                    </span>
                  )}
                </p>
                <p className="flex flex-wrap items-center gap-x-3 text-sm text-ink">
                  <span>{entry.customerName}</span>
                  {entry.customerPhone && (
                    <a
                      href={`tel:${entry.customerPhone}`}
                      className="inline-flex items-center gap-1 font-semibold text-accent-ink underline"
                    >
                      <Phone size={14} aria-hidden="true" />
                      {entry.customerPhone}
                    </a>
                  )}
                </p>
                <Link
                  href={`/staff/surgeries/${entry.surgeryId}#recovery`}
                  className="w-fit text-sm font-semibold text-accent-ink underline underline-offset-2"
                >
                  Open {entry.catName}’s surgery
                </Link>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
