'use client';

import * as React from 'react';
import { useFieldArray, useForm } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { submitSection } from '@/lib/shop/submitSection';
import type { StaffProductDetail } from '@/lib/api/staff-shop';
import { SectionFrame, inputClass } from './SectionFrame';

export interface PickedSku {
  skuId: string;
  code: string;
  label: string;
}

interface Row {
  componentSkuId: string;
  code: string;
  label: string;
  quantity: string;
}
interface Values {
  components: Row[];
}

export interface BundleEditorProps {
  product: StaffProductDetail;
  /** The SKU picker — staff product search, then that product's SKUs (wired by the page). */
  renderPicker: (onPick: (sku: PickedSku) => void) => React.ReactNode;
  onSave: (components: Array<{ componentSkuId: string; quantity: number }>) => Promise<unknown>;
  onReload: () => void;
}

const CODE_CHARACTER = /[A-Za-z0-9_-]/;

/**
 * Which row a server `InvalidBundleComponent` message names: the one whose SKU code it mentions as
 * a whole code. A code inside a longer one does not count (`KIT-1` is not named by `KIT-10`) - S14.
 * Written with `indexOf` and neighbour checks, not a lookbehind (Safari before 16.4 throws on one).
 */
export function rowNamedBy(message: string | undefined, rows: Array<{ code: string }>): number {
  if (!message) return -1;
  return rows.findIndex((r) => {
    if (!r.code) return false;
    let from = message.indexOf(r.code);
    while (from !== -1) {
      const before = message.charAt(from - 1);
      const after = message.charAt(from + r.code.length);
      if (!(before && CODE_CHARACTER.test(before)) && !(after && CODE_CHARACTER.test(after))) {
        return true;
      }
      from = message.indexOf(r.code, from + 1);
    }
    return false;
  });
}

/**
 * HO-SHP-002 §11.6 `BundleEditor` — US-SHP-12, kits only. Component rows (SKU, quantity 1–20); the
 * server's `InvalidBundleComponent` (a kit, a prescription diet, a discontinued SKU…) is shown on the
 * ROW whose SKU it names, else on the form.
 */
export function BundleEditor({ product, renderPicker, onSave, onReload }: BundleEditorProps) {
  const {
    control,
    register,
    handleSubmit,
    setError,
    reset,
    watch,
    formState: { errors, isDirty, isSubmitting, isSubmitSuccessful },
  } = useForm<Values>({
    defaultValues: {
      components: (product.bundleComponents ?? []).map((c) => ({
        componentSkuId: c.componentSkuId,
        code: c.skuCode ?? '',
        label: `${c.productName ?? 'Product'} (${c.skuCode ?? 'SKU'})`,
        quantity: String(c.quantity),
      })),
    },
  });
  const { fields, append, remove } = useFieldArray({ control, name: 'components' });
  const rows = watch('components');
  const [notice, setNotice] = React.useState<string | null>(null);
  const serverMessage = (errors as { components?: { message?: string } }).components?.message;
  const namedRow = rowNamedBy(serverMessage, rows);

  return (
    <SectionFrame
      title="Kit components"
      description="A kit needs at least 2 component units. Kits and prescription diets can't be components."
      errors={errors}
      isDirty={isDirty}
      isSubmitting={isSubmitting}
      isSubmitSuccessful={isSubmitSuccessful}
      onReload={onReload}
      onSubmit={handleSubmit(async (values) => {
        const bad = values.components.findIndex(
          (r) => !/^\d+$/.test(r.quantity) || Number(r.quantity) < 1 || Number(r.quantity) > 20,
        );
        if (bad >= 0) {
          setError(`components.${bad}.quantity`, {
            type: 'client',
            message: 'Quantity must be from 1 to 20.',
          });
          return;
        }
        const ok = await submitSection(
          () =>
            onSave(
              values.components.map((r) => ({
                componentSkuId: r.componentSkuId,
                quantity: Number(r.quantity),
              })),
            ),
          setError,
          ['components'],
        );
        if (ok) reset(values);
      })}
    >
      {serverMessage && namedRow < 0 && (
        <p role="alert" className="text-sm font-semibold text-danger-ink">
          {serverMessage}
        </p>
      )}
      <ul className="m-0 flex list-none flex-col gap-2 p-0">
        {fields.map((field, index) => (
          <li
            key={field.id}
            className="flex flex-col gap-1 rounded-[var(--radius-sm)] border border-border bg-surface p-3"
          >
            <div className="flex flex-wrap items-center gap-3">
              <span className="min-w-0 flex-1 text-sm font-semibold text-ink">
                {rows[index]?.label}
              </span>
              <label htmlFor={`bc-${index}-qty`} className="text-sm text-ink-muted">
                Quantity
              </label>
              <input
                id={`bc-${index}-qty`}
                inputMode="numeric"
                className={`${inputClass} !w-20`}
                aria-invalid={!!errors.components?.[index]?.quantity || namedRow === index}
                {...register(`components.${index}.quantity`)}
              />
              <Button
                type="button"
                variant="ghost"
                size="sm"
                aria-label={`Remove ${rows[index]?.label}`}
                onClick={() => remove(index)}
              >
                Remove
              </Button>
            </div>
            {errors.components?.[index]?.quantity?.message && (
              <p role="alert" className="text-xs font-semibold text-danger-ink">
                {errors.components[index]!.quantity!.message}
              </p>
            )}
            {namedRow === index && (
              <p role="alert" className="text-xs font-semibold text-danger-ink">
                {serverMessage}
              </p>
            )}
          </li>
        ))}
        {fields.length === 0 && <li className="text-sm text-ink-muted">No components yet.</li>}
      </ul>
      <div className="flex flex-col gap-2">
        <p className="text-sm font-semibold text-ink">Add a component</p>
        {renderPicker((sku) => {
          // The same SKU twice would be two rows for one component: raise its quantity instead.
          if (rows.some((r) => r.componentSkuId === sku.skuId)) {
            setNotice(`${sku.label} is already in the kit. Change its quantity instead.`);
            return;
          }
          setNotice(null);
          append({ componentSkuId: sku.skuId, code: sku.code, label: sku.label, quantity: '1' });
        })}
        {notice && (
          <p role="status" className="text-sm font-semibold text-ink">
            {notice}
          </p>
        )}
      </div>
    </SectionFrame>
  );
}
