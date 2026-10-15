'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { FieldWithUnit } from '@/components/common/FieldWithUnit';
import { QuantityInput } from '@/components/common/Quantity';
import { WitnessDialog, type WitnessCredentials } from '@/components/pharmacy/WitnessDialog';
import { Banner } from '@/components/inventory/fields';
import { toast } from '@/components/ui/use-toast';
import { VisitCatHeader } from '@/components/visit/VisitCatHeader';
import { describeSurgeryError, surgeryCode } from '@/lib/api/errors';
import {
  MILESTONES,
  VITAL_KINDS,
  VITAL_LABELS,
  VITAL_UNITS,
  formatDose,
  milestoneLabel,
  routeLabel,
  type VitalKind,
} from '@/lib/format/surgery';
import { timeOnlyInZone } from '@/lib/format/time';
import { doseUnitsForForm } from '@/lib/format/units';
import { useCorrectEntry } from '@/lib/hooks/useAnaesthesiaLog';
import { useFormularyMedicine } from '@/lib/hooks/useFormulary';
import {
  TIME_PATTERN,
  implausibleVitalKinds,
  instantFromTime,
  mapDrugViolations,
  parseDecimal,
} from '@/lib/validation/anaesthesia';
import { SURGERY_LIMITS, correctionReasonSchema } from '@/lib/validation/surgery';
import type { AnaesthesiaEntry, CorrectEntryRequest } from '@/lib/api/anaesthesia-log';
import type { DrugDialogContext } from './DrugEntryDialog';
import { TextAreaBox } from '@/components/surgery/TextAreaBox';
import { errorClass, inputClass, labelClass } from '@/components/surgery/styles';

/**
 * ⭐ `CorrectEntryDialog` — HO-SRG-002 §11.4, ADR-FE-070, US-SRG-04 AC2 / US-SRG-08 AC2. **Correct…** is the
 * only way to change what the log says: this adds a NEW linked entry and leaves the original, struck
 * through, in the log. The form is prefilled from the entry; the reason (10 to 500 characters) is
 * required; the confirm says *"The original entry stays in the log."* A correction of a CONTROLLED drug
 * needs a new witness, entered in the witness dialog on this device and sent with the request (never
 * kept). `AnaesthesiaEntryAlreadyCorrected` / `CorrectionTargetNotFound` mean someone corrected it first:
 * reload the log and correct the latest entry.
 */
