'use client';

import * as React from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { Banner, SelectField, TextAreaField, TextField } from '@/components/inventory/fields';
import { countryOptions } from '@/components/inventory/countries';
import { INVENTORY_CODE_MESSAGES, submitWithServerErrors, toFieldName } from '@/lib/api/errors';
import { FALLBACK_CURRENCY_CODE } from '@/lib/shop/currency';
import type { SupplierDetail, SupplierInput } from '@/lib/api/procurement-suppliers';

const digits = (label: string, required: boolean) =>
  z
    .string()
    .trim()
    .refine((v) => (required ? /^\d+$/.test(v) : v === '' || /^\d+$/.test(v)), {
      message: `${label} must be a whole number of days.`,
    });

const schema = z.object({
  code: z.string().trim().max(20, 'A code is at most 20 characters.'),
  name: z.string().trim().min(1, 'Enter the supplier name.').max(200),
  countryCode: z.string().min(1, 'Choose a country.'),
  contactName: z.string().trim().max(200),
  phone: z.string().trim().max(30),
  email: z.string().trim().max(256),
  address: z.string().trim().max(500),
  currencyCode: z
    .string()
    .trim()
    .regex(/^[A-Za-z]{3}$/, 'Use a three-letter currency code, such as BDT or USD.'),
  leadTimeDays: digits('Lead time', true),
  paymentTerms: z.string().trim().min(1, 'Describe the payment terms.').max(500),
  paymentTermDays: digits('Payment term days', false),
  notes: z.string().trim().max(1000),
});
type Values = z.infer<typeof schema>;

const PROPERTY_NAMES = [
  'Code', 'Name', 'CountryCode', 'ContactName', 'Phone', 'Email', 'Address', 'CurrencyCode', 'LeadTimeDays',
  'PaymentTerms', 'PaymentTermDays', 'Notes',
] as const;
const FIELD_MAP = Object.fromEntries(PROPERTY_NAMES.map((n) => [n, toFieldName(n)])) as Record<string, keyof Values>;
const KNOWN_FIELDS = Object.values(FIELD_MAP);

function defaultsFor(initial?: SupplierDetail): Values {
  return {
    code: initial?.code ?? '',
    name: initial?.name ?? '',
    countryCode: initial?.countryCode ?? '',
    contactName: initial?.contactName ?? '',
    phone: initial?.phone ?? '',
    email: initial?.email ?? '',
    address: initial?.address ?? '',
    currencyCode: initial?.currencyCode ?? FALLBACK_CURRENCY_CODE,
    leadTimeDays: String(initial?.leadTimeDays ?? 14),
    paymentTerms: initial?.paymentTerms ?? '',
    paymentTermDays: initial?.paymentTermDays === null || initial === undefined ? '' : String(initial.paymentTermDays),
    notes: initial?.notes ?? '',
  };
}

const orNull = (v: string) => (v.trim() === '' ? null : v.trim());

/**
 * `components/procurement/SupplierForm.tsx` — HO-IMP-002 §11.2, US-IMP-01. Code (fixed once
 * created), name, country (a `<select>` of ISO names, value = alpha-2), contact, currency, lead time
 * in days, payment terms (text) and term days, notes. A `400` lands on its field and every typed
 * value is kept (NFR-USA-5). Active/inactive is a separate confirmed action on the detail page.
 */
