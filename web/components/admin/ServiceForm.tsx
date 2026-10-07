'use client';

import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { applyValidationErrors, describeUnexpectedError } from '@/lib/api/errors';
import { ApiValidationError } from '@/lib/api/client';
import { SERVICE_CATEGORIES } from '@/lib/api/admin-catalogue';
import { ROOM_KINDS } from '@/lib/api/admin-branches';
import type { ClinicService } from '@/lib/api/admin-catalogue';
import type { TaxClass } from '@/lib/api/admin-catalogue';

const schema = z.object({
  code: z.string().trim().min(1, 'Enter a code.').max(30),
  name: z.string().trim().min(1, 'Enter a name.').max(200),
  description: z.string().trim().max(2000),
  category: z.enum([
    'Consultation',
    'Vaccination',
    'Spay',
    'Neuter',
    'Dental',
    'Grooming',
    'Emergency',
    'FollowUp',
  ]),
  // Plain z.number(), not z.coerce.number() (see WeightEntryForm.tsx's comment on why) —
  // register('durationMinutes', { valueAsNumber: true }) below does the conversion instead.
  durationMinutes: z.number({ error: 'Enter a duration.' }).min(5).max(480),
  qualifiedRoleIds: z.array(z.string()).min(1, 'Select at least one role.'),
  requiredRoomKind: z.enum(['Consultation', 'Treatment', 'Theatre', 'Grooming']),
  taxClassId: z.string().min(1, 'Choose a tax class.'),
  isBookableOnline: z.boolean(),
});
type FormValues = z.infer<typeof schema>;

const FIELD_MAP = {
  Code: 'code',
  Name: 'name',
  Description: 'description',
  Category: 'category',
  DurationMinutes: 'durationMinutes',
  QualifiedRoleIds: 'qualifiedRoleIds',
  RequiredRoomKind: 'requiredRoomKind',
  TaxClassId: 'taxClassId',
  IsBookableOnline: 'isBookableOnline',
} as const;
const KNOWN_FIELDS = Object.values(FIELD_MAP);

/** `components/admin/ServiceForm.tsx` — HO-ADM-002 §11.3, US-ADM-04. Create when `service` is
 *  omitted (code editable); edit when supplied (code read-only). ARCH gate precedent (HO-ACC-002
 *  §8.2 rule 4) — takes `roleCatalogue`/`taxClasses` as props; the page wires
 *  `useStaffRoleCatalogue()`/`useTaxClasses()`. */
export interface ServiceFormProps {
  service?: ClinicService;
  roleCatalogue: { data: { roleIds: string[] } | undefined };
  taxClasses: { data: TaxClass[] | undefined };
  onSave: (values: Omit<FormValues, 'code'> & { code?: string }) => Promise<unknown>;
  isPending: boolean;
}

