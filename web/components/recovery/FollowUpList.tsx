'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { ReasonDialog } from '@/components/inventory/ReasonDialog';
import { Panel } from '@/components/inventory/fields';
import { toast } from '@/components/ui/use-toast';
import { describeSurgeryError } from '@/lib/api/errors';
import { formatDateOnly } from '@/lib/format/time';
import { followUpLabel } from '@/lib/format/surgery';
import { useCancelFollowUp, useCompleteFollowUp } from '@/lib/hooks/useSurgeries';
import type { SurgeryDetail, SurgeryFollowUp } from '@/lib/api/surgeries';
import { errorClass } from '@/components/surgery/styles';

/**
 * `FollowUpList` — HO-SRG-002 §11.6. After discharge: each follow-up with its due date and status (text),
 * and — for nurses and vets, while the server's `allowedTransitions` has `CloseFollowUp` — **Mark done**
 * and **Cancel** (a reason is required).
 */
export function FollowUpList({ surgery, canClose }: { surgery: SurgeryDetail; canClose: boolean }) {
  const complete = useCompleteFollowUp(surgery.id);
  const cancel = useCancelFollowUp(surgery.id);
  const [cancelling, setCancelling] = useState<SurgeryFollowUp | null>(null);
  const [error, setError] = useState<string | null>(null);
  const mayClose = canClose && surgery.allowedTransitions.includes('CloseFollowUp');

  return (
    <Panel title="Follow-ups" headingId="srg-followups">
      {surgery.followUps.length === 0 ? (
        <p className="text-sm text-ink-muted">No follow-ups were scheduled.</p>
      ) : (
        <ul className="flex flex-col divide-y divide-border">
          {surgery.followUps.map((followUp) => (
            <li
              key={followUp.id}
              className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm text-ink"
            >
              <span>
                <strong>{followUpLabel(followUp.kind)}</strong> · due{' '}
                {formatDateOnly(followUp.dueOn)} · {followUp.status}
              </span>
              {mayClose && followUp.status === 'Pending' && (
                <span className="flex gap-2">
                  <Button
                    variant="secondary"
                    size="sm"
                    disabled={complete.isPending}
                    onClick={async () => {
                      setError(null);
                      try {
                        await complete.mutateAsync(followUp.id);
                        toast({ title: `${followUpLabel(followUp.kind)} marked done` });
                      } catch (failure) {
                        setError(describeSurgeryError(failure));
                      }
                    }}
                  >
                    Mark done
                  </Button>
                  <Button variant="secondary" size="sm" onClick={() => setCancelling(followUp)}>
                    Cancel
                  </Button>
                </span>
              )}
            </li>
          ))}
        </ul>
      )}
      {error && (
        <p role="alert" className={errorClass}>
          {error}
        </p>
      )}
      <ReasonDialog
        open={cancelling !== null}
        onOpenChange={(open) => !open && setCancelling(null)}
        title="Cancel this follow-up?"
        consequence={
          cancelling
            ? `The ${followUpLabel(cancelling.kind).toLowerCase()} due ${formatDateOnly(cancelling.dueOn)} is cancelled and its reminder is not sent.`
            : ''
        }
        reasonLabel="Reason for cancelling"
        confirmLabel="Cancel follow-up"
        variant="destructive"
        describeError={describeSurgeryError}
        onConfirm={async (reason) => {
          if (!cancelling) return;
          await cancel.mutateAsync({ followUpId: cancelling.id, reason });
          setCancelling(null);
          toast({ title: 'Follow-up cancelled' });
        }}
      />
    </Panel>
  );
}
