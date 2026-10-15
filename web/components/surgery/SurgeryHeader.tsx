'use client';

import { useState } from 'react';
import { ShieldAlert } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Banner, DefinitionList } from '@/components/inventory/fields';
import { ReasonDialog } from '@/components/inventory/ReasonDialog';
import { VisitCatHeader } from '@/components/visit/VisitCatHeader';
import { toast } from '@/components/ui/use-toast';
import { describeSurgeryError } from '@/lib/api/errors';
import { formatAppointmentRange, formatAppointmentWhen } from '@/lib/format/appointment';
import { formatMoney } from '@/lib/format/money';
import { PRICING_BASIS_LABELS } from '@/lib/format/surgery';
import { timeOnlyInZone } from '@/lib/format/time';
import { useStaffRoles } from '@/lib/hooks/useStaffRoles';
import { useCancelSurgery } from '@/lib/hooks/useSurgeries';
import { SURGERY_LIMITS } from '@/lib/validation/surgery';
import type { SurgeryDetail } from '@/lib/api/surgeries';
import { RescheduleDialog } from './RescheduleDialog';
import { SurgeryStatusBadge } from './SurgeryStatusBadge';
import { SurgeryStepper } from './SurgeryStepper';

/**
 * ⭐ `SurgeryHeader` — HO-SRG-002 §11.3. The UI-7 cat header, the procedure, when (branch zone), the team,
 * the price with its basis (information only — no payment step), the status stepper, **Reschedule** and
 * **Cancel** (only when the server's `allowedTransitions` has them AND the role may schedule), and the
 * consent-override banner (*"Started without recorded consent — emergency override by Dr Rahman at
 * 09:05: {reason}"*).
 */
export function SurgeryHeader({ surgery }: { surgery: SurgeryDetail }) {
  const roles = useStaffRoles();
  const cancel = useCancelSurgery(surgery.id);
  const [cancelling, setCancelling] = useState(false);
  const [rescheduling, setRescheduling] = useState(false);
  const zone = surgery.branchTimeZoneId;
  const mayChange = roles.has('Reception', 'Vet', 'Administrator');
  const canReschedule = mayChange && surgery.allowedTransitions.includes('Reschedule');
  const canCancel = mayChange && surgery.allowedTransitions.includes('Cancel');
  const override = surgery.consentOverride;

  return (
    <header className="flex flex-col gap-4">
      <VisitCatHeader catId={surgery.catId} />
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h1 className="font-display text-2xl font-semibold text-ink">
            {surgery.procedure.name} — {surgery.catName}
          </h1>
          <p className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-ink-muted">
            <SurgeryStatusBadge status={surgery.status} />
            <span>{formatAppointmentRange(surgery.startsOn, surgery.endsOn, zone)}</span>
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          {canReschedule && (
            <Button variant="secondary" onClick={() => setRescheduling(true)}>
              Reschedule
            </Button>
          )}
          {canCancel && (
            <Button variant="secondary" onClick={() => setCancelling(true)}>
              Cancel surgery
            </Button>
          )}
        </div>
      </div>

      {surgery.status === 'Cancelled' ? (
        <Banner tone="danger">
          This surgery was cancelled
          {surgery.cancelledOn ? ` on ${formatAppointmentWhen(surgery.cancelledOn, zone)}` : ''}
          {surgery.cancellationReason ? `: ${surgery.cancellationReason}` : '.'}
        </Banner>
      ) : (
        <SurgeryStepper status={surgery.status} />
      )}

      {override && (
        <Banner tone="warning" role="status">
          <span className="flex items-start gap-2">
            <ShieldAlert size={16} className="mt-0.5 flex-none" aria-hidden="true" />
            <span>
              Started without recorded consent — emergency override by{' '}
              {override.overriddenByName ?? 'a vet'} at{' '}
              {timeOnlyInZone(override.overriddenOn, zone)}: {override.reason}
            </span>
          </span>
        </Banner>
      )}

      <DefinitionList
        className="rounded-[var(--radius-md)] border border-border bg-surface p-4"
        items={[
          {
            label: 'Customer',
            value: `${surgery.customerName}${surgery.customerPhone ? ` · ${surgery.customerPhone}` : ''}`,
          },
          { label: 'Theatre', value: surgery.theatre.name },
          { label: 'Surgeon', value: surgery.surgeon.name },
          { label: 'Assistant', value: surgery.assistant.name },
          { label: 'Admit by', value: formatAppointmentWhen(surgery.admitBy, zone) },
          {
            label: 'Price',
            value: (
              <>
                {formatMoney(surgery.priceSnapshot.amount)}{' '}
                <span className="text-ink-muted">
                  ({PRICING_BASIS_LABELS[surgery.priceSnapshot.basis]}, for information — paid at
                  the clinic)
                </span>
                {surgery.priceSnapshot.campaignEligibilityNote && (
                  <span className="block text-ink-muted">
                    Eligibility: {surgery.priceSnapshot.campaignEligibilityNote}
                  </span>
                )}
              </>
            ),
          },
        ]}
      />

      <ReasonDialog
        open={cancelling}
        onOpenChange={setCancelling}
        title="Cancel this surgery?"
        consequence={`Cancel ${surgery.catName}’s ${surgery.procedure.name.toLowerCase()} on ${formatAppointmentWhen(surgery.startsOn, zone)} with ${surgery.surgeon.name}? The owner will be told.`}
        reasonLabel="Reason for cancelling"
        hint={`Up to ${SURGERY_LIMITS.cancellationReasonMax} characters.`}
        confirmLabel="Cancel surgery"
        pendingLabel="Cancelling…"
        variant="destructive"
        describeError={describeSurgeryError}
        onConfirm={async (reason) => {
          await cancel.mutateAsync(reason);
          setCancelling(false);
          toast({ title: 'Surgery cancelled' });
        }}
      />
      {rescheduling && (
        <RescheduleDialog surgery={surgery} onClose={() => setRescheduling(false)} />
      )}
    </header>
  );
}
