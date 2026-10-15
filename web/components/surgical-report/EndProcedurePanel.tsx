'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { toast } from '@/components/ui/use-toast';
import { describeSurgeryError } from '@/lib/api/errors';
import { useEndProcedure } from '@/lib/hooks/useSurgeries';
import type { ProcedureOutcome, SurgeryDetail } from '@/lib/api/surgeries';
import { errorClass } from '@/components/surgery/styles';

/**
 * `EndProcedurePanel` — HO-SRG-002 §11.5, US-SRG-05/06. In `InTheatre`, a vet ends the procedure as
 * *Completed* or *Abandoned*. The confirm states what happens (UI-3): for a sterilisation, that the cat's
 * neuter status is set. Shown only when the server's `allowedTransitions` has `EndProcedure`.
 */
export function EndProcedurePanel({
  surgery,
  canEnd,
}: {
  surgery: SurgeryDetail;
  canEnd: boolean;
}) {
  const end = useEndProcedure(surgery.id);
  const [outcome, setOutcome] = useState<ProcedureOutcome>('Completed');
  const [confirming, setConfirming] = useState(false);
  const [error, setError] = useState<string | null>(null);
  if (!surgery.allowedTransitions.includes('EndProcedure')) return null;

  const consequence =
    outcome === 'Completed'
      ? `End the procedure as Completed?${
          surgery.procedure.isSterilisation
            ? ` For a ${surgery.procedure.name.toLowerCase()} this sets ${surgery.catName}’s neuter status.`
            : ''
        } ${surgery.catName} moves to recovery.`
      : `End the procedure as Abandoned? ${surgery.catName} moves to recovery and the neuter status is not changed.`;

  return (
    <section aria-labelledby="srg-end" className="flex flex-col gap-3">
      <h2 id="srg-end" className="font-display text-lg font-semibold text-ink">
        End the procedure
      </h2>
      {canEnd ? (
        <>
          <fieldset className="flex flex-wrap gap-4">
            <legend className="mb-1 text-sm font-semibold text-ink">Outcome</legend>
            {(['Completed', 'Abandoned'] as const).map((value) => (
              <label key={value} className="flex items-center gap-2 text-sm text-ink">
                <input
                  type="radio"
                  name="procedure-outcome"
                  checked={outcome === value}
                  onChange={() => setOutcome(value)}
                />
                {value}
              </label>
            ))}
          </fieldset>
          {error && (
            <p role="alert" className={errorClass}>
              {error}
            </p>
          )}
          <div>
            <Button onClick={() => setConfirming(true)}>End procedure</Button>
          </div>
        </>
      ) : (
        <p className="text-sm text-ink-muted">A vet ends the procedure.</p>
      )}
      <ConfirmDialog
        open={confirming}
        onOpenChange={setConfirming}
        title="End the procedure?"
        consequence={consequence}
        confirmLabel="End procedure"
        pendingLabel="Ending…"
        onConfirm={async () => {
          try {
            await end.mutateAsync(outcome);
            toast({ title: `Procedure ended as ${outcome}` });
          } catch (failure) {
            setError(describeSurgeryError(failure));
          } finally {
            setConfirming(false);
          }
        }}
      />
    </section>
  );
}
