'use client';

import * as React from 'react';
import { useForm } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { LimitedMarkup } from '@/components/content/LimitedMarkup';
import { ApiValidationError } from '@/lib/api/client';
import {
  applyValidationErrors,
  describeUnexpectedError,
  SHOP_CODE_MESSAGES,
  SHOP_FIELD_MAP,
} from '@/lib/api/errors';
import { plainExcerpt } from '@/lib/shop/helpers';
import type { StaffOrigin } from '@/lib/api/staff-shop';
import { Field, inputClass } from './SectionFrame';

interface Values {
  countryCode: string;
  sourcingNotes: string;
  isFeatured: boolean;
  sortOrder: string;
}

export interface OriginFormProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  origin: StaffOrigin | null;
  countries: Array<{ code: string; name: string }>;
  takenCodes: string[];
  onSave: (values: {
    countryCode: string;
    sourcingNotes: string | null;
    isFeatured: boolean;
    sortOrder: number;
    isNew: boolean;
  }) => Promise<unknown>;
}

/** HO-SHP-002 §11.6 `OriginForm` — country (select; fixed when editing), sourcing notes
 *  (LimitedMarkup with a live preview), featured, sort order. */
export function OriginForm({
  open,
  onOpenChange,
  origin,
  countries,
  takenCodes,
  onSave,
}: OriginFormProps) {
  const {
    register,
    handleSubmit,
    setError,
    watch,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<Values>({
    values: {
      countryCode: origin?.countryCode ?? '',
      sourcingNotes: origin?.sourcingNotes ?? '',
      isFeatured: origin?.isFeatured ?? false,
      sortOrder: String(origin?.sortOrder ?? 0),
    },
  });
  const notes = watch('sourcingNotes');
  return (
    <Dialog open={open} onOpenChange={(next) => !isSubmitting && onOpenChange(next)}>
      <DialogContent className="max-w-xl">
        <DialogHeader>
          <DialogTitle>{origin ? `Edit ${origin.countryName}` : 'Add an origin'}</DialogTitle>
          <DialogDescription>
            Origins appear on Shop by Country once a food from there is published.
          </DialogDescription>
        </DialogHeader>
        <form
          noValidate
          className="flex flex-col gap-3"
          onSubmit={handleSubmit(async (v) => {
            if (!v.countryCode) return setError('countryCode', { message: 'Choose a country.' });
            if (!/^-?\d+$/.test(v.sortOrder))
              return setError('sortOrder', { message: 'Sort order must be a whole number.' });
            try {
              await onSave({
                countryCode: v.countryCode,
                sourcingNotes: v.sourcingNotes.trim() || null,
                isFeatured: v.isFeatured,
                sortOrder: Number(v.sortOrder),
                isNew: !origin,
              });
              reset();
              onOpenChange(false);
            } catch (error) {
              if (error instanceof ApiValidationError) {
                applyValidationErrors(error, setError, {
                  fieldMap: SHOP_FIELD_MAP as never,
                  codeMessages: SHOP_CODE_MESSAGES,
                  knownFields: ['countryCode', 'sourcingNotes', 'isFeatured', 'sortOrder'],
                });
              } else {
                setError('root', { message: describeUnexpectedError(error) });
              }
            }
          })}
        >
          {errors.root && (
            <p role="alert" className="text-sm font-semibold text-danger-ink">
              {errors.root.message}
            </p>
          )}
          <Field id="og-country" label="Country" error={errors.countryCode?.message}>
            <select
              id="og-country"
              className={inputClass}
              disabled={!!origin}
              {...register('countryCode')}
            >
              <option value="">Choose…</option>
              {countries
                .filter((c) => !!origin || !takenCodes.includes(c.code))
                .map((c) => (
                  <option key={c.code} value={c.code}>
                    {c.name}
                  </option>
                ))}
            </select>
          </Field>
          <Field
            id="og-notes"
            label="Sourcing notes"
            error={errors.sourcingNotes?.message}
            hint="Paragraphs, **bold**, - bullets and links only."
          >
            <textarea
              id="og-notes"
              rows={5}
              className={inputClass}
              {...register('sourcingNotes')}
            />
          </Field>
          <div
            role="group"
            aria-label="Sourcing notes preview"
            className="rounded-[var(--radius-sm)] border border-border bg-surface p-3 text-sm"
          >
            <LimitedMarkup body={notes || ''} className="space-y-2" />
          </div>
          <label className="flex items-center gap-2 text-sm text-ink">
            <input
              type="checkbox"
              className="h-4 w-4 accent-[var(--accent)]"
              {...register('isFeatured')}
            />
            Featured on the shop home
          </label>
          <Field id="og-order" label="Sort order" error={errors.sortOrder?.message}>
            <input
              id="og-order"
              inputMode="numeric"
              className={inputClass}
              {...register('sortOrder')}
            />
          </Field>
          <DialogFooter>
            <Button
              type="button"
              variant="secondary"
              onClick={() => onOpenChange(false)}
              disabled={isSubmitting}
            >
              Cancel
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Saving…' : 'Save origin'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

export interface OriginsTableProps {
  origins: StaffOrigin[];
  countries: Array<{ code: string; name: string }>;
  onSave: OriginFormProps['onSave'];
}

/** HO-SHP-002 §11.6 `OriginsTable` — sourcing origins with their product count (read-only). */
export function OriginsTable({ origins, countries, onSave }: OriginsTableProps) {
  const [editing, setEditing] = React.useState<StaffOrigin | null>(null);
  const [adding, setAdding] = React.useState(false);
  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="font-display text-2xl font-semibold text-ink">Sourcing origins</h1>
        <Button onClick={() => setAdding(true)}>Add origin</Button>
      </div>
      <div className="overflow-x-auto">
        <table className="w-full min-w-[640px] border-collapse text-sm">
          <caption className="sr-only">Sourcing origins</caption>
          <thead>
            <tr className="border-b border-border text-left text-xs text-ink-muted">
              {['Country', 'Notes', 'Featured', 'Sort order', 'Products', 'Actions'].map((h) => (
                <th key={h} scope="col" className="py-2 pr-3 font-semibold">
                  {h}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {origins.map((o) => (
              <tr key={o.countryCode} className="border-b border-border">
                <th scope="row" className="py-2 pr-3 text-left font-semibold text-ink">
                  {o.countryName}
                </th>
                <td className="py-2 pr-3">{plainExcerpt(o.sourcingNotes, 80) || '—'}</td>
                <td className="py-2 pr-3">{o.isFeatured ? 'Yes' : 'No'}</td>
                <td className="py-2 pr-3">{o.sortOrder}</td>
                <td className="py-2 pr-3">{o.productCount}</td>
                <td className="py-2">
                  <Button
                    variant="secondary"
                    size="sm"
                    aria-label={`Edit ${o.countryName}`}
                    onClick={() => setEditing(o)}
                  >
                    Edit
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <OriginForm
        open={adding}
        onOpenChange={setAdding}
        origin={null}
        countries={countries}
        takenCodes={origins.map((o) => o.countryCode)}
        onSave={onSave}
      />
      <OriginForm
        open={editing !== null}
        onOpenChange={(open) => !open && setEditing(null)}
        origin={editing}
        countries={countries}
        takenCodes={[]}
        onSave={onSave}
      />
    </div>
  );
}
