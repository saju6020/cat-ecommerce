'use client';

import { useRef, useState } from 'react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { QuantityInput } from '@/components/common/Quantity';
import { FormularyPicker } from '@/components/formulary/FormularyPicker';
import { WitnessDialog, type WitnessCredentials } from '@/components/pharmacy/WitnessDialog';
import { Banner } from '@/components/inventory/fields';
import { toast } from '@/components/ui/use-toast';
import { VisitCatHeader } from '@/components/visit/VisitCatHeader';
import { describeSurgeryError, surgeryCode } from '@/lib/api/errors';
import { formatDateOnly } from '@/lib/format/time';
import { formatMgPerKg, routeLabel } from '@/lib/format/surgery';
import { doseUnitsForForm } from '@/lib/format/units';
import { useLogDrug } from '@/lib/hooks/useAnaesthesiaLog';
import { useFormularyMedicine } from '@/lib/hooks/useFormulary';
import {
  TIME_PATTERN,
  drugFingerprint,
  instantFromTime,
  mapDrugViolations,
  nowTimeInZone,
  parseDecimal,
  type MappedDrugErrors,
} from '@/lib/validation/anaesthesia';
import type { FormularyMedicineSummary } from '@/lib/api/formulary';
import type { BatchSummary } from '@/lib/api/inventory-stock';
import { TextAreaBox } from '@/components/surgery/TextAreaBox';
import { errorClass, hintClass, inputClass, labelClass } from '@/components/surgery/styles';
import { DrugBatchChooser } from './DrugBatchChooser';

const NO_ERRORS: MappedDrugErrors = { summary: [], byField: {}, witness: null };

export interface DrugDialogContext {
  surgeryId: string;
  catId: string;
  zone: string;
  weight: { kilograms: number; recordedOn: string } | null;
  /** The people who may be named as having given the drug (vets): the caller first. */
  administrators: Array<{ id: string; name: string }>;
}

/**
 * ⭐ `DrugEntryDialog` — HO-SRG-002 §11.4, ADR-FE-071/075, US-SRG-04/08. Medicine (the formulary picker),
 * dose with its unit, route, time given (defaults to now in the branch zone, editable back), who gave it,
 * and — for a stocked medicine — the batch (usable batches only) and the quantity drawn. A CONTROLLED
 * drug also needs the wastage (0 allowed) and a second member of staff: **Next: witness** opens the
 * witness dialog on this same device, where the witness types their OWN identifier and password. The
 * backend takes those in the same request (HO-SRG-001 §1 Amended (16)), so the entry is saved when the
 * witness confirms; the credentials are passed straight to the request and never kept (ADR-FE-071).
 * The mg/kg figure is for information only (ADR-FE-075). One idempotency `entryId` is generated per
 * entry and reused on every retry; it changes only if the dose, medicine, batch or quantity change.
 */
