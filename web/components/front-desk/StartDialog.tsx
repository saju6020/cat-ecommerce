'use client';

import { useEffect } from 'react';
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
import { startSchema, type StartValues } from '@/lib/validation/appointments';
import { submitWithServerErrors } from '@/lib/api/errors';

export interface StartChoice {
  id: string;
  name: string;
  detail?: string;
}

/**
 * `components/front-desk/StartDialog.tsx` — HO-APT-002 §11.5. A walk-in gets its vet and room when
 * it starts: both are chosen here, and the SERVER's refusal (not qualified, wrong kind of room, a
 * clash) is shown inside the dialog — the lists offer every vet and room the branch has, so the
 * server is the only judge of "the right kind". The dialog owns its form state, so a 30 s poll that
 * replaces the data behind it never resets the choices (UI-5, ADR-FE-027, U-D2).
 */
export function StartDialog({
  open,
  onOpenChange,
  catName,
  service,
  performers,
  rooms,
  onStart,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  catName: string;
  service: string;
  performers: StartChoice[];
  rooms: StartChoice[];
  onStart: (values: StartValues) => Promise<void>;
}) {
  const returnFocusRef = useDialogReturnFocus(open);
  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<StartValues>({
    resolver: zodResolver(startSchema),
    defaultValues: { performerId: '', roomId: '' },
  });

  // A fresh open starts clean; a poll while open never touches the form.
  useEffect(() => {
    if (open) reset({ performerId: '', roomId: '' });
  }, [open, reset]);

  async function submit(values: StartValues) {
    const done = await submitWithServerErrors(
      async () => {
        await onStart(values);
        return true;
      },
      setError,
      {
        fieldMap: { PerformerId: 'performerId', RoomId: 'roomId' },
        knownFields: ['performerId', 'roomId'],
      },
    );
    if (done) onOpenChange(false);
  }

  return (
    <Dialog open={open} onOpenChange={(next) => !isSubmitting && onOpenChange(next)}>
      <DialogContent returnFocusRef={returnFocusRef}>
        <form onSubmit={handleSubmit(submit)} noValidate>
          <DialogHeader>
            <DialogTitle>Start {catName}’s visit</DialogTitle>
            <DialogDescription>
              {service}. Choose who will see this cat and in which room.
            </DialogDescription>
          </DialogHeader>

          <div className="flex flex-col gap-4">
            <div className="flex flex-col gap-1.5">
              <label htmlFor="start-performer" className="text-sm font-semibold text-ink">
                Vet or nurse
              </label>
              <select
                id="start-performer"
                aria-invalid={!!errors.performerId}
                aria-describedby={errors.performerId ? 'start-performer-error' : undefined}
                className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
                {...register('performerId')}
              >
                <option value="">Choose…</option>
                {performers.map((performer) => (
                  <option key={performer.id} value={performer.id}>
                    {performer.name}
                  </option>
                ))}
              </select>
              {errors.performerId && (
                <p
                  id="start-performer-error"
                  role="alert"
                  className="text-xs font-semibold text-danger-ink"
                >
                  {errors.performerId.message}
                </p>
              )}
            </div>
            <div className="flex flex-col gap-1.5">
              <label htmlFor="start-room" className="text-sm font-semibold text-ink">
                Room
              </label>
              <select
                id="start-room"
                aria-invalid={!!errors.roomId}
                aria-describedby={errors.roomId ? 'start-room-error' : undefined}
                className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
                {...register('roomId')}
              >
                <option value="">Choose…</option>
                {rooms.map((room) => (
                  <option key={room.id} value={room.id}>
                    {room.name}
                    {room.detail ? ` (${room.detail})` : ''}
                  </option>
                ))}
              </select>
              {errors.roomId && (
                <p
                  id="start-room-error"
                  role="alert"
                  className="text-xs font-semibold text-danger-ink"
                >
                  {errors.roomId.message}
                </p>
              )}
            </div>
            {errors.root && (
              <p role="alert" className="text-sm font-semibold text-danger-ink">
                {errors.root.message}
              </p>
            )}
          </div>

          <DialogFooter>
            <Button
              type="button"
              variant="secondary"
              onClick={() => onOpenChange(false)}
              disabled={isSubmitting}
            >
              Cancel
            </Button>
            <Button type="submit" disabled={isSubmitting} aria-busy={isSubmitting}>
              {isSubmitting ? 'Starting…' : 'Start visit'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