export function CorrectEntryDialog({
  entry,
  context,
  onClose,
}: {
  entry: AnaesthesiaEntry;
  context: DrugDialogContext;
  onClose: () => void;
}) {
  const { surgeryId, zone, administrators } = context;
  const correct = useCorrectEntry(surgeryId);
  const v = entry.values;
  const kind = entry.correctedKind ?? entry.kind;
  const detail = useFormularyMedicine(kind === 'Drug' ? v.medicineId : null).data;
  const [reason, setReason] = useState('');
  const [time, setTime] = useState(timeOnlyInZone(entry.occurredOn, zone));
  const [dose, setDose] = useState({ amount: String(v.doseAmount ?? ''), unit: v.doseUnit ?? '' });
  const [route, setRoute] = useState(v.route ?? '');
  const [givenBy, setGivenBy] = useState(v.administeredById ?? administrators[0]?.id ?? '');
  const [drawn, setDrawn] = useState(v.quantityDrawn !== null ? String(v.quantityDrawn) : '');
  const [wasted, setWasted] = useState(v.quantityWasted !== null ? String(v.quantityWasted) : '');
  const [vitals, setVitals] = useState<Record<VitalKind, string>>(
    () =>
      Object.fromEntries(
        VITAL_KINDS.map((k) => [k, v[k] !== null && v[k] !== undefined ? String(v[k]) : '']),
      ) as Record<VitalKind, string>,
  );
  const [note, setNote] = useState(v.note ?? '');
  const [milestone, setMilestone] = useState(v.milestone ?? '');
  const [confirming, setConfirming] = useState(false);
  const [witnessOpen, setWitnessOpen] = useState(false);
  const [witnessError, setWitnessError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [reasonError, setReasonError] = useState<string | undefined>();
  const [formError, setFormError] = useState<string | null>(null);
  const [vitalErrors, setVitalErrors] = useState<Partial<Record<VitalKind, string>>>({});
  const [implausible, setImplausible] = useState(false);
  const [saveAnyway, setSaveAnyway] = useState(false);
  const controlledDrug = kind === 'Drug' && v.isControlled;

  function buildRequest(credentials?: WitnessCredentials): CorrectEntryRequest | null {
    const base = {
      reason: reason.trim(),
      occurredOn: instantFromTime(time, zone, new Date(entry.occurredOn).getTime()),
    };
    if (kind === 'Drug') {
      const amount = parseDecimal(dose.amount);
      if (amount === null || amount <= 0 || !dose.unit || !route) {
        setFormError('Enter the dose with its unit, and the route.');
        return null;
      }
      const drawnNumber = drawn ? parseDecimal(drawn) : undefined;
      const wastedNumber = wasted ? parseDecimal(wasted) : undefined;
      if (drawnNumber === null || wastedNumber === null) {
        setFormError('Enter the quantities as numbers.');
        return null;
      }
      return {
        ...base,
        drug: {
          doseAmount: amount,
          doseUnit: dose.unit,
          route,
          administeredById: givenBy,
          quantityDrawn: drawnNumber,
          quantityWasted: wastedNumber,
        },
        witnessIdentifier: credentials?.identifier,
        witnessPassword: credentials?.password,
      };
    }
    if (kind === 'Vitals') {
      const out: Record<string, number> = {};
      const local: Partial<Record<VitalKind, string>> = {};
      for (const k of VITAL_KINDS) {
        if (!vitals[k].trim()) continue;
        const n = parseDecimal(vitals[k]);
        if (n === null)
          local[k] = `Enter ${VITAL_LABELS[k].toLowerCase()} as a number in ${VITAL_UNITS[k]}.`;
        else out[k] = n;
      }
      setVitalErrors(local);
      if (Object.keys(local).length > 0) return null;
      return {
        ...base,
        vitals: {
          ...out,
          note: note.trim() || undefined,
          confirmImplausible: saveAnyway || undefined,
        },
      };
    }
    if (!milestone) {
      setFormError('Choose the milestone.');
      return null;
    }
    return { ...base, milestone: { milestone, note: note.trim() || undefined } };
  }

  async function send(credentials?: WitnessCredentials) {
    const request = buildRequest(credentials);
    if (!request) {
      setConfirming(false);
      return;
    }
    setBusy(true);
    try {
      await correct(entry.id, request);
      toast({ title: 'Correction logged' });
      onClose();
    } catch (error) {
      setConfirming(false);
      if (surgeryCode(error, 'ImplausibleVitalValue')) {
        const marked: Partial<Record<VitalKind, string>> = {};
        implausibleVitalKinds(error).forEach(
          (k) => (marked[k] = 'This value looks unlikely. Check it.'),
        );
        setVitalErrors(marked);
        setImplausible(true);
        setWitnessOpen(false);
      } else {
        const mapped = mapDrugViolations(error);
        if (mapped.witness && credentials) setWitnessError(mapped.witness);
        else {
          setWitnessOpen(false);
          setFormError(
            [...mapped.summary, ...Object.values(mapped.byField).flat()].join(' ') ||
              describeSurgeryError(error),
          );
        }
      }
    } finally {
      setBusy(false);
    }
  }

  function next(event: React.FormEvent) {
    event.preventDefault();
    const parsed = correctionReasonSchema.safeParse(reason);
    if (!parsed.success) {
      setReasonError(parsed.error.issues[0]?.message);
      return;
    }
    if (!TIME_PATTERN.test(time)) {
      setFormError('Enter the time as hours and minutes, for example 09:30.');
      return;
    }
    setReasonError(undefined);
    setFormError(null);
    setConfirming(true);
  }

  return (
    <>
      <Dialog
        open
        onOpenChange={(open) => !open && !busy && !witnessOpen && !confirming && onClose()}
      >
        <DialogContent className="max-h-[90vh] max-w-2xl overflow-y-auto">
          <form noValidate onSubmit={next} className="flex flex-col gap-4">
            <DialogHeader>
              <DialogTitle>Correct this entry</DialogTitle>
              <DialogDescription>
                This adds a correction. The original entry stays in the log, struck through.
              </DialogDescription>
            </DialogHeader>
            <VisitCatHeader catId={context.catId} />
            <div className="rounded-[var(--radius-sm)] bg-surface-sunken p-3 text-sm text-ink">
              <p className="text-xs font-bold uppercase tracking-wide text-ink-muted">
                The original entry
              </p>
              {kind === 'Drug' ? (
                <p>
                  {v.medicineName}{' '}
                  {v.doseAmount !== null && v.doseUnit && formatDose(v.doseAmount, v.doseUnit)}
                  {v.route && ` ${routeLabel(v.route)}`}
                </p>
              ) : kind === 'Vitals' ? (
                <p>Vitals at {timeOnlyInZone(entry.occurredOn, zone)}</p>
              ) : (
                <p>{v.milestone ? milestoneLabel(v.milestone) : 'Milestone'}</p>
              )}
            </div>
            {formError && (
              <p role="alert" className={errorClass}>
                {formError}
              </p>
            )}
            <TextAreaBox
              id="correction-reason"
              label="Reason for the correction"
              value={reason}
              onChange={setReason}
              max={SURGERY_LIMITS.correctionReasonMax}
              min={SURGERY_LIMITS.correctionReasonMin}
              error={reasonError}
            />
            <div className="flex max-w-xs flex-col gap-1.5">
              <label htmlFor="correction-time" className={labelClass}>
                Time ({zone})
              </label>
              <input
                id="correction-time"
                type="time"
                className={inputClass}
                value={time}
                onChange={(event) => setTime(event.target.value)}
              />
            </div>

            {kind === 'Drug' && (
              <>
                <QuantityInput
                  id="correction-dose"
                  label="Dose"
                  amount={dose.amount}
                  unit={dose.unit}
                  units={detail ? doseUnitsForForm(detail.form) : dose.unit ? [dose.unit] : []}
                  onChange={setDose}
                />
                <div className="grid gap-4 sm:grid-cols-2">
                  <div className="flex flex-col gap-1.5">
                    <label htmlFor="correction-route" className={labelClass}>
                      Route
                    </label>
                    <select
                      id="correction-route"
                      className={inputClass}
                      value={route}
                      onChange={(event) => setRoute(event.target.value)}
                    >
                      {(detail?.allowedRoutes ?? (route ? [route] : [])).map((r) => (
                        <option key={r} value={r}>
                          {routeLabel(r)} ({r})
                        </option>
                      ))}
                    </select>
                  </div>
                  <div className="flex flex-col gap-1.5">
                    <label htmlFor="correction-by" className={labelClass}>
                      Given by
                    </label>
                    <select
                      id="correction-by"
                      className={inputClass}
                      value={givenBy}
                      onChange={(event) => setGivenBy(event.target.value)}
                    >
                      {[
                        ...administrators,
                        ...(v.administeredById &&
                        !administrators.some((a) => a.id === v.administeredById)
                          ? [
                              {
                                id: v.administeredById,
                                name: v.administeredByName ?? 'Staff member',
                              },
                            ]
                          : []),
                      ].map((person) => (
                        <option key={person.id} value={person.id}>
                          {person.name}
                        </option>
                      ))}
                    </select>
                  </div>
                </div>
                {v.stockUnit && v.quantityDrawn !== null && (
                  <QuantityInput
                    id="correction-drawn"
                    label="Quantity drawn"
                    amount={drawn}
                    unit={v.stockUnit}
                    fixedUnit={v.stockUnit}
                    onChange={({ amount }) => setDrawn(amount)}
                  />
                )}
                {controlledDrug && v.stockUnit && (
                  <QuantityInput
                    id="correction-wasted"
                    label="Quantity wasted"
                    amount={wasted}
                    unit={v.stockUnit}
                    fixedUnit={v.stockUnit}
                    hint="Enter 0 if nothing was wasted."
                    onChange={({ amount }) => setWasted(amount)}
                  />
                )}
                {controlledDrug && (
                  <Banner tone="warning" role="status">
                    This is a controlled drug: a second member of staff must witness the correction.
                  </Banner>
                )}
              </>
            )}

            {kind === 'Vitals' && (
              <>
                <div className="grid gap-4 sm:grid-cols-2">
                  {VITAL_KINDS.map((k) => (
                    <FieldWithUnit
                      key={k}
                      id={`correction-${k}`}
                      label={VITAL_LABELS[k]}
                      unit={VITAL_UNITS[k]}
                      inputMode="decimal"
                      autoComplete="off"
                      value={vitals[k]}
                      error={vitalErrors[k]}
                      onChange={(event) =>
                        setVitals((all) => ({ ...all, [k]: event.target.value }))
                      }
                    />
                  ))}
                </div>
                {implausible && (
                  <Banner tone="warning">
                    <label className="flex items-start gap-2 text-sm font-semibold">
                      <input
                        type="checkbox"
                        className="mt-1 h-4 w-4"
                        checked={saveAnyway}
                        onChange={(event) => setSaveAnyway(event.target.checked)}
                      />
                      Save anyway — I have checked these values and they are correct
                    </label>
                  </Banner>
                )}
              </>
            )}

            {kind === 'Milestone' && (
              <div className="flex flex-col gap-1.5">
                <label htmlFor="correction-milestone" className={labelClass}>
                  Milestone
                </label>
                <select
                  id="correction-milestone"
                  className={inputClass}
                  value={milestone}
                  onChange={(event) => setMilestone(event.target.value)}
                >
                  {MILESTONES.map((m) => (
                    <option key={m} value={m}>
                      {milestoneLabel(m)}
                    </option>
                  ))}
                </select>
              </div>
            )}
            {kind !== 'Drug' && (
              <TextAreaBox
                id="correction-note"
                label="Note (optional)"
                value={note}
                onChange={setNote}
                max={SURGERY_LIMITS.noteMax}
                rows={2}
              />
            )}

            <DialogFooter>
              <Button variant="secondary" onClick={onClose} disabled={busy}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy || (implausible && !saveAnyway)}>
                Review correction
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <ConfirmDialog
        open={confirming}
        onOpenChange={setConfirming}
        title="Add this correction?"
        consequence="The original entry stays in the log. The correction is added beside it, with your reason."
        confirmLabel={controlledDrug ? 'Continue to the witness' : 'Add correction'}
        pendingLabel="Saving…"
        onConfirm={async () => {
          if (controlledDrug) {
            setConfirming(false);
            setWitnessError(null);
            setWitnessOpen(true);
          } else {
            await send();
            setConfirming(false);
          }
        }}
      />
      <WitnessDialog
        open={witnessOpen}
        witnessRoles={['Vet', 'Nurse']}
        busy={busy}
        error={witnessError}
        title="Controlled drug — witness needed"
        confirmLabel="Confirm and add correction"
        onCancel={() => {
          if (!busy) {
            setWitnessOpen(false);
            setWitnessError(null);
          }
        }}
        onSubmit={(credentials) => void send(credentials)}
      />
    </>
  );
}
