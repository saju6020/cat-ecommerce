'use client';

import * as React from 'react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { ApiConflictError, ApiValidationError } from '@/lib/api/client';
import { describeApiError, SHOP_CODE_MESSAGES } from '@/lib/api/errors';
import type { StaffSku } from '@/lib/api/staff-shop';
import { CONFLICT_MESSAGE } from '@/lib/shop/submitSection';
import { parseShippingWeight, shippingWeightServerError } from '@/lib/shop/shippingWeight';
import { Field, inputClass } from './SectionFrame';

export interface EditSkuValues {
  sizeOrWeight: string | null;
  flavour: string | null;
  colour: string | null;
  packQuantity: number | null;
  /** Always sent, including the unchanged value: the API clears the weight when it is omitted. */
  shippingWeightGrams: number | null;
}

/** HO-SHP-001-FU3 — **Edit SKU**: size/weight, flavour, colour, pack quantity and the optional
 *  shipping weight, all prefilled from the read. Every value is sent on save, so an untouched
 *  weight is round-tripped rather than cleared. The dialog is mounted per SKU (`key`). */
export function EditSkuDialog({
  sku,
  onOpenChange,
  onSave,
}: {
  sku: StaffSku;
  onOpenChange: (open: boolean) => void;
  onSave: (sku: StaffSku, values: EditSkuValues) => Promise<unknown>;
}) {
  const [v, setV] = React.useState({
    size: sku.sizeOrWeight ?? '',
    flavour: sku.flavour ?? '',
    colour: sku.colour ?? '',
    pack: sku.packQuantity === null ? '' : String(sku.packQuantity),
    weight: sku.shippingWeightGrams == null ? '' : String(sku.shippingWeightGrams),
  });
  const [errors, setErrors] = React.useState<Record<string, string>>({});
  const [error, setError] = React.useState<string | null>(null);
  const [pending, setPending] = React.useState(false);
  const set = (key: keyof typeof v) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setV((current) => ({ ...current, [key]: e.target.value }));

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setError(null);
    setErrors({});
    if (v.pack && !/^\d+$/.test(v.pack)) {
      return setErrors({ pack: 'Pack quantity must be a whole number.' });
    }
    const weight = parseShippingWeight(v.weight);
    if (weight.error !== undefined) return setErrors({ weight: weight.error });
    setPending(true);
    try {
      await onSave(sku, {
        sizeOrWeight: v.size.trim() || null,
        flavour: v.flavour.trim() || null,
        colour: v.colour.trim() || null,
        packQuantity: v.pack ? Number(v.pack) : null,
        shippingWeightGrams: weight.value,
      });
      onOpenChange(false);
    } catch (e) {
      const weightError = shippingWeightServerError(e);
      if (weightError) {
        setErrors({ weight: weightError });
        return;
      }
      setError(
        e instanceof ApiConflictError
          ? CONFLICT_MESSAGE
          : e instanceof ApiValidationError
            ? describeApiError(e, SHOP_CODE_MESSAGES)
            : 'Something went wrong. Please try again.',
      );
    } finally {
      setPending(false);
    }
  }

  return (
    <Dialog open onOpenChange={(next) => !pending && onOpenChange(next)}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Edit SKU {sku.code}</DialogTitle>
          <DialogDescription>
            A variant needs at least a size or weight, a flavour or a colour.
          </DialogDescription>
        </DialogHeader>
        <form onSubmit={submit} noValidate className="flex flex-col gap-3">
          {error && (
            <p role="alert" className="text-sm font-semibold text-danger-ink">
              {error}
            </p>
          )}
          <Field id="edit-sku-size" label="Size or weight">
            <input
              id="edit-sku-size"
              className={inputClass}
              value={v.size}
              onChange={set('size')}
            />
          </Field>
          <Field id="edit-sku-flavour" label="Flavour">
            <input
              id="edit-sku-flavour"
              className={inputClass}
              value={v.flavour}
              onChange={set('flavour')}
            />
          </Field>
          <Field id="edit-sku-colour" label="Colour">
            <input
              id="edit-sku-colour"
              className={inputClass}
              value={v.colour}
              onChange={set('colour')}
            />
          </Field>
          <Field id="edit-sku-pack" label="Pack quantity" error={errors.pack}>
            <input
              id="edit-sku-pack"
              inputMode="numeric"
              className={inputClass}
              value={v.pack}
              onChange={set('pack')}
              aria-invalid={!!errors.pack}
            />
          </Field>
          <Field
            id="edit-sku-weight"
            label="Shipping weight (g) (optional)"
            error={errors.weight}
            hint="Used to work out the delivery charge. Leave empty if not set."
          >
            <input
              id="edit-sku-weight"
              type="number"
              inputMode="numeric"
              min={1}
              max={100000}
              step={1}
              className={inputClass}
              value={v.weight}
              onChange={set('weight')}
              aria-invalid={!!errors.weight}
            />
          </Field>
          <DialogFooter>
            <Button
              type="button"
              variant="secondary"
              onClick={() => onOpenChange(false)}
              disabled={pending}
            >
              Cancel
            </Button>
            <Button type="submit" disabled={pending}>
              {pending ? 'Saving…' : 'Save SKU'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
