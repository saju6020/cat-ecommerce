'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Banner } from '@/components/inventory/fields';
import { toast } from '@/components/ui/use-toast';
import { describeSurgeryError } from '@/lib/api/errors';
import { formatBranchDateLong } from '@/lib/format/appointment';
import { localDateOf } from '@/lib/format/time';
import { useApplySterilisationOutcome } from '@/lib/hooks/useSurgeries';
import type { SurgeryDetail } from '@/lib/api/surgeries';

/**
 * `NeuterStatusNotice` — HO-SRG-002 §11.5, US-SRG-06. For a sterilisation that ended *Completed*: either
 * *"Neuter status set to Neutered on 12 Oct"* (the server's `neuterStatusApplied`), or — when it is false —
 * a plain statement that the cat's record was not updated and the **Apply neuter status** repair action
 * (vets). The detail carries no reason for the failure (HO-SRG-002 §1 Amended), so none is invented.
 */
export function NeuterStatusNotice({
  surgery,
  canApply,
}: {
  surgery: SurgeryDetail;
  canApply: boolean;
}) {
  const apply = useApplySterilisationOutcome(surgery.id);
  const [error, setError] = useState<string | null>(null);
  if (!surgery.procedure.isSterilisation || surgery.outcome !== 'Completed') return null;
  if (surgery.neuterStatusApplied === null) return null;

  if (surgery.neuterStatusApplied) {
    const on = surgery.procedureEndedOn
      ? formatBranchDateLong(localDateOf(surgery.procedureEndedOn, surgery.branchTimeZoneId))
      : '';
    return (
      <Banner tone="info" role="status">
        Neuter status set to Neutered{on ? ` on ${on}` : ''}.
      </Banner>
    );
  }
  return (
    <div className="flex flex-col gap-2" role="alert">
      <Banner tone="warning">
        {surgery.catName}’s neuter status has not been updated from this surgery. Apply it to{' '}
        {surgery.catName}’s record once you have checked the details.
      </Banner>
      {error && <p className="text-xs font-semibold text-danger-ink">{error}</p>}
      {canApply && (
        <div>
          <Button
            variant="secondary"
            disabled={apply.isPending}
            onClick={async () => {
              setError(null);
              try {
                await apply.mutateAsync();
                toast({ title: 'Neuter status applied' });
              } catch (failure) {
                setError(describeSurgeryError(failure));
              }
            }}
          >
            {apply.isPending ? 'Applying…' : 'Apply neuter status'}
          </Button>
        </div>
      )}
    </div>
  );
}
