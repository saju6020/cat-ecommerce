'use client';

import * as React from 'react';
import Link from 'next/link';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { ListboxPicker } from '@/components/formulary/ListboxPicker';
import { Banner, TextAreaField, TextField } from '@/components/inventory/fields';
import { NO_VISIT_MESSAGE, VisitContextBar } from '@/components/prescriptions/VisitContextBar';
import { toast } from '@/components/ui/use-toast';
import type { DietProduct } from '@/lib/api/diet-authorisations';
import {
  applyValidationErrors,
  PHARMACY_CODE_MESSAGES,
  describePharmacyError,
  pharmacyCode,
} from '@/lib/api/errors';
import { ApiValidationError } from '@/lib/api/client';
import { addDaysIso } from '@/lib/format/appointment';
import { formatDay } from '@/lib/format/pharmacy';
import { todayInZone } from '@/lib/format/time';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useCat } from '@/lib/hooks/useCat';
import { useAuthoriseDiet, useDietProducts } from '@/lib/hooks/useDietAuthorisations';
import { useStaffRoles } from '@/lib/hooks/useStaffRoles';
import { useVisit } from '@/lib/hooks/useVisit';

/**
 * ⭐ `DietAuthorisationForm` — HO-PHM-002 §8.6 / §11.5 (US-PHM-08). A vet authorises a therapeutic diet
 * FROM AN OPEN VISIT: pick the food (the server lists the foods that need an authorisation), the dates
 * (from today by default; 30 / 90 / 180-day quick picks for the end) and optional notes. A second active
 * authorisation for the same food is refused — *"{cat} already has an active authorisation for this
 * food — extend it instead."* with a link to the cat's diet tab. Exported for HO-EMR-002's consultation
 * screen; it never navigates on its own.
 */
const schema = z
  .object({
    productId: z.string().min(1, 'Choose the food.'),
    validFrom: z.string().min(1, 'Enter the first day.'),
    validTo: z.string().min(1, 'Enter the last day.'),
    notes: z.string().max(500, 'Keep the notes to 500 characters or fewer.'),
  })
  .superRefine((values, context) => {
    if (values.validFrom && values.validTo && values.validTo < values.validFrom)
      context.addIssue({
        code: 'custom',
        path: ['validTo'],
        message: 'The last day cannot be before the first day.',
      });
  });
type Values = z.infer<typeof schema>;

const FIELD_MAP = {
  ProductId: 'productId',
  ValidFrom: 'validFrom',
  ValidTo: 'validTo',
  Notes: 'notes',
} as const;
const QUICK_PICKS = [30, 90, 180];

