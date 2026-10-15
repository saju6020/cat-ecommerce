'use client';

import * as React from 'react';
import { Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { useFormularyList, useFormularySkuSearch } from '@/lib/hooks/useFormulary';
import type { FormularySkuSearchResult } from '@/lib/api/formulary';
import { ListboxPicker } from './ListboxPicker';

/**
 * `LinkedSkuPicker` — HO-PHM-002 §11.7. Links clinic SKUs to a medicine (a medicine is *stocked* when
 * it has at least one). Search is over `GET /api/admin/formulary/sku-search` and shows product,
 * variant and barcode. A SKU that already belongs to ANOTHER medicine is listed but disabled, with that
 * medicine's name written out; one already linked to THIS medicine is simply not offered twice.
 */
export interface LinkedSku {
  skuId: string;
  productName: string;
  variantLabel: string;
}

export function LinkedSkuPicker({
  medicineId,
  linked,
  onChange,
  error,
}: {
  medicineId: string | null;
  linked: LinkedSku[];
  onChange: (next: LinkedSku[]) => void;
  error?: string;
}) {
  const [term, setTerm] = React.useState('');
  const search = useFormularySkuSearch(term);
  const names = useFormularyList({ pageSize: 100 });
  const nameOf = (id: string | null) =>
    names.data?.items.find((m) => m.id === id)?.name ?? 'another medicine';
  const items = (search.data ?? []).filter((sku) => !linked.some((l) => l.skuId === sku.skuId));
  const full = linked.length >= 10;

  return (
    <div className="flex flex-col gap-3">
      <ListboxPicker<FormularySkuSearchResult>
        id="fm-sku"
        label="Link a clinic SKU"
        items={items}
        isLoading={search.isLoading}
        isError={search.isError}
        getKey={(sku) => sku.skuId}
        getLabel={(sku) => `${sku.productName} — ${sku.variantLabel}`}
        renderDetail={(sku) => (
          <>
            <span>{sku.barcode ? `Barcode ${sku.barcode}` : 'No barcode'}</span>
            {sku.isClinicOnly && <Badge variant="neutral">Clinic only</Badge>}
          </>
        )}
        getDisabledReason={(sku) =>
          sku.linkedMedicineId && sku.linkedMedicineId !== medicineId
            ? `Already linked to ${nameOf(sku.linkedMedicineId)}`
            : null
        }
        onSearch={setTerm}
        onSelect={(sku) =>
          onChange([
            ...linked,
            { skuId: sku.skuId, productName: sku.productName, variantLabel: sku.variantLabel },
          ])
        }
        disabled={full}
        hint={full ? 'A medicine links at most ten SKUs.' : 'Type a product name or barcode.'}
        error={error}
        noun="SKUs"
      />
      {linked.length === 0 ? (
        <p className="text-sm text-ink-muted">
          No SKUs linked. The medicine can be prescribed, but it will show{' '}
          <strong>Not stocked</strong> and cannot be dispensed here.
        </p>
      ) : (
        <ul className="flex list-none flex-col gap-2 p-0">
          {linked.map((sku) => (
            <li
              key={sku.skuId}
              className="flex flex-wrap items-center justify-between gap-2 rounded-[var(--radius-sm)] border border-border px-3 py-2 text-sm"
            >
              <span className="min-w-0 break-words">
                {sku.productName} <span className="text-ink-muted">— {sku.variantLabel}</span>
              </span>
              <Button
                variant="ghost"
                size="sm"
                className="h-auto min-h-8 whitespace-normal py-1 text-left"
                onClick={() => onChange(linked.filter((l) => l.skuId !== sku.skuId))}
              >
                <Trash2 size={14} aria-hidden="true" />
                Unlink {sku.productName}
              </Button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
