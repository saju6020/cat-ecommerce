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
import { toast } from '@/components/ui/use-toast';
import { VisitCatHeader } from '@/components/visit/VisitCatHeader';
import { describeSurgeryError } from '@/lib/api/errors';
import { MILESTONES, milestoneLabel } from '@/lib/format/surgery';
import { useLogMilestone } from '@/lib/hooks/useAnaesthesiaLog';
import { TIME_PATTERN, instantFromTime, nowTimeInZone } from '@/lib/validation/anaesthesia';
import { SURGERY_LIMITS } from '@/lib/validation/surgery';
import { TextAreaBox } from '@/components/surgery/TextAreaBox';
import { errorClass, inputClass, labelClass } from '@/components/surgery/styles';

/** `MilestoneEntryDialog` — HO-SRG-002 §11.4. A milestone (premedication, induction…), the time and an optional note. */
export function MilestoneEntryDialog({
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
  const log = useLogMilestone(surgeryId);
  const [milestone, setMilestone] = useState('');
  const [time, setTime] = useState(() => nowTimeInZone(zone));
  const [note, setNote] = useState('');
  const [error, setError] = useState<string | null>(null);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    if (!milestone) return setError('Choose the milestone.');
    if (!TIME_PATTERN.test(time))
      return setError('Enter the time as hours and minutes, for example 09:30.');
    setError(null);
    try {
      await log.mutateAsync({
        milestone,
        occurredOn: instantFromTime(time, zone),
        note: note.trim() || undefined,
      });
      toast({ title: `${milestoneLabel(milestone)} logged` });
      onClose();
    } catch (failure) {
      setError(describeSurgeryError(failure));
    }
  }

  return (
    <Dialog open onOpenChange={(open) => !open && !log.isPending && onClose()}>
      <DialogContent className="max-h-[90vh] max-w-lg overflow-y-auto">
        <form noValidate onSubmit={(event) => void submit(event)} className="flex flex-col gap-4">
          <DialogHeader>
            <DialogTitle>Log a milestone</DialogTitle>
            <DialogDescription>
              Mark a point in the anaesthetic, such as induction.
            </DialogDescription>
          </DialogHeader>
          <VisitCatHeader catId={catId} />
          {error && (
            <p role="alert" className={errorClass}>
              {error}
            </p>
          )}
          <div className="flex flex-col gap-1.5">
            <label htmlFor="milestone-kind" className={labelClass}>
              Milestone
            </label>
            <select
              id="milestone-kind"
              className={inputClass}
              value={milestone}
              onChange={(event) => setMilestone(event.target.value)}
            >
              <option value="">Choose…</option>
              {MILESTONES.map((m) => (
                <option key={m} value={m}>
                  {milestoneLabel(m)}
                </option>
              ))}
            </select>
          </div>
          <div className="flex flex-col gap-1.5">
            <label htmlFor="milestone-time" className={labelClass}>
              Time ({zone})
            </label>
            <input
              id="milestone-time"
              type="time"
              className={inputClass}
              value={time}
              onChange={(event) => setTime(event.target.value)}
            />
          </div>
          <TextAreaBox
            id="milestone-note"
            label="Note (optional)"
            value={note}
            onChange={setNote}
            max={SURGERY_LIMITS.noteMax}
            rows={2}
          />
          <DialogFooter>
            <Button variant="secondary" onClick={onClose} disabled={log.isPending}>
              Cancel
            </Button>
            <Button type="submit" disabled={log.isPending}>
              {log.isPending ? 'Saving…' : 'Save milestone'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
