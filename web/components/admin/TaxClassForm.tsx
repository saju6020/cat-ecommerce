'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { applyValidationErrors, describeUnexpectedError } from '@/lib/api/errors';
import { ApiValidationError } from '@/lib/api/client';
import type { TaxClass } from '@/lib/api/admin-catalogue';

const schema = z.object({
  code: z.string().trim().min(1, 'Enter a code.'),
  name: z.string().trim().min(1, 'Enter a name.'),
  isActive: z.boolean(),
});
type FormValues = z.infer<typeof schema>;

const FIELD_MAP = { Code: 'code', Name: 'name', IsActive: 'isActive' } as const;
const KNOWN_FIELDS = Object.values(FIELD_MAP);

/** `components/admin/TaxClassForm.tsx` — HO-ADM-002 §11.3, US-ADM-04. Create when `taxClass` is
 *  omitted (code editable); edit when supplied (code read-only). */
export interface TaxClassFormProps {
  taxClass?: TaxClass;
  onSave: (values: { code?: string; name: string; isActive: boolean }) => Promise<unknown>;
  isPending: boolean;
}

export function TaxClassForm({ taxClass, onSave, isPending }: TaxClassFormProps) {
  const isCreate = !taxClass;
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isDirty, isSubmitting, isSubmitSuccessful },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      code: taxClass?.code ?? '',
      name: taxClass?.name ?? '',
      isActive: taxClass?.isActive ?? true,
    },
  });

  async function onSubmit(values: FormValues) {
    try {
      await onSave({
        code: isCreate ? values.code : undefined,
        name: values.name,
        isActive: values.isActive,
      });
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
      {isSubmitSuccessful && isCreate && (
        <p role="status" aria-live="polite" className="text-sm font-semibold text-ink">
          Tax class created.
        </p>
      )}

      <div className="flex flex-col gap-1.5">
        <label htmlFor="tax-class-code" className="text-sm font-semibold text-ink">
          Code
        </label>
        <input
          id="tax-class-code"
          readOnly={!isCreate}
          {...register('code')}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink read-only:bg-surface-sunken read-only:text-ink-muted"
        />
        {errors.code && (
          <p role="alert" className="text-xs font-semibold text-danger-ink">
            {errors.code.message}
          </p>
        )}
      </div>
      <div className="flex flex-col gap-1.5">
        <label htmlFor="tax-class-name" className="text-sm font-semibold text-ink">
          Name
        </label>
        <input
          id="tax-class-name"
          {...register('name')}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
        />
        {errors.name && (
          <p role="alert" className="text-xs font-semibold text-danger-ink">
            {errors.name.message}
          </p>
        )}
      </div>
      {!isCreate && (
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
        {isSubmitting || isPending ? 'Saving…' : isCreate ? 'Create tax class' : 'Save'}
      </Button>
    </form>
  );
}
