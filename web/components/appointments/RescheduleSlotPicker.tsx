'use client';

import { forwardRef, useState } from 'react';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { SlotStep, type SlotStepProps } from '@/components/booking/SlotStep';
import { formatAppointmentWhen } from '@/lib/format/appointment';
import type { SlotGroup, SlotPerformer } from '@/lib/api/booking';

/**
 * `components/appointments/RescheduleSlotPicker.tsx` — HO-APT-002 §11.2: the booking flow's
 * `SlotStep` for the same service, clinic and cat, then a confirmation that states the
 * consequence — *"Move to {new time}? Your current time will be released."* — before posting the
 * reschedule directly (no hold). The current time is shown throughout and stays shown if the
 * move fails (U-B2): the container reports the failure through `notice`.
 */
export interface RescheduleSlotPickerProps extends Omit<
  SlotStepProps,
  'onSelectSlot' | 'onJoinWaitlist' | 'isSelecting'
> {
  currentWhen: string;
  onMove: (slot: SlotGroup, performer: SlotPerformer | null) => Promise<void>;
  onCancel: () => void;
}

export const RescheduleSlotPicker = forwardRef<HTMLDivElement, RescheduleSlotPickerProps>(
  function RescheduleSlotPicker({ currentWhen, onMove, onCancel, zone, ...slotStep }, ref) {
    const [choice, setChoice] = useState<{
      slot: SlotGroup;
      performer: SlotPerformer | null;
    } | null>(null);

    return (
      <div className="flex flex-col gap-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h2 className="font-display text-xl font-semibold text-ink">Choose a new time</h2>
          <Button variant="secondary" size="sm" onClick={onCancel}>
            Keep my current time
          </Button>
        </div>
        <p className="text-sm text-ink-muted">
          Your appointment is currently <strong className="text-ink">{currentWhen}</strong>. It
          stays as it is until you confirm a new time.
        </p>
        <SlotStep
          ref={ref}
          zone={zone}
          {...slotStep}
          onSelectSlot={(slot, performer) => setChoice({ slot, performer })}
        />
        <ConfirmDialog
          open={choice !== null}
          onOpenChange={(open) => {
            if (!open) setChoice(null);
          }}
          title="Move your appointment?"
          consequence={
            choice
              ? `Move to ${formatAppointmentWhen(choice.slot.startsOn, zone)} (${zone}) with ${
                  (choice.performer ?? choice.slot.performers[0])?.name ?? 'the clinic'
                }? Your current time will be released.`
              : ''
          }
          confirmLabel="Move appointment"
          pendingLabel="Moving…"
          onConfirm={async () => {
            if (!choice) return;
            const current = choice;
            try {
              await onMove(current.slot, current.performer);
            } finally {
              setChoice(null);
            }
          }}
        />
      </div>
    );
  },
);
