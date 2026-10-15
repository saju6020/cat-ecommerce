'use client';

import * as React from 'react';
import { ShieldAlert, ShieldOff, Power } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ActionDialog } from '@/components/inventory/ReasonDialog';
import { TextAreaField, TextField } from '@/components/inventory/fields';
import { describePharmacyError, pharmacyCode } from '@/lib/api/errors';
import type { FormularyMedicineDetail } from '@/lib/api/formulary';

/**
 * `ControlledToggle` and `ActivationToggle` — HO-PHM-002 §11.7, ADR-FE-067. Each is a SEPARATE action
 * with its own confirmation, never a field of the medicine form: changing whether a medicine is a
 * controlled drug (or whether it can be prescribed) must be deliberate.
 *  - Turning the controlled flag ON needs a schedule label.
 *  - Turning it OFF may need a reason: the server asks (`ControlledFlagChangeReasonRequired`) when the
 *    medicine already has register entries, and the reason field then appears — the screen does not
 *    guess whether it will be asked.
 *  - Deactivating states that existing prescriptions remain dispensable.
 */
export function ControlledToggle({
  medicine,
  onSet,
}: {
  medicine: Pick<FormularyMedicineDetail, 'name' | 'isControlled' | 'controlledSchedule'>;
  onSet: (input: {
    isControlled: boolean;
    controlledSchedule?: string | null;
    reason?: string | null;
  }) => Promise<unknown>;
}) {
  const [open, setOpen] = React.useState(false);
  const [schedule, setSchedule] = React.useState('');
  const [reason, setReason] = React.useState('');
  const [reasonAsked, setReasonAsked] = React.useState(false);
  const turningOn = !medicine.isControlled;

  React.useEffect(() => {
    if (!open) {
      setSchedule('');
      setReason('');
      setReasonAsked(false);
    }
  }, [open]);

  return (
    <section
      aria-label="Controlled drug status"
      className="flex flex-col gap-2 rounded-[var(--radius-md)] border border-border bg-surface p-4"
    >
      <h2 className="text-sm font-bold uppercase tracking-wide text-ink-muted">Controlled drug</h2>
      <p className="flex flex-wrap items-center gap-2 text-sm text-ink">
        {medicine.isControlled ? (
          <Badge variant="warning" icon={<ShieldAlert size={12} aria-hidden="true" />}>
            Controlled{medicine.controlledSchedule ? ` — ${medicine.controlledSchedule}` : ''}
          </Badge>
        ) : (
          <Badge variant="neutral">Not controlled</Badge>
        )}
        <span className="text-ink-muted">
          {medicine.isControlled
            ? 'Every supply needs a witness and is recorded in the controlled-drug register.'
            : 'Supplies are not witnessed or entered in the register.'}
        </span>
      </p>
      <div>
        <Button variant="secondary" onClick={() => setOpen(true)}>
          {turningOn ? (
            <ShieldAlert size={14} aria-hidden="true" />
          ) : (
            <ShieldOff size={14} aria-hidden="true" />
          )}
          {turningOn ? 'Mark as controlled…' : 'Clear the controlled flag…'}
        </Button>
      </div>
      <ActionDialog
        open={open}
        onOpenChange={setOpen}
        title={
          turningOn
            ? `Mark ${medicine.name} as controlled?`
            : `Clear the controlled flag on ${medicine.name}?`
        }
        consequence={
          turningOn
            ? 'From now on every supply of this medicine needs a witness and is recorded in the register. Existing register entries are unaffected.'
            : 'Supplies will no longer need a witness or a register entry. The existing register entries stay exactly as they are.'
        }
        confirmLabel={turningOn ? 'Mark as controlled' : 'Clear the flag'}
        variant={turningOn ? 'primary' : 'destructive'}
        confirmDisabled={turningOn && schedule.trim() === ''}
        describeError={describePharmacyError}
        onConfirm={async () => {
          try {
            await onSet(
              turningOn
                ? { isControlled: true, controlledSchedule: schedule.trim() }
                : { isControlled: false, reason: reason.trim() || null },
            );
          } catch (error) {
            if (pharmacyCode(error, 'ControlledFlagChangeReasonRequired')) setReasonAsked(true);
            throw error;
          }
        }}
      >
        {turningOn && (
          <TextField
            id="ct-schedule"
            label="Schedule"
            required
            value={schedule}
            maxLength={30}
            hint="The label printed on the register and the dispensing label, for example Schedule 3."
            onChange={(event) => setSchedule(event.target.value)}
          />
        )}
        {!turningOn && reasonAsked && (
          <TextAreaField
            id="ct-reason"
            label="Reason"
            required
            value={reason}
            maxLength={500}
            onChange={(event) => setReason(event.target.value)}
          />
        )}
      </ActionDialog>
    </section>
  );
}

export function ActivationToggle({
  medicine,
  onSet,
}: {
  medicine: Pick<FormularyMedicineDetail, 'name' | 'isActive'>;
  onSet: (isActive: boolean) => Promise<unknown>;
}) {
  const [open, setOpen] = React.useState(false);
  const deactivating = medicine.isActive;
  return (
    <section
      aria-label="Availability"
      className="flex flex-col gap-2 rounded-[var(--radius-md)] border border-border bg-surface p-4"
    >
      <h2 className="text-sm font-bold uppercase tracking-wide text-ink-muted">Availability</h2>
      <p className="text-sm text-ink">
        {medicine.isActive
          ? 'Active — vets can prescribe this medicine.'
          : 'Inactive — vets cannot prescribe it; existing prescriptions can still be dispensed.'}
      </p>
      <div>
        <Button variant="secondary" onClick={() => setOpen(true)}>
          <Power size={14} aria-hidden="true" />
          {deactivating ? 'Deactivate…' : 'Activate…'}
        </Button>
      </div>
      <ActionDialog
        open={open}
        onOpenChange={setOpen}
        title={deactivating ? `Deactivate ${medicine.name}?` : `Activate ${medicine.name}?`}
        consequence={
          deactivating
            ? 'Vets will no longer be able to prescribe it. Prescriptions that already exist remain dispensable.'
            : 'Vets will be able to prescribe it again.'
        }
        confirmLabel={deactivating ? 'Deactivate' : 'Activate'}
        variant={deactivating ? 'destructive' : 'primary'}
        describeError={describePharmacyError}
        onConfirm={() => onSet(!medicine.isActive)}
      />
    </section>
  );
}
