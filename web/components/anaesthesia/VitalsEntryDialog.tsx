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
import { FieldWithUnit } from '@/components/common/FieldWithUnit';
import { Banner } from '@/components/inventory/fields';
import { toast } from '@/components/ui/use-toast';
import { VisitCatHeader } from '@/components/visit/VisitCatHeader';
import { ApiValidationError } from '@/lib/api/client';
import { describeSurgeryError, surgeryCode, surgeryViolations } from '@/lib/api/errors';
import { VITAL_KINDS, VITAL_LABELS, VITAL_UNITS, type VitalKind } from '@/lib/format/surgery';
import { useLogVitals } from '@/lib/hooks/useAnaesthesiaLog';
import {
  TIME_PATTERN,
  implausibleVitalKinds,
  instantFromTime,
  nowTimeInZone,
  parseDecimal,
} from '@/lib/validation/anaesthesia';
import { SURGERY_LIMITS } from '@/lib/validation/surgery';
import { TextAreaBox } from '@/components/surgery/TextAreaBox';
import { errorClass, inputClass, labelClass } from '@/components/surgery/styles';

/**
 * `VitalsEntryDialog` — HO-SRG-002 §11.4, US-SRG-04. Heart rate, respiratory rate, SpO2, EtCO2,
 * temperature, systolic pressure and MAP — each optional, each with its UNIT beside the field (UI-8), and
 * the time it was taken. An `ImplausibleVitalValue` refusal marks the fields the server named with its
 * message and offers **Save anyway**, which re-sends with the confirmation flag. The compact cat header
 * (allergies, high-risk) stays on screen.
 */
export function VitalsEntryDialog({
  surgeryId,
  catId,
  zone,
  onClose,
}: {
  surgeryId: string;
  catId: string;
  zone: string;
  onClose: () => void;
}) {
  const log = useLogVitals(surgeryId);
  const [values, setValues] = useState<Record<VitalKind, string>>(
    () => Object.fromEntries(VITAL_KINDS.map((k) => [k, ''])) as Record<VitalKind, string>,
  );
  const [time, setTime] = useState(() => nowTimeInZone(zone));
  const [note, setNote] = useState('');
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<VitalKind, string>>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [implausible, setImplausible] = useState(false);
  const [saveAnyway, setSaveAnyway] = useState(false);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    const local: Partial<Record<VitalKind, string>> = {};
    const body: Record<string, number> = {};
    for (const kind of VITAL_KINDS) {
      const text = values[kind].trim();
      if (!text) continue;
      const number = parseDecimal(text);
      if (number === null)
        local[kind] =
          `Enter ${VITAL_LABELS[kind].toLowerCase()} as a number in ${VITAL_UNITS[kind]}.`;
      else body[kind] = number;
    }
    setFieldErrors(local);
    setFormError(null);
    if (Object.keys(local).length > 0) return;
    if (Object.keys(body).length === 0) {
      setFormError('Enter at least one vital.');
      return;
    }
    if (!TIME_PATTERN.test(time)) {
      setFormError('Enter the time as hours and minutes, for example 09:30.');
      return;
    }
    try {
      await log.mutateAsync({
        occurredOn: instantFromTime(time, zone),
        ...body,
        note: note.trim() || undefined,
        confirmImplausible: saveAnyway || undefined,
      });
      toast({ title: 'Vitals logged' });
      onClose();
    } catch (error) {
      if (error instanceof ApiValidationError && surgeryCode(error, 'ImplausibleVitalValue')) {
        const marked: Partial<Record<VitalKind, string>> = {};
        const named = implausibleVitalKinds(error);
        const messages = surgeryViolations(error).filter(
          (v) => v.errorCode === 'ImplausibleVitalValue',
        );
        named.forEach((kind, i) => {
          if (VITAL_KINDS.includes(kind)) marked[kind] = messages[i]?.message;
        });
        setFieldErrors(marked);
        setImplausible(true);
        if (Object.keys(marked).length === 0)
          setFormError(messages.map((m) => m.message).join(' '));
        return;
      }
      setFormError(describeSurgeryError(error));
    }
  }

  return (
    <Dialog open onOpenChange={(open) => !open && !log.isPending && onClose()}>
      <DialogContent className="max-h-[90vh] max-w-xl overflow-y-auto">
        <form noValidate onSubmit={(event) => void submit(event)} className="flex flex-col gap-4">
          <DialogHeader>
            <DialogTitle>Log vitals</DialogTitle>
            <DialogDescription>Fill in what you measured. Leave the rest empty.</DialogDescription>
          </DialogHeader>
          <VisitCatHeader catId={catId} />
          {formError && (
            <p role="alert" className={errorClass}>
              {formError}
            </p>
          )}
          <div className="grid gap-4 sm:grid-cols-2">
            {VITAL_KINDS.map((kind) => (
              <FieldWithUnit
                key={kind}
                id={`vital-${kind}`}
                label={VITAL_LABELS[kind]}
                unit={VITAL_UNITS[kind]}
                inputMode="decimal"
                autoComplete="off"
                value={values[kind]}
                error={fieldErrors[kind]}
                onChange={(event) => {
                  setValues((all) => ({ ...all, [kind]: event.target.value }));
                  setSaveAnyway(false);
                }}
              />
            ))}
            <div className="flex flex-col gap-1.5">
              <label htmlFor="vitals-time" className={labelClass}>
                Time taken ({zone})
              </label>
              <input
                id="vitals-time"
                type="time"
                className={inputClass}
                value={time}
                onChange={(event) => setTime(event.target.value)}
              />
            </div>
          </div>
          <TextAreaBox
            id="vitals-note"
            label="Note (optional)"
            value={note}
            onChange={setNote}
            max={SURGERY_LIMITS.noteMax}
            rows={2}
          />
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
          <DialogFooter>
            <Button variant="secondary" onClick={onClose} disabled={log.isPending}>
              Cancel
            </Button>
            <Button type="submit" disabled={log.isPending || (implausible && !saveAnyway)}>
              {log.isPending ? 'Saving…' : 'Save vitals'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
