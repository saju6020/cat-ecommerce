'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { CircleCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { toast } from '@/components/ui/use-toast';
import { ProvidersGate } from './ProvidersGate';
import { QuickVitalsDialog } from './vitals/QuickVitalsDialog';
import { describeAppointmentError, describeClinicalError } from '@/lib/api/errors';
import { useStartAppointment } from '@/lib/hooks/useStaffAppointments';
import { useStaffSession } from '@/lib/hooks/useSession';
import { useOpenVisit, useOpenVisits } from '@/lib/hooks/useVisit';
import { timeOnlyInZone } from '@/lib/format/time';
import type { QueueEntry } from '@/lib/api/front-desk';

/**
 * `QueueVisitActions` — HO-EMR-002 §11.9, the two queue-card actions HO-APT-002's `ClinicQueue` gains.
 *  - **Record vitals** (nurse, vet): the `QuickVitalsDialog` — opens the visit if needed; afterwards
 *    the card reads *"Vitals recorded 09:12"* (from this session, else from the open-visits list).
 *  - **Open consultation** (vet): starts the appointment through HO-APT-002's EXISTING Start flow if
 *    it is still `CheckedIn` (a walk-in or an unassigned appointment needs a vet and a room, so
 *    `onStart` opens that dialog and the vet presses Open consultation again), opens the visit
 *    (`POST /api/staff/visits`, idempotent) and goes to the workspace.
 * The buttons are role-gated for convenience; the server still refuses what the role cannot do.
 */
export function QueueVisitActions(props: QueueVisitActionsProps) {
  return (
    <ProvidersGate needsRouter>
      <QueueVisitActionsInner {...props} />
    </ProvidersGate>
  );
}

interface QueueVisitActionsProps {
  entry: QueueEntry;
  zone: string;
  onStart: (entry: QueueEntry) => void;
}

function QueueVisitActionsInner({ entry, zone, onStart }: QueueVisitActionsProps) {
  const router = useRouter();
  const roles = useStaffSession().data?.roleIds ?? [];
  const canVitals = roles.includes('Nurse') || roles.includes('Vet');
  const canConsult = roles.includes('Vet');
  const live = entry.status === 'CheckedIn' || entry.status === 'InConsultation';
  const open = useOpenVisits();
  const start = useStartAppointment();
  const openVisit = useOpenVisit();
  const [vitalsOpen, setVitalsOpen] = useState(false);
  const [recordedAt, setRecordedAt] = useState<Date | null>(null);
  const [busy, setBusy] = useState(false);

  if (!live || (!canVitals && !canConsult)) return null;
  const hasVitals = open.data?.some((v) => v.appointmentId === entry.appointmentId && v.hasVitals);

  async function openConsultation() {
    setBusy(true);
    try {
      if (entry.status === 'CheckedIn') {
        if (entry.isWalkIn || !entry.performerName) {
          onStart(entry);
          return;
        }
        try {
          await start.mutateAsync({ appointmentId: entry.appointmentId });
        } catch (error) {
          toast({
            title: 'Starting the visit didn’t work',
            description: describeAppointmentError(error),
          });
          return;
        }
      }
      const result = await openVisit.mutateAsync(entry.appointmentId);
      router.push(`/staff/visits/${result.visitId}`);
    } catch (error) {
      toast({ title: 'The consultation didn’t open', description: describeClinicalError(error) });
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="flex flex-wrap items-center gap-3">
      {canVitals && (
        <Button
          variant="secondary"
          size="sm"
          aria-label={`Record vitals for ${entry.catName}`}
          onClick={() => setVitalsOpen(true)}
        >
          Record vitals
        </Button>
      )}
      {canConsult && (
        <Button
          size="sm"
          aria-label={`Open consultation for ${entry.catName}`}
          onClick={() => void openConsultation()}
          disabled={busy}
          aria-busy={busy}
        >
          Open consultation
        </Button>
      )}
      {(recordedAt || hasVitals) && (
        <span
          role="status"
          className="inline-flex items-center gap-1 text-xs font-semibold text-accent-ink"
        >
          <CircleCheck size={12} aria-hidden="true" />
          Vitals recorded{recordedAt ? ` ${timeOnlyInZone(recordedAt.toISOString(), zone)}` : ''}
        </span>
      )}
      {canVitals && (
        <QuickVitalsDialog
          open={vitalsOpen}
          onOpenChange={setVitalsOpen}
          appointmentId={entry.appointmentId}
          catName={entry.catName}
          zone={zone}
          onRecorded={setRecordedAt}
        />
      )}
    </div>
  );
}
