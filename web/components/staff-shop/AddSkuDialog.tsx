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
import { CONFLICT_MESSAGE } from '@/lib/shop/submitSection';
import { Field, inputClass } from './SectionFrame';

export interface AddSkuValues {
  code: string;
  barcode: string | null;
  sizeOrWeight: string | null;
  flavour: string | null;
  colour: string | null;
  packQuantity: number | null;
}

/** HO-SHP-002 §11.6 — **Add SKU**: code, barcode, size/weight, flavour, colour, pack quantity. A
 *  server refusal (duplicate code or barcode, missing attributes) is shown on the dialog; the typed
 *  values stay. */
export function AddSkuDialog({
  open,
  onOpenChange,
  onAdd,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onAdd: (values: AddSkuValues) => Promise<unknown>;
}) {
  const [v, setV] = React.useState({
    code: '',
    barcode: '',
    size: '',
    flavour: '',
    colour: '',
    pack: '',
  });
  const [error, setError] = React.useState<string | null>(null);
  const [pending, setPending] = React.useState(false);
  const set = (key: keyof typeof v) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setV((current) => ({ ...current, [key]: e.target.value }));

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setError(null);
    if (!v.code.trim()) return setError('Enter the SKU code.');
    if (v.pack && !/^\d+$/.test(v.pack)) return setError('Pack quantity must be a whole number.');
    setPending(true);
    try {
      await onAdd({
        code: v.code.trim(),
        barcode: v.barcode.trim() || null,
        sizeOrWeight: v.size.trim() || null,
        flavour: v.flavour.trim() || null,
        colour: v.colour.trim() || null,
        packQuantity: v.pack ? Number(v.pack) : null,
      });
      setV({ code: '', barcode: '', size: '', flavour: '', colour: '', pack: '' });
      onOpenChange(false);
    } catch (e) {
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
    <Dialog open={open} onOpenChange={(next) => !pending && onOpenChange(next)}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Add SKU</DialogTitle>
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
          <Field id="sku-code" label="SKU code">
            <input id="sku-code" className={inputClass} value={v.code} onChange={set('code')} />
          </Field>
          <Field id="sku-barcode" label="Barcode (optional)">
            <input
              id="sku-barcode"
              inputMode="numeric"
              className={inputClass}
              value={v.barcode}
              onChange={set('barcode')}
            />
          </Field>
          <Field id="sku-size" label="Size or weight">
            <input id="sku-size" className={inputClass} value={v.size} onChange={set('size')} />
          </Field>
          <Field id="sku-flavour" label="Flavour">
            <input
              id="sku-flavour"
              className={inputClass}
              value={v.flavour}
              onChange={set('flavour')}
            />
          </Field>
          <Field id="sku-colour" label="Colour">
            <input
              id="sku-colour"
              className={inputClass}
              value={v.colour}
              onChange={set('colour')}
            />
          </Field>
          <Field id="sku-pack" label="Pack quantity">
            <input
              id="sku-pack"
              inputMode="numeric"
              className={inputClass}
              value={v.pack}
              onChange={set('pack')}
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
              {pending ? 'Adding…' : 'Add SKU'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