export function ServiceForm({
  service,
  roleCatalogue,
  taxClasses,
  onSave,
  isPending,
}: ServiceFormProps) {
  const isCreate = !service;

  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors, isDirty, isSubmitSuccessful, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      code: service?.code ?? '',
      name: service?.name ?? '',
      description: service?.description ?? '',
      category: service?.category ?? 'Consultation',
      durationMinutes: service?.durationMinutes ?? 15,
      qualifiedRoleIds: service?.qualifiedRoleIds ?? [],
      requiredRoomKind: service?.requiredRoomKind ?? 'Consultation',
      taxClassId: service?.taxClassId ?? '',
      isBookableOnline: service?.isBookableOnline ?? true,
    },
  });

  async function onSubmit(values: FormValues) {
    try {
      await onSave({ ...values, code: isCreate ? values.code : undefined });
    } catch (error) {
      if (error instanceof ApiValidationError) {
        applyValidationErrors(error, setError, { fieldMap: FIELD_MAP, knownFields: KNOWN_FIELDS });
      } else {
        setError('root', { type: 'server', message: describeUnexpectedError(error) });
      }
    }
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex max-w-lg flex-col gap-4">
      <UnsavedChangesGuard when={isDirty && !isSubmitSuccessful} />

      {errors.root && (
        <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
          {errors.root.message}
        </p>
      )}
      {isSubmitSuccessful && (
        <p role="status" aria-live="polite" className="text-sm font-semibold text-ink">
          Saved.
        </p>
      )}

      <div className="flex flex-col gap-1.5">
        <label htmlFor="service-code" className="text-sm font-semibold text-ink">
          Code
        </label>
        <input
          id="service-code"
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
        <label htmlFor="service-name" className="text-sm font-semibold text-ink">
          Name
        </label>
        <input
          id="service-name"
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
        <label htmlFor="service-description" className="text-sm font-semibold text-ink">
          Description
        </label>
        <textarea
          id="service-description"
          rows={3}
          {...register('description')}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
        />
      </div>

      <div className="flex flex-wrap gap-3">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="service-category" className="text-sm font-semibold text-ink">
            Category
          </label>
          <select
            id="service-category"
            {...register('category')}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
          >
            {SERVICE_CATEGORIES.map((c) => (
              <option key={c} value={c}>
                {c}
              </option>
            ))}
          </select>
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="service-duration" className="text-sm font-semibold text-ink">
            Duration (minutes)
          </label>
          <input
            id="service-duration"
            type="number"
            step={5}
            min={5}
            max={480}
            {...register('durationMinutes', { valueAsNumber: true })}
            className="w-28 rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
          />
          {errors.durationMinutes && (
            <p role="alert" className="text-xs font-semibold text-danger-ink">
              In 5-minute steps, 5–480.
            </p>
          )}
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="service-room-kind" className="text-sm font-semibold text-ink">
            Required room
          </label>
          <select
            id="service-room-kind"
            {...register('requiredRoomKind')}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
          >
            {ROOM_KINDS.map((k) => (
              <option key={k} value={k}>
                {k}
              </option>
            ))}
          </select>
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="service-tax-class" className="text-sm font-semibold text-ink">
            Tax class
          </label>
          <select
            id="service-tax-class"
            {...register('taxClassId')}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
          >
            <option value="">Choose…</option>
            {(taxClasses.data ?? []).map((tc) => (
              <option key={tc.id} value={tc.id}>
                {tc.name}
              </option>
            ))}
          </select>
          {errors.taxClassId && (
            <p role="alert" className="text-xs font-semibold text-danger-ink">
              {errors.taxClassId.message}
            </p>
          )}
        </div>
      </div>

      <Controller
        control={control}
        name="qualifiedRoleIds"
        render={({ field }) => (
          <fieldset className="flex flex-col gap-1.5">
            <legend className="text-sm font-semibold text-ink">Qualified roles</legend>
            {(roleCatalogue.data?.roleIds ?? []).map((roleId) => (
              <label key={roleId} className="flex items-center gap-2 text-sm text-ink">
                <input
                  type="checkbox"
                  checked={field.value.includes(roleId)}
                  onChange={(event) => {
                    field.onChange(
                      event.target.checked
                        ? [...field.value, roleId]
                        : field.value.filter((r: string) => r !== roleId),
                    );
                  }}
                />
                {roleId}
              </label>
            ))}
            {errors.qualifiedRoleIds && (
              <p role="alert" className="text-xs font-semibold text-danger-ink">
                {errors.qualifiedRoleIds.message}
              </p>
            )}
          </fieldset>
        )}
      />

      <label className="flex items-center gap-2 text-sm text-ink">
        <input type="checkbox" {...register('isBookableOnline')} />
        Bookable online
      </label>

      <Button
        type="submit"
        disabled={isSubmitting || isPending}
        aria-busy={isSubmitting || isPending}
        className="w-fit"
      >
        {isSubmitting || isPending ? 'Saving…' : isCreate ? 'Create service' : 'Save'}
      </Button>
    </form>
  );
}
