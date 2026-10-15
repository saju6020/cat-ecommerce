'use client';

import type { FieldErrors, UseFormRegister } from 'react-hook-form';
import type { PaymentMethod } from '@/lib/api/billing-staff';
import { TextField } from './Fields';
import { methodLabel } from './labels';
import type { PaymentFormValues } from './recordPaymentSchema';

/**
 * `components/billing/PaymentMethodFields.tsx` — HO-BIL-002 §11.4, ADR-FE-082. The method-dependent
 * fields, **rendered from the server's capability flags** and never from a method name: a reference
 * field labelled with the server's `referenceLabel` (*"Terminal slip number"*) when
 * `requiresReference`, a collector when `requiresCollector`. A method added in Phase 5 needs no
 * change here. The reference runs the C-5 guard (the form's schema) and its message never repeats
 * the value.
 */
export function MethodPicker({
  methods,
  value,
  onChange,
  disabled,
}: {
  methods: PaymentMethod[];
  value: string;
  onChange: (method: string) => void;
  disabled?: boolean;
}) {
  return (
    <fieldset disabled={disabled} className="flex flex-col gap-2 border-0 p-0">
      <legend className="mb-1 text-sm font-semibold text-ink">
        Payment method{' '}
        <span aria-hidden="true" className="text-danger-ink">
          *
        </span>
      </legend>
      <div className="flex flex-wrap gap-2">
        {methods.map((method) => (
          <label
            key={method.name}
            className="inline-flex cursor-pointer items-center gap-2 rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm has-[:checked]:border-accent has-[:checked]:bg-accent-soft"
          >
            <input
              type="radio"
              name="payment-method"
              value={method.name}
              checked={value === method.name}
              onChange={() => onChange(method.name)}
            />
            {methodLabel(method.name)}
          </label>
        ))}
      </div>
    </fieldset>
  );
}

export function PaymentMethodFields({
  method,
  register,
  errors,
  staffName,
  collectorKind,
}: {
  method: PaymentMethod | undefined;
  register: UseFormRegister<PaymentFormValues>;
  errors: FieldErrors<PaymentFormValues>;
  staffName: string | undefined;
  collectorKind: 'Courier' | 'Staff';
}) {
  if (!method) return null;
  return (
    <>
      {method.requiresReference && (
        <TextField
          id="pay-reference"
          label={method.referenceLabel}
          required
          autoComplete="off"
          error={errors.externalReference?.message}
          {...register('externalReference')}
        />
      )}
      {method.requiresCollector && (
        <fieldset className="flex min-w-0 flex-col gap-3 rounded-[var(--radius-md)] border border-border p-3">
          <legend className="px-1 text-sm font-semibold text-ink">
            Collected by{' '}
            <span aria-hidden="true" className="text-danger-ink">
              *
            </span>
          </legend>
          <div className="flex flex-wrap gap-4 text-sm">
            <label className="inline-flex items-center gap-2">
              <input type="radio" value="Courier" {...register('collectorKind')} />A courier
            </label>
            <label className="inline-flex items-center gap-2">
              <input type="radio" value="Staff" {...register('collectorKind')} />
              Staff — {staffName ?? 'me'}
            </label>
          </div>
          {collectorKind === 'Courier' && (
            <div className="grid gap-3 sm:grid-cols-2">
              <TextField id="pay-courier-code" label="Courier code" required error={errors.courierCode?.message} {...register('courierCode')} />
              <TextField id="pay-courier-name" label="Courier name" required error={errors.courierName?.message} {...register('courierName')} />
            </div>
          )}
          {errors.collectorKind?.message && (
            <p role="alert" className="text-xs font-semibold text-danger-ink">
              {errors.collectorKind.message}
            </p>
          )}
        </fieldset>
      )}
    </>
  );
}