export function DietAuthorisationForm({
  catId,
  visitId,
  onAuthorised,
}: {
  catId: string;
  visitId: string | null;
  onAuthorised: (authorisationId: string) => void;
}) {
  const roles = useStaffRoles();
  const zone = useBranchTimeZone();
  const cat = useCat(catId);
  const visit = useVisit(visitId ?? '');
  const authorise = useAuthoriseDiet(catId);
  const today = todayInZone(zone);
  const [term, setTerm] = React.useState('');
  const products = useDietProducts(term);
  const [picked, setPicked] = React.useState<DietProduct | null>(null);
  const [alreadyActive, setAlreadyActive] = React.useState(false);
  const [closed, setClosed] = React.useState(false);
  const [leaving, setLeaving] = React.useState(false);
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { productId: '', validFrom: today, validTo: '', notes: '' },
  });
  const { register, handleSubmit, setValue, setError, watch, formState } = form;
  const from = watch('validFrom');
  const catName = cat.data?.name ?? 'This cat';

  if (roles.ready && !roles.has('Vet')) return <PermissionDeniedState />;
  if (!visitId)
    return (
      <Banner tone="warning" role="alert">
        {NO_VISIT_MESSAGE}
      </Banner>
    );

  return (
    <div className="flex flex-col gap-4">
      {!leaving && <UnsavedChangesGuard when={formState.isDirty} />}
      <VisitContextBar visit={visit.data} closed={closed} />
      {!closed && visit.data?.status !== 'Finalised' && (
        <form
          noValidate
          aria-label="Authorise a diet"
          onSubmit={handleSubmit(async (values) => {
            setAlreadyActive(false);
            try {
              const created = await authorise.mutateAsync({
                visitId,
                productId: values.productId,
                validFrom: values.validFrom,
                validTo: values.validTo,
                notes: values.notes.trim() || null,
              });
              setLeaving(true); // stand the unsaved-changes guard down BEFORE the caller navigates
              toast({ title: 'Diet authorised', description: picked?.name });
              onAuthorised(created.authorisationId);
            } catch (error) {
              if (pharmacyCode(error, 'DietAuthorisationAlreadyActive')) setAlreadyActive(true);
              else if (pharmacyCode(error, 'VisitNotOpen')) setClosed(true);
              else if (error instanceof ApiValidationError)
                applyValidationErrors(error, setError, {
                  fieldMap: FIELD_MAP,
                  knownFields: Object.values(FIELD_MAP),
                  codeMessages: PHARMACY_CODE_MESSAGES,
                });
              else setError('root', { type: 'server', message: describePharmacyError(error) });
            }
          })}
          className="flex max-w-2xl flex-col gap-4 rounded-[var(--radius-md)] border border-border bg-surface p-4"
        >
          {formState.errors.root?.message && (
            <p role="alert" className="text-sm font-semibold text-danger-ink">
              {formState.errors.root.message}
            </p>
          )}
          {alreadyActive && (
            <Banner tone="warning" role="alert">
              {catName} already has an active authorisation for this food — extend it instead.{' '}
              <Link
                href={`/staff/cats/${catId}/diet-authorisations`}
                className="font-semibold underline"
              >
                Go to the diet authorisations
              </Link>
            </Banner>
          )}
          <div className="flex flex-col gap-1">
            <ListboxPicker<DietProduct>
              id="da-product"
              label="Food"
              required
              items={products.data ?? []}
              isLoading={products.isLoading}
              isError={products.isError}
              getKey={(product) => product.productId}
              getLabel={(product) => product.name}
              renderDetail={(product) => <span>{product.brand}</span>}
              onSearch={setTerm}
              onSelect={(product) => {
                setPicked(product);
                setValue('productId', product.productId, {
                  shouldDirty: true,
                  shouldValidate: formState.isSubmitted,
                });
              }}
              minLength={2}
              clearOnSelect={false}
              noun="foods"
              hint="Only foods that need a vet’s authorisation are listed."
              error={formState.errors.productId?.message}
            />
            {picked && (
              <p className="text-xs text-ink-muted">
                Chosen: {picked.name} ({picked.brand})
              </p>
            )}
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <TextField
              id="da-from"
              type="date"
              label="Valid from"
              required
              error={formState.errors.validFrom?.message}
              {...register('validFrom')}
            />
            <div className="flex flex-col gap-2">
              <TextField
                id="da-to"
                type="date"
                label="Valid to"
                required
                error={formState.errors.validTo?.message}
                {...register('validTo')}
              />
              <div
                role="group"
                aria-label="Quick picks for the last day"
                className="flex flex-wrap gap-2"
              >
                {QUICK_PICKS.map((days) => (
                  <Button
                    key={days}
                    variant="secondary"
                    size="sm"
                    onClick={() =>
                      setValue('validTo', addDaysIso(from || today, days), {
                        shouldDirty: true,
                        shouldValidate: formState.isSubmitted,
                      })
                    }
                  >
                    {days} days
                  </Button>
                ))}
              </div>
              <p className="text-xs text-ink-muted">
                {watch('validTo')
                  ? `Runs until ${formatDay(watch('validTo'))}.`
                  : 'The clinic sets the longest an authorisation can run; the server tells you if the end date is too far.'}
              </p>
            </div>
          </div>
          <TextAreaField
            id="da-notes"
            label="Notes (optional)"
            rows={2}
            maxLength={500}
            error={formState.errors.notes?.message}
            {...register('notes')}
          />
          <div>
            <Button
              type="submit"
              disabled={formState.isSubmitting}
              aria-busy={formState.isSubmitting}
            >
              {formState.isSubmitting ? 'Authorising…' : 'Authorise diet'}
            </Button>
          </div>
        </form>
      )}
    </div>
  );
}
