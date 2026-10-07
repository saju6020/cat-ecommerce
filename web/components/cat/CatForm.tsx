'use client';

import Link from 'next/link';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { CAT_SEX_OPTIONS, DATE_OF_BIRTH_PRECISION_OPTIONS } from '@/lib/catEnumOptions';
import { submitWithServerErrors } from '@/lib/api/errors';

/** HO-CAT-001 command `propertyName` → this form's field name (HO-CAT-002 §11.8's "one table
 *  per form"). `RegisterCatCommand`/`UpdateCatProfileCommand` use PascalCase property names. */
const CAT_FORM_FIELD_MAP = {
  OwnerId: 'ownerId',
  Name: 'name',
  Species: 'species',
  Sex: 'sex',
  DateOfBirth: 'dateOfBirth',
  DateOfBirthPrecision: 'dateOfBirthPrecision',
  Breed: 'breed',
  ColourAndMarkings: 'colourAndMarkings',
} as const;
const CAT_FORM_KNOWN_FIELDS = Object.values(CAT_FORM_FIELD_MAP);
/** Customer mode has no owner field, so an `OwnerId` error (CustomerNotFound, AccountNotActive)
 *  has nowhere to show — it is left out of the known fields and so lands in the form-level
 *  `root` message instead of a hidden field. */
const CUSTOMER_KNOWN_FIELDS = CAT_FORM_KNOWN_FIELDS.filter((field) => field !== 'ownerId');

/**
 * `components/cat/CatForm.tsx` — US-CAT-01 (HO-CAT-002 §11.7). Register (`mode="register"`) and
 * edit (`mode="edit"`) share every field except `ownerId`, which only registration collects —
 * `UpdateCatProfileCommand` has no owner field (ownership only changes through
 * `TransferOwnershipDialog`, US-CAT-08).
 *
 * The zod schema mirrors `RegisterCatCommandValidator`/`UpdateCatProfileCommandValidator`
 * (HO-CAT-001 §11.3): name required; date of birth not required when the precision is
 * `Estimated`, but required — and blocking submission client-side — when it is `Exact`
 * (component tests U17/U18). `NEXT_PUBLIC_USE_MOCKS`-era owner IDs are free text: there is no
 * owner lookup yet (Accounts, §1, unbuilt), so `ownerId` is a plain "enter the customer ID"
 * field with a hint saying so, not a picker.
 *
 * NFR-USA-5: react-hook-form never clears field values on a failed validation (client- or
 * server-side) — a rejected submission leaves every entered value exactly as the user left it.
 */
const catFormSchema = z
  .object({
    ownerId: z.string().optional(),
    name: z
      .string()
      .trim()
      .min(1, 'Enter a name.')
      .max(100, 'Name must be 100 characters or fewer.'),
    species: z.string().trim().max(50, 'Species must be 50 characters or fewer.').optional(),
    sex: z.enum(CAT_SEX_OPTIONS as [string, ...string[]]),
    dateOfBirthPrecision: z.enum(DATE_OF_BIRTH_PRECISION_OPTIONS as [string, ...string[]]),
    dateOfBirth: z.string().optional(),
    breed: z.string().trim().max(100, 'Breed must be 100 characters or fewer.').optional(),
    colourAndMarkings: z
      .string()
      .trim()
      .max(200, 'Colour and markings must be 200 characters or fewer.')
      .optional(),
  })
  .refine((data) => data.dateOfBirthPrecision !== 'Exact' || !!data.dateOfBirth, {
    message: 'Enter a date of birth, or switch to an estimated age.',
    path: ['dateOfBirth'],
  });

export type CatFormValues = z.infer<typeof catFormSchema>;

export interface CatFormProps {
  mode: 'register' | 'edit';
  defaultValues?: Partial<CatFormValues>;
  /** Called with the validated values. Throw `ApiValidationError`/`ApiConflictError`/`ApiError`
   *  (as `lib/api/cats.ts`'s functions already do) and the form maps it per §11.8 itself. */
  onSubmit: (values: CatFormValues) => Promise<unknown>;
  isSubmitting?: boolean;
  submitLabel?: string;
  /** `customer` (HO-CAT-002-ADDCAT): the owner portal's own registration. No owner field — the
   *  server forces the owner to the signed-in customer (F19). Defaults to `staff`. */
  audience?: 'staff' | 'customer';
  /** Renders a Cancel link beside the submit button. */
  cancelHref?: string;
}

