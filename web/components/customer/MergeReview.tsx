'use client';

import { useState } from 'react';
import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { useDialogReturnFocus } from '@/components/ui/dialog';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import { NotYetRecordedNotice } from './NotYetRecordedNotice';
import { MergeConfirmDialog } from './MergeConfirmDialog';
import { ApiValidationError } from '@/lib/api/client';
import type {
  StaffCustomer,
  MergeCustomerRequest,
  MergeCustomerResult,
} from '@/lib/api/staff-customers';

interface MergeResult {
  survivorId: string;
  isReapplication: boolean;
}

/**
 * `components/customer/MergeReview.tsx` — ⭐ US-ACC-11, ADR-FE-011. A FRESH read of every record
 * on arrival (via `GET /api/staff/customers/{id}`, the same query the detail screen uses — never
 * the duplicates-report row). No survivor pre-selected, ever. One `POST .../merge` per
 * non-survivor member — for the common two-member group (the case the match rule produces almost
 * always) this is literally the one request §11.13 describes; a larger group runs the same
 * request once per remaining member in sequence, each independently re-runnable on a failure
 * (below). The server moves the cats; this component never calls a cat endpoint (ADR-FE-011(b)).
 *
 * CR MEDIUM 2 (first-pass gate fix round, item 13) — this fresh single-customer read (Q-H1) used to
 * report only `catCount`, not a per-cat list, unlike the duplicates report (Q-I1) — a §20.7 contract
 * gap against §11.13's "lists every cat", closed by extending `StaffCustomerViewModel` itself
 * (`cats`, alongside the unchanged `catCount`) rather than falling back to the report's own cat list
 * (still forbidden — ADR-FE-011(c) — the report row is exactly what a fresh read exists to not
 * trust). `MergeConfirmDialog` renders the actual names from this fresh read.
 *
 * ARCH gate HIGH 3 (first pass) — takes `customerIds`/`isLoading`/`isError`/`customers`/`onMerge`/
 * `isMergePending` as props; the page (`app/(staff)/staff/customers/duplicates/review/page.tsx`)
 * wires `useStaffCustomersByIds(customerIds)`/`useMergeCustomer()`. The confirmation dialog itself
 * is `MergeConfirmDialog` (ARCH LOW "component size" — split out once this file's own hooks moved
 * to the page still left it over §11's ~150-line guideline).
 */
export interface MergeReviewProps {
  customerIds: string[];
  isLoading: boolean;
  isError: boolean;
  customers: StaffCustomer[];
  onMerge: (
    duplicateCustomerId: string,
    request: MergeCustomerRequest,
  ) => Promise<MergeCustomerResult>;
  isMergePending: boolean;
}

