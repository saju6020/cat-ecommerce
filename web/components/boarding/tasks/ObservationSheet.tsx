'use client';

import { useState } from 'react';
import { AlertTriangle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  useDialogReturnFocus,
} from '@/components/ui/dialog';
import { FormErrorAlert, SelectRow, TextAreaRow } from '@/components/boarding/shared/FormBits';
import {
  LITTER_OUTPUT_LABEL,
  MOOD_LABEL,
  PORTION_LABEL,
  PORTION_ORDER,
  PORTION_SPOKEN,
  WATER_LABEL,
} from '@/components/boarding/shared/labels';
import { describeBoardingError } from '@/lib/api/errors';
import type { CatMood, LitterOutput, PortionEaten, WaterIntake } from '@/lib/api/types';
import { useRecordObservation } from '@/lib/hooks/useBoardingCare';
import { cn } from '@/lib/utils';

/**
 * `components/boarding/tasks/ObservationSheet.tsx` — ⭐ HO-BRD-002 §11.7, US-BRD-11: one cat's
 * observation — portion eaten (None / ¼ / ½ / ¾ / All as labelled buttons, 44 px), water, litter,
 * mood, notes. The sheet owns its values, so a 30 s refresh behind it changes nothing typed
 * (ADR-FE-101, U-D4). **“Not eating — flagged”** is shown from the SERVER's flag for the cat
 * (`notEating`), never computed here (ADR-FE-105, U-D5).
 */
export function ObservationSheet({
  open,
  onOpenChange,
  stayId,
  catId,
  catName,
  notEating,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  stayId: string;
  catId: string;
  catName: string;
  notEating: boolean;
}) {
  const record = useRecordObservation(stayId);
  const [portion, setPortion] = useState<PortionEaten | null>(null);
  const [water, setWater] = useState<WaterIntake>('Normal');
  const [litter, setLitter] = useState<LitterOutput>('Normal');
  const [mood, setMood] = useState<CatMood>('Relaxed');
  const [notes, setNotes] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const returnFocusRef = useDialogReturnFocus(open);

  async function save() {
    if (!portion) {
      setError('Choose how much was eaten.');
      return;
    }
    setError(null);
    try {
      await record.mutateAsync({
        catId,
        portionEaten: portion,
        waterIntake: water,
        litterOutput: litter,
        mood,
        notes: notes.trim() || null,
      });
      setSaved(true);
      setPortion(null);
      setNotes('');
      onOpenChange(false);
    } catch (e) {
      setError(describeBoardingError(e));
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent hideClose returnFocusRef={returnFocusRef}>
        <DialogHeader>
          <DialogTitle>Observation — {catName}</DialogTitle>
          <DialogDescription>
            Record how {catName} is doing. It goes into the owner’s diary.
          </DialogDescription>
        </DialogHeader>
        {notEating && (
          <p
            role="status"
            className="flex items-center gap-2 rounded-[var(--radius-sm)] bg-danger-bg px-3 py-2 text-sm font-bold text-danger-ink"
          >
            <AlertTriangle size={16} aria-hidden="true" /> Not eating — flagged
          </p>
        )}
        <div className="flex flex-col gap-3">
          <fieldset>
            <legend className="mb-1.5 text-sm font-semibold text-ink">Portion eaten</legend>
            <div role="radiogroup" aria-label="Portion eaten" className="flex flex-wrap gap-2">
              {PORTION_ORDER.map((p) => (
                <button
                  key={p}
                  type="button"
                  role="radio"
                  aria-checked={portion === p}
                  aria-label={PORTION_SPOKEN[p]}
                  onClick={() => setPortion(p)}
                  className={cn(
                    'min-h-11 min-w-14 rounded-[var(--radius-sm)] border px-3 text-sm font-bold',
                    portion === p
                      ? 'border-accent bg-accent text-white'
                      : 'border-border-strong bg-surface text-ink',
                  )}
                >
                  {PORTION_LABEL[p]}
                </button>
              ))}
            </div>
          </fieldset>
          <div className="grid gap-3 sm:grid-cols-3">
            <SelectRow
              id="obs-water"
              label="Water"
              value={water}
              onChange={(e) => setWater(e.target.value as WaterIntake)}
            >
              {Object.entries(WATER_LABEL).map(([k, v]) => (
                <option key={k} value={k}>
                  {v}
                </option>
              ))}
            </SelectRow>
            <SelectRow
              id="obs-litter"
              label="Litter"
              value={litter}
              onChange={(e) => setLitter(e.target.value as LitterOutput)}
            >
              {Object.entries(LITTER_OUTPUT_LABEL).map(([k, v]) => (
                <option key={k} value={k}>
                  {v}
                </option>
              ))}
            </SelectRow>
            <SelectRow
              id="obs-mood"
              label="Mood"
              value={mood}
              onChange={(e) => setMood(e.target.value as CatMood)}
            >
              {Object.entries(MOOD_LABEL).map(([k, v]) => (
                <option key={k} value={k}>
                  {v}
                </option>
              ))}
            </SelectRow>
          </div>
          <TextAreaRow
            id="obs-notes"
            label="Notes (optional)"
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
          />
          <FormErrorAlert message={error} />
          {saved && (
            <p className="sr-only" role="status">
              Observation saved.
            </p>
          )}
        </div>
        <DialogFooter>
          <Button variant="secondary" className="min-h-11" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button className="min-h-11" onClick={() => void save()} disabled={record.isPending}>
            {record.isPending ? 'Saving…' : 'Save observation'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
