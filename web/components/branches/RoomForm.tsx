'use client';

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { applyValidationErrors, describeUnexpectedError } from '@/lib/api/errors';
import { ApiValidationError } from '@/lib/api/client';
import { ROOM_KINDS, type Room } from '@/lib/api/admin-branches';

const schema = z.object({
  name: z.string().trim().min(1, 'Enter a name.').max(100),
  kind: z.enum(['Consultation', 'Treatment', 'Theatre', 'Grooming']),
  isActive: z.boolean(),
});
type FormValues = z.infer<typeof schema>;

const FIELD_MAP = { Name: 'name', Kind: 'kind', IsActive: 'isActive' } as const;
const KNOWN_FIELDS = Object.values(FIELD_MAP);

/** `components/branches/RoomForm.tsx` — HO-ADM-002 §11.2, US-ADM-01. Create when `room` is
 *  omitted; edit (including active toggle) when supplied. */
export interface RoomFormProps {
  room?: Room;
  onSave: (values: { name: string; kind: Room['kind']; isActive: boolean }) => Promise<unknown>;
  isPending: boolean;
  onDone?: () => void;
}

export function RoomForm({ room, onSave, isPending, onDone }: RoomFormProps) {
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isDirty, isSubmitting, isSubmitSuccessful },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: room?.name ?? '',
      kind: room?.kind ?? 'Consultation',
      isActive: room?.isActive ?? true,
    },
  });

  // ADM backlog 28 (§7 UI-3): switching an active room off is confirmed, like branch deactivation.
  const [deactivateValues, setDeactivateValues] = useState<FormValues | null>(null);

  async function onSubmit(values: FormValues) {
    if (room?.isActive && !values.isActive) {
      setDeactivateValues(values);
      return;
    }
    await save(values);
  }

  async function save(values: FormValues) {
    try {
      await onSave(values);
      onDone?.();
    } catch (error) {
      if (error instanceof ApiValidationError) {
        applyValidationErrors(error, setError, { fieldMap: FIELD_MAP, knownFields: KNOWN_FIELDS });
      } else {
        setError('root', { type: 'server', message: describeUnexpectedError(error) });
      }
    }
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex max-w-sm flex-col gap-3">
      <UnsavedChangesGuard when={isDirty && !isSubmitSuccessful} />
      {errors.root && (
        <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
          {errors.root.message}
        </p>
      )}
      {isSubmitSuccessful && !room && (
        <p role="status" aria-live="polite" className="text-sm font-semibold text-ink">
          Room added.
        </p>
      )}

      <div className="flex flex-col gap-1.5">
        <label htmlFor="room-name" className="text-sm font-semibold text-ink">
          Name
        </label>
        <input
          id="room-name"
          {...register('name')}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
        />
        {errors.name && (
          <p role="alert" className="text-xs font-semibold text-danger-ink">
            {errors.name.message}
          </p>
        )}
      </div>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="room-kind" className="text-sm font-semibold text-ink">
          Kind
        </label>
        <select
          id="room-kind"
          {...register('kind')}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
        >
          {ROOM_KINDS.map((kind) => (
            <option key={kind} value={kind}>
              {kind}
            </option>
          ))}
        </select>
      </div>

      {room && (
        <label className="flex items-center gap-2 text-sm text-ink">
          <input type="checkbox" {...register('isActive')} />
          Active
        </label>
      )}

      <Button
        type="submit"
        disabled={isSubmitting || isPending}
        aria-busy={isSubmitting || isPending}
        className="w-fit"
      >
        {isSubmitting || isPending ? 'Saving…' : room ? 'Save' : 'Add room'}
      </Button>

      {room && (
        <ConfirmDialog
          open={deactivateValues !== null}
          onOpenChange={(open) => {
            if (!open) setDeactivateValues(null);
          }}
          title={`Deactivate ${room.name}?`}
          consequence={`${room.name} will stop being offered for new appointments. Existing appointments and records are kept.`}
          confirmLabel="Deactivate"
          variant="destructive"
          onConfirm={async () => {
            const values = deactivateValues;
            setDeactivateValues(null);
            if (values) await save(values);
          }}
        />
      )}
    </form>
  );
}
