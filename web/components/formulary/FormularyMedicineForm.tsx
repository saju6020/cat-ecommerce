'use client';

import * as React from 'react';
import { Controller, useFieldArray, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { QuantityInput, ClinicalText } from '@/components/common/Quantity';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import {
  Banner,
  SelectField,
  TextAreaField,
  TextField,
  CheckboxField,
} from '@/components/inventory/fields';
import type { FormularyMedicineDetail, FormularyMedicineInput } from '@/lib/api/formulary';
import { DISPENSING_UNITS, STRENGTH_UNITS, unitLabel } from '@/lib/format/units';
import { DOSING_BASIS_LABELS, MEDICINE_FORM_LABELS, ROUTE_LABELS } from '@/lib/format/pharmacy';
import { positiveDecimal, submitPharmacyForm, toDecimal } from '@/lib/validation/pharmacy';
import { ChipsField } from './ChipsField';
import { InteractionRows, type InteractionRowValue } from './InteractionRows';
import { LinkedSkuPicker, type LinkedSku } from './LinkedSkuPicker';

/**
 * `FormularyMedicineForm` — HO-PHM-002 §11.7, ADR-FE-067. The whole medicine in one form, saved with
 * `expectedVersion`. The controlled flag and activation are deliberately NOT fields here: each is its
 * own confirmed action (`ControlledToggle`, `ActivationToggle`). A per-kg medicine needs its min/max
 * mg/kg (the range is data the server's safety check reads — the form only checks it is entered).
 * Values survive every error and the idle lock (NFR-USA-5); a `409` says someone else changed the
 * medicine and offers Reload without discarding what was typed.
 */
const interactionSchema = z
  .object({
    kind: z.enum(['medicine', 'term']),
    medicineId: z.string(),
    medicineName: z.string(),
    term: z.string().trim().max(100, 'Keep the term to 100 characters or fewer.'),
    severity: z.string().min(1, 'Choose how serious this interaction is.'),
    note: z
      .string()
      .trim()
      .min(1, 'Add a short note about the interaction.')
      .max(500, 'Keep the note to 500 characters or fewer.'),
  })
  .superRefine((row, context) => {
    if (row.kind === 'medicine' && !row.medicineId)
      context.addIssue({
        code: 'custom',
        path: ['medicineId'],
        message: 'Choose the other medicine.',
      });
    if (row.kind === 'term' && !row.term)
      context.addIssue({ code: 'custom', path: ['term'], message: 'Enter the term.' });
  });

const schema = z
  .object({
    code: z
      .string()
      .trim()
      .min(1, 'Enter a code.')
      .max(30, 'Keep the code to 30 characters or fewer.')
      .regex(/^[A-Z0-9-]+$/, 'Use capital letters, digits and hyphens only.'),
    name: z
      .string()
      .trim()
      .min(1, 'Enter the name.')
      .max(200, 'Keep the name to 200 characters or fewer.'),
    ingredients: z.array(z.string()).min(1, 'List one to five active ingredients.').max(5),
    classes: z.array(z.string()).max(10),
    form: z.string().min(1, 'Choose the form.'),
    strengthAmount: positiveDecimal('the strength'),
    strengthUnit: z.string().min(1, 'Choose the strength unit.'),
    perQuantity: positiveDecimal('the quantity it is per'),
    perUnit: z.string().min(1, 'Choose the unit it is per.'),
    defaultRoute: z.string().min(1, 'Choose the default route.'),
    allowedRoutes: z.array(z.string()).min(1, 'Allow at least one route.'),
    dosingBasis: z.string().min(1, 'Choose the dosing basis.'),
    minMgPerKg: z.string().trim(),
    maxMgPerKg: z.string().trim(),
    contraindications: z.string().max(2000, 'Keep contraindications to 2000 characters or fewer.'),
    dispensingUnit: z.string().min(1, 'Choose the dispensing unit.'),
    linkedSkus: z
      .array(z.object({ skuId: z.string(), productName: z.string(), variantLabel: z.string() }))
      .max(10),
    interactions: z.array(interactionSchema).max(50),
  })
  .superRefine((values, context) => {
    if (values.dosingBasis !== 'PerKg') return;
    const decimal = /^\d+(\.\d{1,4})?$/;
    const check = (key: 'minMgPerKg' | 'maxMgPerKg', label: string) => {
      const value = values[key];
      if (!decimal.test(value) || Number(value) <= 0)
        context.addIssue({
          code: 'custom',
          path: [key],
          message: `Enter the ${label} dose in mg/kg.`,
        });
    };
    check('minMgPerKg', 'minimum');
    check('maxMgPerKg', 'maximum');
    if (
      decimal.test(values.minMgPerKg) &&
      decimal.test(values.maxMgPerKg) &&
      Number(values.minMgPerKg) >= Number(values.maxMgPerKg)
    )
      context.addIssue({
        code: 'custom',
        path: ['maxMgPerKg'],
        message: 'The maximum must be higher than the minimum.',
      });
  });

export type FormularyFormValues = z.infer<typeof schema>;

const FIELD_MAP = {
  Code: 'code',
  Name: 'name',
  ActiveIngredients: 'ingredients',
  DrugClasses: 'classes',
  Form: 'form',
  'Strength.Amount': 'strengthAmount',
  'Strength.Unit': 'strengthUnit',
  'Strength.PerQuantity': 'perQuantity',
  'Strength.PerUnit': 'perUnit',
  DefaultRoute: 'defaultRoute',
  AllowedRoutes: 'allowedRoutes',
  DosingBasis: 'dosingBasis',
  DoseRange: 'minMgPerKg',
  'DoseRange.MinMgPerKg': 'minMgPerKg',
  'DoseRange.MaxMgPerKg': 'maxMgPerKg',
  Contraindications: 'contraindications',
  DispensingUnit: 'dispensingUnit',
  LinkedSkuIds: 'linkedSkus',
  Interactions: 'interactions',
} as const;
const KNOWN_FIELDS = Object.values(FIELD_MAP);

function defaults(initial?: FormularyMedicineDetail): FormularyFormValues {
  if (!initial) {
    return {
      code: '',
      name: '',
      ingredients: [],
      classes: [],
      form: '',
      strengthAmount: '',
      strengthUnit: '',
      perQuantity: '1',
      perUnit: '',
      defaultRoute: '',
      allowedRoutes: [],
      dosingBasis: 'PerKg',
      minMgPerKg: '',
      maxMgPerKg: '',
      contraindications: '',
      dispensingUnit: '',
      linkedSkus: [],
      interactions: [],
    };
  }
  return {
    code: initial.code,
    name: initial.name,
    ingredients: initial.activeIngredients,
    classes: initial.drugClasses,
    form: initial.form,
    strengthAmount: initial.strength.amount.amount,
    strengthUnit: initial.strength.amount.unit,
    perQuantity: initial.strength.per.amount,
    perUnit: initial.strength.per.unit,
    defaultRoute: initial.defaultRoute,
    allowedRoutes: initial.allowedRoutes,
    dosingBasis: initial.dosingBasis,
    minMgPerKg: initial.doseRange?.minMgPerKg ?? '',
    maxMgPerKg: initial.doseRange?.maxMgPerKg ?? '',
    contraindications: initial.contraindications,
    dispensingUnit: initial.dispensingUnit,
    linkedSkus: initial.linkedSkus,
    interactions: initial.interactions.map((row) => ({
      kind: row.interactsWithMedicineId ? 'medicine' : 'term',
      medicineId: row.interactsWithMedicineId ?? '',
      medicineName: row.interactsWithMedicineName ?? '',
      term: row.interactsWithTerm ?? '',
      severity: row.severity,
      note: row.note,
    })),
  };
}

function toInput(values: FormularyFormValues, expectedVersion: number): FormularyMedicineInput {
  return {
    code: values.code,
    name: values.name,
    activeIngredients: values.ingredients,
    drugClasses: values.classes,
    form: values.form,
    strength: {
      amount: toDecimal(values.strengthAmount),
      unit: values.strengthUnit,
      perQuantity: toDecimal(values.perQuantity),
      perUnit: values.perUnit,
    },
    defaultRoute: values.defaultRoute,
    allowedRoutes: values.allowedRoutes,
    dosingBasis: values.dosingBasis,
    doseRange:
      values.dosingBasis === 'PerKg'
        ? { minMgPerKg: toDecimal(values.minMgPerKg), maxMgPerKg: toDecimal(values.maxMgPerKg) }
        : null,
    contraindications: values.contraindications,
    interactions: values.interactions.map((row) => ({
      interactsWithMedicineId: row.kind === 'medicine' ? row.medicineId : null,
      interactsWithTerm: row.kind === 'term' ? row.term : null,
      severity: row.severity,
      note: row.note,
    })),
    dispensingUnit: values.dispensingUnit,
    linkedSkuIds: values.linkedSkus.map((sku) => sku.skuId),
    expectedVersion,
  };
}

export interface FormularyMedicineFormProps {
  /** Present when editing; absent when creating. */
  initial?: FormularyMedicineDetail;
  onSubmit: (input: FormularyMedicineInput) => Promise<unknown>;
  /** Called with what `onSubmit` returned once it succeeded — the page navigates here (creating), after the guard stands down. */
  onSaved?: (result: unknown) => void;
  /** After a `409`: refetch the medicine and re-key the form. */
  onReload?: () => void;
}

export function FormularyMedicineForm({
  initial,
  onSubmit,
  onSaved,
  onReload,
}: FormularyMedicineFormProps) {
  const isEdit = Boolean(initial);
  const [conflict, setConflict] = React.useState(false);
  const [leaving, setLeaving] = React.useState(false);
  const form = useForm<FormularyFormValues>({
    resolver: zodResolver(schema),
    defaultValues: defaults(initial),
  });
  const {
    control,
    register,
    handleSubmit,
    setError,
    watch,
    setValue,
    formState: { errors, isDirty, isSubmitting, isSubmitSuccessful },
  } = form;
  const interactions = useFieldArray({ control, name: 'interactions' });
  const values = watch();
  const perKg = values.dosingBasis === 'PerKg';

  const preview =
    values.strengthAmount && values.strengthUnit && values.perQuantity && values.perUnit
      ? `${values.strengthAmount} ${unitLabel(values.strengthUnit, values.strengthAmount)} per ${values.perQuantity} ${unitLabel(values.perUnit, values.perQuantity)}`
      : null;

  const toggleRoute = (route: string, checked: boolean) => {
    const next = checked
      ? [...values.allowedRoutes, route]
      : values.allowedRoutes.filter((r) => r !== route);
    setValue('allowedRoutes', next, {
      shouldDirty: true,
      shouldValidate: isSubmitting || !!errors.allowedRoutes,
    });
  };

  return (
    <form
      noValidate
      aria-label={isEdit ? 'Edit medicine' : 'New medicine'}
      onSubmit={handleSubmit((submitted) =>
        submitPharmacyForm(
          async () => {
            const result = await onSubmit(toInput(submitted, initial?.version ?? 0));
            if (onSaved) {
              setLeaving(true); // stand the unsaved-changes guard down BEFORE the page navigates
              onSaved(result);
            }
          },
          setError,
          {
            fieldMap: FIELD_MAP,
            knownFields: KNOWN_FIELDS,
            onConflict: () => setConflict(true),
          },
        ),
      )}
      className="flex max-w-3xl flex-col gap-5"
    >
      {!leaving && <UnsavedChangesGuard when={isDirty && !isSubmitSuccessful} />}
      {conflict && (
        <Banner tone="warning" role="alert" className="flex flex-wrap items-center gap-3">
          <span>Someone else changed this medicine. Reload to see their changes.</span>
          {onReload && (
            <Button
              variant="secondary"
              size="sm"
              onClick={() => {
                setConflict(false);
                onReload();
              }}
            >
              Reload
            </Button>
          )}
          <span className="text-xs">What you typed is still here until you reload.</span>
        </Banner>
      )}
      {errors.root?.message && !conflict && (
        <p role="alert" className="text-sm font-semibold text-danger-ink">
          {errors.root.message}
        </p>
      )}

      <fieldset className="grid gap-4 sm:grid-cols-2">
        <legend className="mb-2 text-sm font-bold uppercase tracking-wide text-ink-muted">
          Identity
        </legend>
        <TextField
          id="fm-code"
          label="Code"
          required
          disabled={isEdit}
          hint={isEdit ? 'The code cannot be changed.' : 'Capital letters, digits and hyphens.'}
          error={errors.code?.message}
          {...register('code')}
        />
        <TextField
          id="fm-name"
          label="Name"
          required
          error={errors.name?.message}
          {...register('name')}
        />
        <Controller
          control={control}
          name="ingredients"
          render={({ field }) => (
            <ChipsField
              id="fm-ingredients"
              label="Active ingredients"
              required
              values={field.value}
              onChange={(next) => field.onChange(next)}
              max={5}
              hint="One to five. Allergy checks compare these with a cat's recorded allergies."
              error={errors.ingredients?.message}
            />
          )}
        />
        <Controller
          control={control}
          name="classes"
          render={({ field }) => (
            <ChipsField
              id="fm-classes"
              label="Drug classes"
              values={field.value}
              onChange={(next) => field.onChange(next)}
              max={10}
              hint="For example penicillin or nsaid."
              error={errors.classes?.message}
            />
          )}
        />
      </fieldset>

      <fieldset className="grid gap-4 sm:grid-cols-2">
        <legend className="mb-2 text-sm font-bold uppercase tracking-wide text-ink-muted">
          Strength and form
        </legend>
        <SelectField
          id="fm-form"
          label="Form"
          required
          error={errors.form?.message}
          {...register('form')}
        >
          <option value="">Choose form</option>
          {Object.entries(MEDICINE_FORM_LABELS).map(([value, text]) => (
            <option key={value} value={value}>
              {text}
            </option>
          ))}
        </SelectField>
        <SelectField
          id="fm-dispensing-unit"
          label="Dispensing unit"
          required
          hint="What a supply is counted in."
          error={errors.dispensingUnit?.message}
          {...register('dispensingUnit')}
        >
          <option value="">Choose unit</option>
          {DISPENSING_UNITS.map((unit) => (
            <option key={unit} value={unit}>
              {unitLabel(unit, '2')}
            </option>
          ))}
        </SelectField>
        <Controller
          control={control}
          name="strengthAmount"
          render={({ field }) => (
            <QuantityInput
              id="fm-strength"
              label="Strength"
              required
              amount={field.value}
              unit={values.strengthUnit}
              units={[...STRENGTH_UNITS]}
              onChange={(next) => {
                field.onChange(next.amount);
                setValue('strengthUnit', next.unit, { shouldDirty: true });
              }}
              error={errors.strengthAmount?.message ?? errors.strengthUnit?.message}
            />
          )}
        />
        <Controller
          control={control}
          name="perQuantity"
          render={({ field }) => (
            <QuantityInput
              id="fm-per"
              label="Per"
              required
              amount={field.value}
              unit={values.perUnit}
              units={[...DISPENSING_UNITS]}
              onChange={(next) => {
                field.onChange(next.amount);
                setValue('perUnit', next.unit, { shouldDirty: true });
              }}
              error={errors.perQuantity?.message ?? errors.perUnit?.message}
            />
          )}
        />
        <p className="text-sm text-ink-muted sm:col-span-2" aria-live="polite">
          Strength preview:{' '}
          {preview ? (
            <ClinicalText className="font-semibold text-ink">{preview}</ClinicalText>
          ) : (
            'enter the strength and what it is per'
          )}
        </p>
      </fieldset>

      <fieldset className="flex flex-col gap-4">
        <legend className="mb-2 text-sm font-bold uppercase tracking-wide text-ink-muted">
          Routes and dosing
        </legend>
        <div role="group" aria-labelledby="fm-routes-label" className="flex flex-col gap-2">
          <p id="fm-routes-label" className="text-sm font-semibold text-ink">
            Allowed routes{' '}
            <span aria-hidden="true" className="text-danger-ink">
              *
            </span>
          </p>
          <div className="grid gap-2 sm:grid-cols-3">
            {Object.entries(ROUTE_LABELS).map(([route, text]) => (
              <CheckboxField
                key={route}
                id={`fm-route-${route}`}
                label={text}
                checked={values.allowedRoutes.includes(route)}
                onChange={(event) => toggleRoute(route, event.target.checked)}
              />
            ))}
          </div>
          {errors.allowedRoutes?.message && (
            <p role="alert" className="text-xs font-semibold text-danger-ink">
              {errors.allowedRoutes.message}
            </p>
          )}
        </div>
        <SelectField
          id="fm-default-route"
          label="Default route"
          required
          error={errors.defaultRoute?.message}
          {...register('defaultRoute')}
        >
          <option value="">Choose route</option>
          {Object.entries(ROUTE_LABELS).map(([route, text]) => (
            <option key={route} value={route}>
              {text}
            </option>
          ))}
        </SelectField>
        <SelectField
          id="fm-basis"
          label="Dosing basis"
          required
          error={errors.dosingBasis?.message}
          {...register('dosingBasis')}
        >
          {Object.entries(DOSING_BASIS_LABELS).map(([value, text]) => (
            <option key={value} value={value}>
              {text}
            </option>
          ))}
        </SelectField>
        {perKg && (
          <div className="grid gap-4 sm:grid-cols-2">
            <Controller
              control={control}
              name="minMgPerKg"
              render={({ field }) => (
                <QuantityInput
                  id="fm-min"
                  label="Minimum dose"
                  required
                  amount={field.value}
                  unit="mg/kg"
                  fixedUnit="mg/kg"
                  onChange={(next) => field.onChange(next.amount)}
                  hint="The server compares each prescription's dose with this range."
                  error={errors.minMgPerKg?.message}
                />
              )}
            />
            <Controller
              control={control}
              name="maxMgPerKg"
              render={({ field }) => (
                <QuantityInput
                  id="fm-max"
                  label="Maximum dose"
                  required
                  amount={field.value}
                  unit="mg/kg"
                  fixedUnit="mg/kg"
                  onChange={(next) => field.onChange(next.amount)}
                  error={errors.maxMgPerKg?.message}
                />
              )}
            />
          </div>
        )}
        <TextAreaField
          id="fm-contra"
          label="Contraindications"
          rows={4}
          hint="Shown to the vet next to the safety check."
          error={errors.contraindications?.message}
          {...register('contraindications')}
        />
      </fieldset>

      <fieldset className="flex flex-col gap-3">
        <legend className="mb-2 text-sm font-bold uppercase tracking-wide text-ink-muted">
          Stock
        </legend>
        <Controller
          control={control}
          name="linkedSkus"
          render={({ field }) => (
            <LinkedSkuPicker
              medicineId={initial?.id ?? null}
              linked={field.value as LinkedSku[]}
              onChange={(next) => field.onChange(next)}
              error={errors.linkedSkus?.message as string | undefined}
            />
          )}
        />
      </fieldset>

      <fieldset className="flex flex-col gap-3">
        <legend className="mb-2 text-sm font-bold uppercase tracking-wide text-ink-muted">
          Interactions
        </legend>
        <InteractionRows
          rows={values.interactions as InteractionRowValue[]}
          keys={interactions.fields.map((field) => field.id)}
          selfId={initial?.id}
          errors={errors.interactions as never}
          onAdd={() =>
            interactions.append({
              kind: 'medicine',
              medicineId: '',
              medicineName: '',
              term: '',
              severity: '',
              note: '',
            })
          }
          onRemove={interactions.remove}
          onPatch={(index, patch) => {
            const current = form.getValues(`interactions.${index}`);
            interactions.update(index, { ...current, ...patch });
          }}
          register={register as never}
        />
      </fieldset>

      <div className="flex flex-wrap items-center gap-3">
        <Button type="submit" disabled={isSubmitting} aria-busy={isSubmitting}>
          {isSubmitting ? 'Saving…' : isEdit ? 'Save changes' : 'Create medicine'}
        </Button>
        <span className="text-xs text-ink-muted">
          {isEdit
            ? 'The controlled flag and activation are changed separately, with their own confirmation.'
            : 'You can mark it controlled after it is created.'}
        </span>
      </div>
    </form>
  );
}
