'use client';

import { useEffect, useRef, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
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
import { moveSchema, type MoveValues } from '@/lib/validation/appointments';
import { describeAppointmentError } from '@/lib/api/errors';
import { formatAppointmentWhen, zonedLocalToInstant } from '@/lib/format/appointment';
import { timeOnlyInZone, localDateOf } from '@/lib/format/time';

export interface MoveSubject {
  id: string;
  catName: string;
  serviceName: string;
  startsOn: string | null;
  performerId: string | null;
  roomId: string | null;
}

export interface MoveTarget {
  startsOn: string;
  performerId: string;
  roomId: string;
}

export interface MoveChoice {
  id: string;
  name: string;
}

/**
 * ⭐ `components/calendar/MoveDialog.tsx` — HO-APT-002 §11.6, ADR-FE-026: the keyboard and
 * small-screen path to a move (and the confirmation a drag drop opens, with `initialTarget`
 * set). Date, vet, room and time, then a confirmation stating old and new — *"Move Mitu's
 * vaccination from 10:00 with Dr Rahman to 11:30 with Dr Akter?"* (UI-3) — then the same request a
 * drag sends. The SERVER decides: a refusal (a clash, off shift, a stale version) is shown inside
 * the dialog in the server's words and the choices are kept. The time is typed, on the slot grid
 * (`step`); the appointment payloads carry no service id to ask the slots endpoint with, so no
 * availability is computed here (HO-APT-002 §1 Amended).
 */
export function MoveDialog({
  open,
  onOpenChange,
  subject,
  zone,
  gridMinutes,
  performers,
  rooms,
  initialTarget,
  onMove,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  subject: MoveSubject | null;
  zone: string;
  gridMinutes: number;
  performers: MoveChoice[];
  rooms: MoveChoice[];
  /** A drop target: the dialog opens straight on the confirmation. */
  initialTarget?: MoveTarget | null;
  onMove: (target: MoveTarget) => Promise<void>;
}) {
  const returnFocusRef = useDialogReturnFocus(open);
  const [stage, setStage] = useState<'choose' | 'confirm'>('choose');
  const [pending, setPending] = useState(false);
  const [serverError, setServerError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    getValues,
    reset,
    watch,
    formState: { errors },
  } = useForm<MoveValues>({
    resolver: zodResolver(moveSchema),
    defaultValues: { date: '', performerId: '', roomId: '', time: '' },
  });

  // The form resets when the dialog opens for a (different) appointment — not whenever the parent
  // re-renders with a new `subject` object, which a background refetch does and which would wipe
  // the server's refusal and the user's edits.
  const subjectRef = useRef(subject);
  subjectRef.current = subject;
  const subjectId = subject?.id ?? null;
  useEffect(() => {
    const subject = subjectRef.current;
    if (!open || !subject) return;
    const target = initialTarget;
    const startsOn = target?.startsOn ?? subject.startsOn;
    reset({
      date: startsOn ? localDateOf(startsOn, zone) : '',
      performerId: target?.performerId ?? subject.performerId ?? '',
      roomId: target?.roomId ?? subject.roomId ?? '',
      time: startsOn ? timeOnlyInZone(startsOn, zone) : '',
    });
    setStage(target ? 'confirm' : 'choose');
    setServerError(null);
  }, [open, subjectId, initialTarget, zone, reset]);

  // `null` until the vet/room lists have loaded: the sentence then leaves the clause out rather
  // than reading "with the same … in the same" (W15).
  const nameOf = (list: MoveChoice[], id: string | null) =>
    list.find((choice) => choice.id === id)?.name ?? null;
  const values = watch();

  async function confirm() {
    const current = getValues();
    setPending(true);
    setServerError(null);
    try {
      await onMove({
        startsOn: zonedLocalToInstant(current.date, current.time, zone),
        performerId: current.performerId,
        roomId: current.roomId,
      });
      onOpenChange(false);
    } catch (error) {
      setServerError(describeAppointmentError(error));
      setStage('choose');
    } finally {
      setPending(false);
    }
  }

  if (!subject) return null;
  // After the subject guard: a closed dialog has no zone to format in (W16: no UTC stand-in).
  const newStartsOn =
    values.date && values.time ? zonedLocalToInstant(values.date, values.time, zone) : null;
  const cls =
    'rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent';
  const oldPerformer = nameOf(performers, subject.performerId);
  const newPerformer = nameOf(performers, values.performerId);
  const newRoom = nameOf(rooms, values.roomId);
  const oldWhen = subject.startsOn ? timeOnlyInZone(subject.startsOn, zone) : '—';

  return (
    <Dialog open={open} onOpenChange={(next) => !pending && onOpenChange(next)}>
      <DialogContent returnFocusRef={returnFocusRef}>
        <DialogHeader>
          <DialogTitle>Move {subject.catName}’s appointment</DialogTitle>
          <DialogDescription>
            {subject.serviceName}, currently{' '}
            {subject.startsOn
              ? `${formatAppointmentWhen(subject.startsOn, zone)} (${zone})`
              : 'unscheduled'}
            .
          </DialogDescription>
        </DialogHeader>

        {serverError && (
          <p
            role="alert"
            className="mb-3 rounded-[var(--radius-sm)] bg-danger-bg px-3 py-2 text-sm font-semibold text-danger-ink"
          >
            {serverError}
          </p>
        )}

        {stage === 'choose' ? (
          <form
            noValidate
            onSubmit={handleSubmit(() => {
              setServerError(null);
              setStage('confirm');
            })}
            className="flex flex-col gap-4"
          >
            <div className="flex flex-col gap-1.5">
              <label htmlFor="move-date" className="text-sm font-semibold text-ink">
                Date ({zone})
              </label>
              <input id="move-date" type="date" className={cls} {...register('date')} />
              {errors.date && (
                <p role="alert" className="text-xs font-semibold text-danger-ink">
                  {errors.date.message}
                </p>
              )}
            </div>
            <div className="flex flex-col gap-1.5">
              <label htmlFor="move-performer" className="text-sm font-semibold text-ink">
                Vet or nurse
              </label>
              <select id="move-performer" className={cls} {...register('performerId')}>
                <option value="">Choose…</option>
                {performers.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name}
                  </option>
                ))}
              </select>
              {errors.performerId && (
                <p role="alert" className="text-xs font-semibold text-danger-ink">
                  {errors.performerId.message}
                </p>
              )}
            </div>
            <div className="flex flex-col gap-1.5">
              <label htmlFor="move-room" className="text-sm font-semibold text-ink">
                Room
              </label>
              <select id="move-room" className={cls} {...register('roomId')}>
                <option value="">Choose…</option>
                {rooms.map((r) => (
                  <option key={r.id} value={r.id}>
                    {r.name}
                  </option>
                ))}
              </select>
              {errors.roomId && (
                <p role="alert" className="text-xs font-semibold text-danger-ink">
                  {errors.roomId.message}
                </p>
              )}
            </div>
            <div className="flex flex-col gap-1.5">
              <label htmlFor="move-time" className="text-sm font-semibold text-ink">
                Time ({zone})
              </label>
              <input
                id="move-time"
                type="time"
                step={gridMinutes * 60}
                className={cls}
                {...register('time')}
              />
              <p className="text-xs text-ink-muted">
                On the {gridMinutes}-minute grid. The clinic system checks the time when you
                confirm.
              </p>
              {errors.time && (
                <p role="alert" className="text-xs font-semibold text-danger-ink">
                  {errors.time.message}
                </p>
              )}
            </div>
            <DialogFooter>
              <Button type="button" variant="secondary" onClick={() => onOpenChange(false)}>
                Cancel
              </Button>
              <Button type="submit">Review move</Button>
            </DialogFooter>
          </form>
        ) : (
          <div className="flex flex-col gap-4">
            <p className="text-sm text-ink">
              Move {subject.catName}’s {subject.serviceName.toLowerCase()} from {oldWhen}
              {oldPerformer && ` with ${oldPerformer}`} to{' '}
              {newStartsOn ? timeOnlyInZone(newStartsOn, zone) : '—'}
              {newPerformer && ` with ${newPerformer}`}
              {newRoom && ` in ${newRoom}`} on{' '}
              {newStartsOn ? formatAppointmentWhen(newStartsOn, zone).split(',')[0] : '—'}?
            </p>
            <DialogFooter>
              <Button variant="secondary" onClick={() => setStage('choose')} disabled={pending}>
                Back
              </Button>
              <Button onClick={() => void confirm()} disabled={pending} aria-busy={pending}>
                {pending ? 'Moving…' : 'Move appointment'}
              </Button>
            </DialogFooter>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
