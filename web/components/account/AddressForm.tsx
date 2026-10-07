'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
  useDialogReturnFocus,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { submitWithServerErrors } from '@/lib/api/errors';
import type { DeliveryAddress, DeliveryAddressRequest } from '@/lib/api/account';

const schema = z.object({
  label: z.string().trim().min(1, 'Enter a label, e.g. "Home".'),
  line1: z.string().trim().min(1, 'Enter the address.'),
  line2: z.string().trim().optional(),
  city: z.string().trim().min(1, 'Enter a city.'),
  district: z.string().trim().min(1, 'Enter a district.'),
  postalCode: z.string().trim().optional(),
  deliveryInstructions: z.string().trim().optional(),
});
type FormValues = z.infer<typeof schema>;

const FIELD_MAP = {
  Label: 'label',
  Line1: 'line1',
  Line2: 'line2',
  City: 'city',
  District: 'district',
  PostalCode: 'postalCode',
  DeliveryInstructions: 'deliveryInstructions',
} as const;
const KNOWN_FIELDS = Object.values(FIELD_MAP);

/** `components/account/AddressForm.tsx` — US-ACC-06. A dialog, add or edit. */
export interface AddressFormProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  address?: DeliveryAddress;
  onSubmit: (values: DeliveryAddressRequest) => Promise<unknown>;
}

export function AddressForm({ open, onOpenChange, address, onSubmit }: AddressFormProps) {
  const returnFocusRef = useDialogReturnFocus(open);
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    values: {
      label: address?.label ?? '',
      line1: address?.line1 ?? '',
      line2: address?.line2 ?? '',
      city: address?.city ?? '',
      district: address?.district ?? '',
      postalCode: address?.postalCode ?? '',
      deliveryInstructions: address?.deliveryInstructions ?? '',
    },
  });

  async function handle(values: FormValues) {
    const result = await submitWithServerErrors(
      () =>
        onSubmit({
          label: values.label,
          line1: values.line1,
          line2: values.line2 || null,
          city: values.city,
          district: values.district,
          postalCode: values.postalCode || null,
          deliveryInstructions: values.deliveryInstructions || null,
        }),
      setError,
      { fieldMap: FIELD_MAP, knownFields: KNOWN_FIELDS },
    );
    if (result !== undefined) onOpenChange(false);
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent returnFocusRef={returnFocusRef}>
        <DialogHeader>
          <DialogTitle>{address ? 'Edit address' : 'Add address'}</DialogTitle>
        </DialogHeader>
        <form
          method="post"
          onSubmit={handleSubmit(handle)}
          noValidate
          className="flex flex-col gap-4"
        >
          {errors.root && (
            <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
              {errors.root.message}
            </p>
          )}
          {(
            [
              ['label', 'Label', true],
              ['line1', 'Address line 1', true],
              ['line2', 'Address line 2', false],
              ['city', 'City', true],
              ['district', 'District', true],
              ['postalCode', 'Postal code', false],
              ['deliveryInstructions', 'Delivery instructions', false],
            ] as const
          ).map(([name, fieldLabel, required]) => (
            <div key={name} className="flex flex-col gap-1.5">
              <label htmlFor={name} className="text-sm font-semibold text-ink">
                {fieldLabel}
                {required && (
                  <span aria-hidden="true" className="text-danger-ink">
                    {' '}
                    *
                  </span>
                )}
              </label>
              <input
                id={name}
                required={required}
                aria-invalid={!!errors[name]}
                aria-describedby={errors[name] ? `${name}-error` : undefined}
                className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
                {...register(name)}
              />
              {errors[name] && (
                <p
                  id={`${name}-error`}
                  role="alert"
                  className="text-xs font-semibold text-danger-ink"
                >
                  {errors[name]?.message}
                </p>
              )}
            </div>
          ))}
          <DialogFooter>
            <Button type="button" variant="secondary" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={isSubmitting} aria-busy={isSubmitting}>
              {isSubmitting ? 'Saving…' : 'Save address'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
