'use client';

import { useState } from 'react';
import { WriteButton } from '@/components/common/WriteButton';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { toast } from '@/components/ui/use-toast';
import { useRequestRetentionRun } from '@/lib/hooks/useRetention';
import { describeOperationsError, hasOperationsCode } from '@/lib/api/errors';

/**
 * `components/retention/RunRetentionNow.tsx` — HO-XCT-002 §11.5, U-B2. The confirmation says what
 * the run WILL do (purge expired sign-in sessions and tokens and old measurements, blank old
 * message contents, process open erasure requests, recount records eligible for review) and what
 * it will NOT (it never deletes clinical, financial, regulatory or audit records). A `409` reads
 * "A retention run is already in progress."
 */
export function RunRetentionNow() {
  const [open, setOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const run = useRequestRetentionRun();

  async function confirm() {
    setError(null);
    try {
      await run.mutateAsync();
      setOpen(false);
      toast({ title: 'Retention run requested', description: 'It starts within a minute. Follow it under Runs.' });
    } catch (e) {
      setOpen(false);
      setError(
        hasOperationsCode(e, 'RetentionRunAlreadyInProgress')
          ? 'A retention run is already in progress.'
          : describeOperationsError(e),
      );
    }
  }

  return (
    <div className="flex flex-col gap-2">
      <div>
        <WriteButton variant="secondary" onClick={() => setOpen(true)}>
          Run retention now
        </WriteButton>
      </div>
      {error && (
        <p role="alert" className="text-sm text-danger-ink">
          {error}
        </p>
      )}
      <ConfirmDialog
        open={open}
        onOpenChange={setOpen}
        title="Run retention now?"
        consequence={
          <>
            <span className="block">This run will:</span>
            <span className="block">
              remove expired sign-in sessions and reset tokens and old performance measurements,
              blank the text of old sent messages, work through open customer erasure requests, and
              recount the records eligible for review.
            </span>
            <span className="mt-2 block font-semibold">
              It will not delete clinical, financial, regulatory or audit records. Those are only
              counted for review.
            </span>
          </>
        }
        confirmLabel="Run retention"
        onConfirm={confirm}
      />
    </div>
  );
}
