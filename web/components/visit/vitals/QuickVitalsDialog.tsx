'use client';

import { useEffect, useRef, useState } from 'react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  useDialogReturnFocus,
} from '@/components/ui/dialog';
import { VisitCatHeader } from '../VisitCatHeader';
import { VitalsForm } from './VitalsForm';
import { describeClinicalError } from '@/lib/api/errors';
import { useOpenVisit, useRecordVitals, useVisit } from '@/lib/hooks/useVisit';

/**
 * ⭐ `QuickVitalsDialog` — HO-EMR-002 §11.2 (US-CLN-11), from the queue card. Opens the visit if it
 * does not exist yet (`POST /api/staff/visits`, idempotent — an existing one is returned as success),
 * shows the compact cat header (allergies repeated inside the dialog, since it covers the page) and
 * the vitals form: the nurse path is six fields and one Save. On success the dialog closes and
 * the caller shows *"Vitals recorded 09:12"*; the header's weight is refreshed by the hook.
 */
export function QuickVitalsDialog({
  open,
  onOpenChange,
  appointmentId,
  catName,
  zone,
  onRecorded,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  appointmentId: string;
  catName: string;
  zone: string;
  onRecorded: (recordedAt: Date) => void;
}) {
  const returnFocusRef = useDialogReturnFocus(open);
  const openVisit = useOpenVisit();
  const [visitId, setVisitId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const startedRef = useRef(false);

  useEffect(() => {
    if (!open) {
      startedRef.current = false;
      return;
    }
    if (visitId || startedRef.current) return;
    startedRef.current = true;
    setError(null);
    openVisit
      .mutateAsync(appointmentId)
      .then((result) => setVisitId(result.visitId))
      .catch((caught) => setError(describeClinicalError(caught)));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, appointmentId, visitId]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        returnFocusRef={returnFocusRef}
        className="max-h-[92vh] max-w-xl overflow-y-auto"
      >
        <DialogHeader>
          <DialogTitle>Record vitals — {catName}</DialogTitle>
          <DialogDescription>
            Enter what you measured. Values outside the usual range ask for a confirmation.
          </DialogDescription>
        </DialogHeader>
        {error ? (
          <div className="flex flex-col items-start gap-2">
            <p role="alert" className="text-sm font-semibold text-danger-ink">
              {error}
            </p>
            <Button
              variant="secondary"
              size="sm"
              onClick={() => {
                startedRef.current = false;
                setError(null);
                setVisitId(null);
                onOpenChange(false);
              }}
            >
              Close
            </Button>
          </div>
        ) : !visitId ? (
          <p role="status" className="text-sm text-ink-muted">
            Opening the visit…
          </p>
        ) : (
          <QuickVitalsBody
            visitId={visitId}
            zone={zone}
            onSaved={() => {
              onRecorded(new Date());
              onOpenChange(false);
            }}
          />
        )}
      </DialogContent>
    </Dialog>
  );
}

function QuickVitalsBody({
  visitId,
  zone,
  onSaved,
}: {
  visitId: string;
  zone: string;
  onSaved: () => void;
}) {
  // The queue entry carries no cat id, so the cat comes from the visit itself.
  const visit = useVisit(visitId);
  const record = useRecordVitals(visitId, visit.data?.catId ?? '');
  // Render the data whenever we have it: a failed BACKGROUND refetch must not unmount the form and
  // lose what the nurse has typed (only a first-load failure is an error screen).
  if (!visit.data) {
    if (visit.isError) {
      return (
        <p role="alert" className="text-sm font-semibold text-danger-ink">
          {describeClinicalError(visit.error)}
        </p>
      );
    }
    return (
      <p role="status" className="text-sm text-ink-muted">
        Loading the visit…
      </p>
    );
  }
  return (
    <div className="flex flex-col gap-4">
      <VisitCatHeader catId={visit.data.catId} />
      <VitalsForm
        zone={zone}
        idPrefix="quick-vitals"
        save={(request) => record.mutateAsync(request)}
        onSaved={onSaved}
      />
    </div>
  );
}
