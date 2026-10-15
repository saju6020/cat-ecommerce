'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { FormErrorAlert } from '@/components/boarding/shared/FormBits';
import { describeBoardingError } from '@/lib/api/errors';
import type { MyStayDetail } from '@/lib/api/types';
import { useCancelMyStay } from '@/lib/hooks/useMyBoarding';
import { formatStayDates } from '@/lib/format/boarding';

/**
 * `components/boarding/stay/CancelStayControl.tsx` — HO-BRD-002 §11.3, U-B6, UI-3: **Cancel** with a
 * confirm that states what is affected and the **deposit outcome the server returned** (kept under
 * the cancellation policy, or refunded) — the sentence is the server's (`cancellation.depositOutcome`),
 * never worked out here (ADR-FE-105). Rendered only when the server says the stay can be cancelled.
 */
export function CancelStayControl({ stay }: { stay: MyStayDetail }) {
  const [open, setOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const cancel = useCancelMyStay(stay.id);
  if (!stay.cancellation?.canCancel) return null;

  return (
    <div className="flex flex-col gap-2">
      <FormErrorAlert message={error} />
      <div>
        <Button variant="secondary" onClick={() => setOpen(true)}>
          Cancel this stay
        </Button>
      </div>
      <ConfirmDialog
        open={open}
        onOpenChange={setOpen}
        title={`Cancel the stay for ${stay.cats.map((c) => c.catName).join(' and ')}?`}
        consequence={`${formatStayDates(stay.arrivalOn, stay.departureOn, stay.nights)} will be cancelled and the unit released. ${stay.cancellation.depositOutcome ?? ''}`}
        confirmLabel="Cancel the stay"
        cancelLabel="Keep the stay"
        pendingLabel="Cancelling…"
        variant="destructive"
        onConfirm={async () => {
          setError(null);
          try {
            await cancel.mutateAsync(null);
            setOpen(false);
          } catch (e) {
            setOpen(false);
            setError(describeBoardingError(e));
          }
        }}
      />
    </div>
  );
}