export function SupplierForm({
  initial,
  onSubmit,
  isPending,
  submitLabel,
}: {
  initial?: SupplierDetail;
  onSubmit: (values: SupplierInput & { code: string }) => Promise<unknown>;
  isPending: boolean;
  submitLabel: string;
}) {
  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors, isDirty, isSubmitSuccessful, isSubmitting },
  } = useForm<Values>({ resolver: zodResolver(schema), defaultValues: defaultsFor(initial) });
  const countries = React.useMemo(() => countryOptions(), []);
  const editing = !!initial;

  // Re-seed the form only when a DIFFERENT record is shown, or the form is untouched. A background
  // refetch of the same supplier must not wipe what the member of staff is typing.
  const seededIdRef = React.useRef(initial?.id);
  const dirtyRef = React.useRef(isDirty);
  dirtyRef.current = isDirty;
  React.useEffect(() => {
    const idChanged = seededIdRef.current !== initial?.id;
    if (idChanged || !dirtyRef.current) {
      seededIdRef.current = initial?.id;
      reset(defaultsFor(initial));
    }
  }, [initial, reset]);

  async function submit(values: Values) {
    if (!editing && values.code === '') {
      setError('code', { type: 'validate', message: 'Enter a supplier code, such as MERIDIAN.' });
      return;
    }
    const saved = await submitWithServerErrors(
      () =>
        onSubmit({
          code: values.code,
          name: values.name,
          countryCode: values.countryCode,
          contactName: orNull(values.contactName),
          phone: orNull(values.phone),
          email: orNull(values.email),
          address: orNull(values.address),
          currencyCode: values.currencyCode.toUpperCase(),
          leadTimeDays: parseInt(values.leadTimeDays, 10),
          paymentTerms: values.paymentTerms,
          paymentTermDays: values.paymentTermDays === '' ? null : parseInt(values.paymentTermDays, 10),
          notes: orNull(values.notes),
        }).then(() => true),
      setError,
      { fieldMap: FIELD_MAP, knownFields: KNOWN_FIELDS, codeMessages: INVENTORY_CODE_MESSAGES },
    );
    if (saved) reset(values);
  }

  return (
    <form onSubmit={handleSubmit(submit)} noValidate className="flex max-w-3xl flex-col gap-4">
      {/* Released when the submit STARTS, so a navigation made on success is not undone by the guard. */}
      <UnsavedChangesGuard when={isDirty && !isSubmitSuccessful && !isSubmitting} />
      {errors.root && <Banner tone="danger">{errors.root.message}</Banner>}
      <div className="grid gap-4 sm:grid-cols-2">
        <TextField
          id="supplier-code"
          label="Code"
          required
          disabled={editing}
          hint={editing ? 'A supplier code cannot be changed.' : 'Short and unique, such as MERIDIAN.'}
          error={errors.code?.message}
          {...register('code')}
        />
        <TextField id="supplier-name" label="Name" required error={errors.name?.message} {...register('name')} />
        <SelectField id="supplier-country" label="Country" required error={errors.countryCode?.message} {...register('countryCode')}>
          <option value="">Choose a country</option>
          {countries.map((c) => (
            <option key={c.value} value={c.value}>
              {c.label}
            </option>
          ))}
        </SelectField>
        <TextField
          id="supplier-currency"
          label="Currency"
          required
          maxLength={3}
          hint="Orders default to this currency."
          error={errors.currencyCode?.message}
          {...register('currencyCode')}
        />
        <TextField id="supplier-contact" label="Contact name" error={errors.contactName?.message} {...register('contactName')} />
        <TextField id="supplier-phone" label="Phone" type="tel" error={errors.phone?.message} {...register('phone')} />
        <TextField id="supplier-email" label="Email" type="email" error={errors.email?.message} {...register('email')} />
        <TextField
          id="supplier-lead"
          label="Lead time (days)"
          required
          inputMode="numeric"
          error={errors.leadTimeDays?.message}
          {...register('leadTimeDays')}
        />
        <TextField id="supplier-terms" label="Payment terms" required error={errors.paymentTerms?.message} {...register('paymentTerms')} />
        <TextField
          id="supplier-term-days"
          label="Payment term (days)"
          inputMode="numeric"
          hint="Optional — days until payment is due."
          error={errors.paymentTermDays?.message}
          {...register('paymentTermDays')}
        />
      </div>
      <TextAreaField id="supplier-address" label="Address" error={errors.address?.message} {...register('address')} />
      <TextAreaField id="supplier-notes" label="Notes" error={errors.notes?.message} {...register('notes')} />
      {isSubmitSuccessful && !isDirty && <Banner tone="info">Saved.</Banner>}
      <div>
        <Button type="submit" disabled={isPending} aria-busy={isPending}>
          {isPending ? 'Saving…' : submitLabel}
        </Button>
      </div>
    </form>
  );
}