export function MergeReview({
  customerIds,
  isLoading,
  isError,
  customers,
  onMerge,
  isMergePending,
}: MergeReviewProps) {
  const [survivorId, setSurvivorId] = useState<string | null>(null);
  const [reason, setReason] = useState('');
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [remainingDuplicateIds, setRemainingDuplicateIds] = useState<string[] | null>(null);
  const [result, setResult] = useState<MergeResult | null>(null);
  const [partialFailure, setPartialFailure] = useState(false);
  const [conflictMessage, setConflictMessage] = useState<string | null>(null);
  const returnFocusRef = useDialogReturnFocus(confirmOpen);

  if (isLoading) return <LoadingState variant="card" rows={6} label="Loading records…" />;
  // ARCHITECTURE gate third pass, HIGH — see the matching comment on
  // staff-accounts/[staffAccountId]/page.tsx: render cached data when present rather than swap this
  // whole screen out for an error screen on `isError` alone. `customers` (the page's own prop, built
  // by filtering out every query whose `.data` is falsy) already IS the "is there cached data"
  // signal for each individual member of the group — `customers.length !== customerIds.length` is
  // true exactly when at least one member genuinely has nothing to show, so requiring BOTH that AND
  // `isError` (rather than either alone) is enough: an `isError` on a query that still has its own
  // stale `.data` no longer blanks a currently-visible, in-progress merge review that would
  // otherwise lose the survivor selection, the reason and any already-applied partial-merge state.
  if (isError && customers.length !== customerIds.length) {
    return <ErrorState description="We couldn't load one or more of these records just now." />;
  }

  if (result) {
    return (
      <div role="status" aria-live="polite" className="flex flex-col gap-3">
        <p className="text-sm font-semibold text-ink">
          {result.isReapplication
            ? 'This merge had already been applied; any remaining cats have now been moved.'
            : 'The records have been merged.'}
        </p>
        <Link
          href={`/staff/customers/${result.survivorId}`}
          className="font-semibold text-accent-ink no-underline hover:underline"
        >
          View the surviving record
        </Link>
      </div>
    );
  }

  if (conflictMessage) {
    return (
      <div className="flex flex-col gap-3">
        <p role="alert" className="text-sm text-danger-ink">
          {conflictMessage}
        </p>
        <Link
          href="/staff/customers/duplicates"
          className="font-semibold text-accent-ink no-underline hover:underline"
        >
          Back to duplicates
        </Link>
      </div>
    );
  }

  const survivor = customers.find((c) => c.id === survivorId);
  const others = customers.filter((c) => c.id !== survivorId);

  async function runMerge(duplicateIds: string[]) {
    setPartialFailure(false);
    let lastResult: MergeResult | null = null;

    for (let index = 0; index < duplicateIds.length; index += 1) {
      const duplicateId = duplicateIds[index]!;
      try {
        const response = await onMerge(duplicateId, { survivingCustomerId: survivorId!, reason });
        lastResult = {
          survivorId: response.survivingCustomerId,
          isReapplication: response.isReapplication,
        };
      } catch (error) {
        if (error instanceof ApiValidationError) {
          const code = error.response.errors[0]?.errorCode;
          if (code === 'CustomersNotDuplicates' || code === 'CustomerAlreadyMerged') {
            setConflictMessage(
              error.response.errors[0]?.errorMessage ?? 'These records cannot be merged.',
            );
            return;
          }
        }
        setPartialFailure(true);
        setRemainingDuplicateIds(duplicateIds.slice(index));
        return;
      }
    }

    setRemainingDuplicateIds(null);
    if (lastResult) setResult(lastResult);
  }

  return (
    <div className="flex max-w-2xl flex-col gap-5">
      {partialFailure && (
        <div
          role="alert"
          className="flex flex-col gap-2 rounded-[var(--radius-md)] border border-danger-ink/30 bg-danger-bg p-4 text-sm text-danger-ink"
        >
          <p>The merge may be partly complete. Run it again with the same choice to finish it.</p>
          <Button
            variant="destructive"
            size="sm"
            className="w-fit"
            onClick={() => runMerge(remainingDuplicateIds ?? [])}
          >
            Run again
          </Button>
        </div>
      )}

      <fieldset className="flex flex-col gap-2">
        <legend className="text-sm font-semibold text-ink">Keep which record?</legend>
        {customers.map((customer) => (
          <label key={customer.id} className="flex items-center gap-2 text-sm text-ink">
            <input
              type="radio"
              name="survivor"
              checked={survivorId === customer.id}
              onChange={() => setSurvivorId(customer.id)}
            />
            {customer.fullName} ({customer.mobileNumber ?? customer.emailAddress ?? 'no identifier'}
            )
          </label>
        ))}
      </fieldset>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="reason" className="text-sm font-semibold text-ink">
          Reason{' '}
          <span aria-hidden="true" className="text-danger-ink">
            *
          </span>
        </label>
        <textarea
          id="reason"
          required
          value={reason}
          onChange={(event) => setReason(event.target.value)}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        />
      </div>

      <NotYetRecordedNotice />

      <Button
        disabled={!survivorId || !reason.trim() || isMergePending}
        className="w-fit"
        onClick={() => setConfirmOpen(true)}
      >
        Review merge
      </Button>

      <MergeConfirmDialog
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        returnFocusRef={returnFocusRef}
        survivor={survivor}
        others={others}
        isPending={isMergePending}
        onCancel={() => setConfirmOpen(false)}
        onConfirm={async () => {
          setConfirmOpen(false);
          await runMerge(others.map((o) => o.id));
        }}
      />
    </div>
  );
}