export function CatForm({
  mode,
  defaultValues,
  onSubmit,
  isSubmitting,
  submitLabel,
  audience = 'staff',
  cancelHref,
}: CatFormProps) {
  const {
    register,
    handleSubmit,
    watch,
    setError,
    formState: { errors },
  } = useForm<CatFormValues>({
    resolver: zodResolver(catFormSchema),
    defaultValues: {
      ownerId: '',
      name: '',
      species: 'Cat',
      sex: 'Unknown',
      dateOfBirthPrecision: 'Estimated',
      dateOfBirth: '',
      breed: '',
      colourAndMarkings: '',
      ...defaultValues,
    },
  });

  const precision = watch('dateOfBirthPrecision');

  return (
    <form
      onSubmit={handleSubmit((values) =>
        submitWithServerErrors(() => onSubmit(values), setError, {
          fieldMap: CAT_FORM_FIELD_MAP,
          knownFields: audience === 'customer' ? CUSTOMER_KNOWN_FIELDS : CAT_FORM_KNOWN_FIELDS,
        }),
      )}
      noValidate
      className="flex max-w-xl flex-col gap-5"
    >
      {errors.root && (
        <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
          {errors.root.message}
        </p>
      )}

      {mode === 'register' && audience === 'staff' && (
        <div className="flex flex-col gap-1.5">
          <label htmlFor="ownerId" className="text-sm font-semibold text-ink">
            Owner customer ID
          </label>
          <input
            id="ownerId"
            {...register('ownerId')}
            aria-describedby="ownerId-hint"
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          />
          <p id="ownerId-hint" className="text-xs text-ink-muted">
            Owner lookup isn&rsquo;t available yet — enter the customer&rsquo;s ID directly.
          </p>
        </div>
      )}

      <div className="flex flex-col gap-1.5">
        <label htmlFor="name" className="text-sm font-semibold text-ink">
          Name{' '}
          <span aria-hidden="true" className="text-danger-ink">
            *
          </span>
        </label>
        <input
          id="name"
          {...register('name')}
          required
          aria-invalid={!!errors.name}
          aria-describedby={errors.name ? 'name-error' : undefined}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        />
        {errors.name && (
          <p id="name-error" role="alert" className="text-xs font-semibold text-danger-ink">
            {errors.name.message}
          </p>
        )}
      </div>

      <div className="grid gap-5 sm:grid-cols-2">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="species" className="text-sm font-semibold text-ink">
            Species
          </label>
          <input
            id="species"
            {...register('species')}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="sex" className="text-sm font-semibold text-ink">
            Sex
          </label>
          <select
            id="sex"
            {...register('sex')}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          >
            {CAT_SEX_OPTIONS.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>
        </div>
      </div>

      <fieldset className="flex flex-col gap-2">
        <legend className="text-sm font-semibold text-ink">Date of birth</legend>
        <div className="flex gap-4 text-sm text-ink">
          {DATE_OF_BIRTH_PRECISION_OPTIONS.map((option) => (
            <label key={option} className="flex items-center gap-2">
              <input type="radio" value={option} {...register('dateOfBirthPrecision')} />
              {option === 'Exact' ? 'Exact date' : 'Estimated age'}
            </label>
          ))}
        </div>

        {precision === 'Exact' && (
          <div className="flex flex-col gap-1.5">
            <label htmlFor="dateOfBirth" className="sr-only">
              Date of birth
            </label>
            <input
              id="dateOfBirth"
              type="date"
              {...register('dateOfBirth')}
              aria-invalid={!!errors.dateOfBirth}
              aria-describedby={errors.dateOfBirth ? 'dateOfBirth-error' : undefined}
              className="w-fit rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
            />
            {errors.dateOfBirth && (
              <p
                id="dateOfBirth-error"
                role="alert"
                className="text-xs font-semibold text-danger-ink"
              >
                {errors.dateOfBirth.message}
              </p>
            )}
          </div>
        )}
      </fieldset>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="breed" className="text-sm font-semibold text-ink">
          Breed
        </label>
        <input
          id="breed"
          {...register('breed')}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        />
      </div>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="colourAndMarkings" className="text-sm font-semibold text-ink">
          Colour and markings
        </label>
        <input
          id="colourAndMarkings"
          {...register('colourAndMarkings')}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        />
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <Button type="submit" disabled={isSubmitting} aria-busy={isSubmitting}>
          {isSubmitting
            ? 'Saving…'
            : (submitLabel ?? (mode === 'register' ? 'Register cat' : 'Save changes'))}
        </Button>
        {cancelHref && (
          <Link
            href={cancelHref}
            className="rounded-[var(--radius-sm)] text-sm font-semibold text-ink underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          >
            Cancel
          </Link>
        )}
      </div>
    </form>
  );
}
