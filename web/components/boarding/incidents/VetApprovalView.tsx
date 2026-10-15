'use client';

import { useState } from 'react';
import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { CatHeaderCompact } from '@/components/boarding/shared/CatHeaderCompact';
import { FormErrorAlert } from '@/components/boarding/shared/FormBits';
import { QueryGate } from '@/components/boarding/shared/QueryGate';
import { describeBoardingError } from '@/lib/api/errors';
import type { VetApprovalDetail } from '@/lib/api/types';
import type { ApprovalAnswer } from '@/lib/api/my-boarding';
import { formatMoney } from '@/lib/format/money';
import { formatBranchInstant } from '@/lib/format/boarding';
import { useMyApproval, useRespondToApproval } from '@/lib/hooks/useMyBoarding';
import { portalStayHref } from '@/lib/routing/boarding-links';

function Body({ approval, stayId }: { approval: VetApprovalDetail; stayId: string }) {
  const respond = useRespondToApproval(stayId, approval.id);
  const [asking, setAsking] = useState<ApprovalAnswer | null>(null);
  const [error, setError] = useState<string | null>(null);
  const zone = approval.branchTimeZoneId;
  const amount = formatMoney(approval.amount);

  return (
    <article className="flex flex-col gap-5" aria-labelledby="approval-heading">
      <h1 id="approval-heading" className="font-display text-2xl font-semibold text-ink">
        Treatment for {approval.catName}
      </h1>
      <CatHeaderCompact
        name={approval.catName}
        isHighRisk={approval.catIsHighRisk}
        handlingNote={approval.handlingNote}
      />
      <p className="text-[15px] text-ink">
        The vet would like to treat {approval.catName}: <strong>{approval.description}</strong>.
        Estimated cost <strong>{amount}</strong>.
      </p>
      {approval.limit && approval.used && approval.remaining ? (
        <p className="text-sm text-ink">
          Your limit for this stay is <strong>{formatMoney(approval.limit)}</strong>, of which{' '}
          <strong>{formatMoney(approval.used)}</strong> is used. Remaining:{' '}
          <strong>{formatMoney(approval.remaining)}</strong>.
        </p>
      ) : (
        <p className="text-sm text-ink-muted">
          Your spending limit for this stay is in the care profile you gave us; the clinic checks it
          before asking.
        </p>
      )}

      {approval.status === 'Pending' ? (
        <div className="flex flex-col gap-3">
          <FormErrorAlert message={error} />
          <div className="flex flex-wrap gap-3">
            <Button onClick={() => setAsking('Approve')}>Approve</Button>
            <Button variant="secondary" onClick={() => setAsking('Decline')}>
              Decline
            </Button>
          </div>
          <p className="text-xs text-ink-muted">
            Approving lets the clinic go ahead and add this cost to your stay. Declining means they
            will not.
          </p>
        </div>
      ) : (
        <p
          role="status"
          className="rounded-[var(--radius-sm)] bg-surface-sunken px-4 py-3 text-sm font-semibold text-ink"
        >
          You {approval.status === 'Approved' ? 'approved' : 'declined'} this
          {approval.decidedOn
            ? ` on ${formatBranchInstant(approval.decidedOn, zone)} (${zone})`
            : ''}
          .
        </p>
      )}

      <ConfirmDialog
        open={asking !== null}
        onOpenChange={(open) => !open && setAsking(null)}
        title={
          asking === 'Approve'
            ? `Approve ${amount} for ${approval.catName}?`
            : `Decline treatment for ${approval.catName}?`
        }
        consequence={
          asking === 'Approve'
            ? `The clinic can treat ${approval.catName} (${approval.description}) and ${amount} is added to your stay.`
            : `The clinic will not go ahead with “${approval.description}”. Nothing is added to your stay.`
        }
        confirmLabel={asking === 'Approve' ? 'Approve' : 'Decline'}
        variant={asking === 'Decline' ? 'destructive' : 'primary'}
        onConfirm={async () => {
          if (!asking) return;
          setError(null);
          try {
            await respond.mutateAsync({ answer: asking, note: null });
          } catch (e) {
            setError(describeBoardingError(e));
          }
          setAsking(null);
        }}
      />
      <Link
        href={portalStayHref(stayId)}
        className="text-sm font-semibold text-accent-ink underline"
      >
        ← Back to the stay
      </Link>
    </article>
  );
}

/** `components/boarding/incidents/VetApprovalView.tsx` — ⭐ HO-BRD-002 §11.3 `VetApprovalDetail`,
 *  US-BRD-14: amount, what for, the limit/used/remaining as text (OQ-4), **Approve** / **Decline**
 *  each behind a confirm, and afterwards the outcome and time (AC3). */
export function VetApprovalView({ stayId, approvalId }: { stayId: string; approvalId: string }) {
  const query = useMyApproval(stayId, approvalId);
  return (
    <QueryGate query={query} loadingLabel="Loading…" notFoundTitle="We can’t find that request.">
      {(approval) => <Body approval={approval} stayId={stayId} />}
    </QueryGate>
  );
}