export function DrugEntryDialog({
  context,
  onClose,
}: {
  context: DrugDialogContext;
  onClose: () => void;
}) {
  const { surgeryId, zone, weight, administrators } = context;
  const logDrug = useLogDrug(surgeryId);
  const [medicine, setMedicine] = useState<FormularyMedicineSummary | null>(null);
  const detail = useFormularyMedicine(medicine?.id ?? null).data;
  const [dose, setDose] = useState({ amount: '', unit: '' });
  const [route, setRoute] = useState('');
  const [time, setTime] = useState(() => nowTimeInZone(zone));
  const [givenBy, setGivenBy] = useState(administrators[0]?.id ?? '');
  const [batch, setBatch] = useState<{ id: string; unit: string; first: boolean } | null>(null);
  const [drawn, setDrawn] = useState('');
  const [wasted, setWasted] = useState('');
  const [fefoReason, setFefoReason] = useState('');
  const [errors, setErrors] = useState<MappedDrugErrors>(NO_ERRORS);
  const [witnessOpen, setWitnessOpen] = useState(false);
  const [witnessBusy, setWitnessBusy] = useState(false);
  const [witnessError, setWitnessError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const attempt = useRef<{ id: string; fingerprint: string } | null>(null);

  const stocked = (detail?.linkedSkus.length ?? 0) > 0;
  const controlled = medicine?.isControlled ?? false;
  const doseNumber = parseDecimal(dose.amount);
  const drawnNumber = parseDecimal(drawn);
  const wastedNumber = parseDecimal(wasted);
  const missing: string[] = [];
  if (!medicine) missing.push('the medicine');
  if (doseNumber === null || doseNumber <= 0) missing.push('the dose');
  if (!dose.unit) missing.push('the dose unit');
  if (!route) missing.push('the route');
  if (!TIME_PATTERN.test(time)) missing.push('the time');
  if (!givenBy) missing.push('who gave it');
  if (stocked && !batch) missing.push('the batch');
  if (stocked && (drawnNumber === null || drawnNumber <= 0)) missing.push('the quantity drawn');
  if (stocked && batch && !batch.first && !fefoReason.trim())
    missing.push('why not the batch that expires first');
  if (controlled && wastedNumber === null) missing.push('the quantity wasted (0 if none)');
  const ready = missing.length === 0;
  const mgPerKg =
    weight && doseNumber && dose.unit
      ? formatMgPerKg(doseNumber, dose.unit, weight.kilograms, formatDateOnly(weight.recordedOn))
      : null;

  async function send(credentials?: WitnessCredentials) {
    if (!medicine || doseNumber === null) return;
    const body = {
      medicineId: medicine.id,
      doseAmount: doseNumber,
      doseUnit: dose.unit,
      route,
      occurredOn: instantFromTime(time, zone),
      administeredById: givenBy,
      batchId: batch?.id,
      quantityDrawn: stocked && drawnNumber !== null ? drawnNumber : undefined,
      quantityWasted: controlled && wastedNumber !== null ? wastedNumber : undefined,
      fefoOverrideReason: batch && !batch.first ? fefoReason.trim() : undefined,
    };
    const fingerprint = drugFingerprint({
      medicineId: body.medicineId,
      batchId: body.batchId,
      quantityDrawn: body.quantityDrawn,
      doseAmount: body.doseAmount,
    });
    // One id per drug entry, reused on every retry; a NEW one only when the values it protects changed.
    if (!attempt.current || attempt.current.fingerprint !== fingerprint) {
      attempt.current = { id: crypto.randomUUID(), fingerprint };
    }
    setSaving(true);
    setWitnessBusy(true);
    setErrors(NO_ERRORS);
    try {
      await logDrug({
        entryId: attempt.current.id,
        ...body,
        witnessIdentifier: credentials?.identifier,
        witnessPassword: credentials?.password,
      });
      toast({ title: `${medicine.name} logged` });
      onClose();
    } catch (error) {
      if (surgeryCode(error, 'DuplicateAnaesthesiaEntry', 'AnaesthesiaEntryIdMismatch')) {
        attempt.current = null; // the key is spent: the next try mints a new one
      }
      const mapped = mapDrugViolations(error);
      if (mapped.witness && credentials) {
        setWitnessError(mapped.witness);
      } else {
        setWitnessOpen(false);
        setErrors(
          mapped.summary.length === 0 && Object.keys(mapped.byField).length === 0
            ? { ...mapped, summary: [describeSurgeryError(error)] }
            : mapped,
        );
      }
    } finally {
      setSaving(false);
      setWitnessBusy(false);
    }
  }

  const fieldError = (field: keyof MappedDrugErrors['byField']) => errors.byField[field]?.join(' ');
  const units = detail ? doseUnitsForForm(detail.form) : [];

  return (
    <>
      <Dialog open onOpenChange={(open) => !open && !saving && !witnessOpen && onClose()}>
        <DialogContent className="max-h-[90vh] max-w-2xl overflow-y-auto">
          <form
            noValidate
            onSubmit={(event) => {
              event.preventDefault();
              if (!ready) return;
              if (controlled) {
                setWitnessError(null);
                setWitnessOpen(true);
              } else void send();
            }}
            className="flex flex-col gap-4"
          >
            <DialogHeader>
              <DialogTitle>Log a drug</DialogTitle>
              <DialogDescription>
                Every dose and quantity is recorded with its unit. A mistake is fixed later with a
                correction; the entry itself cannot be edited or deleted.
              </DialogDescription>
            </DialogHeader>
            <VisitCatHeader catId={context.catId} />
            {errors.summary.length > 0 && (
              <Banner tone="danger">
                <ul className="list-disc pl-5">
                  {errors.summary.map((m) => (
                    <li key={m}>{m}</li>
                  ))}
                </ul>
              </Banner>
            )}

            <FormularyPicker
              id="drug-medicine"
              label="Medicine"
              clearOnSelect={false}
              error={fieldError('medicine')}
              onSelect={(m) => {
                setMedicine(m);
                setDose({ amount: '', unit: '' });
                setRoute('');
                setBatch(null);
                setDrawn('');
                setWasted('');
              }}
            />
            {medicine?.isControlled && (
              <Banner tone="warning" role="status">
                {medicine.name} is a controlled drug: record the quantity wasted, then a second
                member of staff witnesses it.
              </Banner>
            )}
            {medicine && !detail && <p className={hintClass}>Loading the medicine…</p>}

            {detail && (
              <>
                <QuantityInput
                  id="drug-dose"
                  label="Dose"
                  amount={dose.amount}
                  unit={dose.unit}
                  units={units}
                  required
                  error={fieldError('dose')}
                  hint={mgPerKg ? `${mgPerKg} — for information` : undefined}
                  onChange={setDose}
                />
                <div className="grid gap-4 sm:grid-cols-3">
                  <div className="flex flex-col gap-1.5">
                    <label htmlFor="drug-route" className={labelClass}>
                      Route
                    </label>
                    <select
                      id="drug-route"
                      className={inputClass}
                      value={route}
                      aria-invalid={fieldError('route') ? true : undefined}
                      onChange={(event) => setRoute(event.target.value)}
                    >
                      <option value="">Choose…</option>
                      {detail.allowedRoutes.map((r) => (
                        <option key={r} value={r}>
                          {routeLabel(r)} ({r})
                        </option>
                      ))}
                    </select>
                    {fieldError('route') && <p className={errorClass}>{fieldError('route')}</p>}
                  </div>
                  <div className="flex flex-col gap-1.5">
                    <label htmlFor="drug-time" className={labelClass}>
                      Time given ({zone})
                    </label>
                    <input
                      id="drug-time"
                      type="time"
                      className={inputClass}
                      value={time}
                      onChange={(event) => setTime(event.target.value)}
                    />
                    {fieldError('time') && <p className={errorClass}>{fieldError('time')}</p>}
                  </div>
                  <div className="flex flex-col gap-1.5">
                    <label htmlFor="drug-by" className={labelClass}>
                      Given by
                    </label>
                    <select
                      id="drug-by"
                      className={inputClass}
                      value={givenBy}
                      aria-invalid={fieldError('administeredBy') ? true : undefined}
                      onChange={(event) => setGivenBy(event.target.value)}
                    >
                      {administrators.map((person) => (
                        <option key={person.id} value={person.id}>
                          {person.name}
                        </option>
                      ))}
                    </select>
                    {fieldError('administeredBy') && (
                      <p className={errorClass}>{fieldError('administeredBy')}</p>
                    )}
                  </div>
                </div>

                {stocked && (
                  <>
                    <DrugBatchChooser
                      skus={detail.linkedSkus}
                      value={batch?.id ?? ''}
                      error={fieldError('batch')}
                      onChange={(chosen: BatchSummary, first: boolean) => {
                        // A different stock unit is a different quantity: do not carry 1 vial over as 1 ml.
                        if (batch && batch.unit !== chosen.stockUnit) {
                          setDrawn('');
                          setWasted('');
                        }
                        setBatch({ id: chosen.id, unit: chosen.stockUnit, first });
                      }}
                    />
                    {batch && !batch.first && (
                      <TextAreaBox
                        id="drug-fefo"
                        label="Why not the batch that expires first?"
                        value={fefoReason}
                        onChange={setFefoReason}
                        max={500}
                        rows={2}
                      />
                    )}
                    {batch && (
                      <QuantityInput
                        id="drug-drawn"
                        label="Quantity drawn"
                        amount={drawn}
                        unit={batch.unit}
                        fixedUnit={batch.unit}
                        required
                        error={fieldError('drawn')}
                        onChange={({ amount }) => setDrawn(amount)}
                      />
                    )}
                    {batch && controlled && (
                      <QuantityInput
                        id="drug-wasted"
                        label="Quantity wasted"
                        amount={wasted}
                        unit={batch.unit}
                        fixedUnit={batch.unit}
                        required
                        error={fieldError('wasted')}
                        hint="Enter 0 if nothing was wasted."
                        onChange={({ amount }) => setWasted(amount)}
                      />
                    )}
                  </>
                )}
              </>
            )}

            {!ready && medicine && (
              <p className={hintClass} aria-live="polite">
                Still needed: {missing.join(', ')}.
              </p>
            )}
            <DialogFooter>
              <Button variant="secondary" onClick={onClose} disabled={saving}>
                Cancel
              </Button>
              <Button type="submit" disabled={!ready || saving}>
                {controlled ? 'Next: witness' : saving ? 'Saving…' : 'Save drug entry'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <WitnessDialog
        open={witnessOpen}
        witnessRoles={['Vet', 'Nurse']}
        busy={witnessBusy}
        error={witnessError}
        title="Controlled drug — witness needed"
        confirmLabel="Confirm and save entry"
        onCancel={() => {
          if (!witnessBusy) {
            setWitnessOpen(false);
            setWitnessError(null);
          }
        }}
        onSubmit={(credentials) => void send(credentials)}
      />
    </>
  );
}
