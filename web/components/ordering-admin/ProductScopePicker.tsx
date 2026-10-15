'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { useStaffProducts } from '@/lib/hooks/useStaffShop';

/**
 * `components/ordering-admin/ProductScopePicker.tsx` — HO-ORD-002 §11.12. Picks the products a discount
 * code applies to: search by name, add from the results, remove a chip. Only product ids are stored; the
 * names shown are the ones seen in this session (an id loaded from the server shows as *Product*).
 */
export function ProductScopePicker({
  selected,
  onChange,
}: {
  selected: string[];
  onChange: (ids: string[]) => void;
}) {
  const [text, setText] = useState('');
  const [names, setNames] = useState<Record<string, string>>({});
  const results = useStaffProducts({ q: text.trim() || undefined, pageSize: 8 });
  const rows = text.trim().length >= 2 ? (results.data?.items ?? []) : [];
  return (
    <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
      <legend className="mb-1 text-sm font-semibold">Products the code applies to</legend>
      {selected.length > 0 && (
        <ul aria-label="Chosen products" className="m-0 flex list-none flex-wrap gap-2 p-0">
          {selected.map((id) => (
            <li key={id} className="flex items-center gap-1 rounded-full bg-surface-sunken px-2.5 py-1 text-xs font-semibold">
              {names[id] ?? 'Product'}
              <button type="button" aria-label={`Remove ${names[id] ?? 'product'}`} className="font-bold" onClick={() => onChange(selected.filter((x) => x !== id))}>×</button>
            </li>
          ))}
        </ul>
      )}
      <label htmlFor="scope-product-search" className="text-sm">Search products by name</label>
      <input id="scope-product-search" type="search" value={text} onChange={(e) => setText(e.target.value)} className="w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink" />
      {rows.length > 0 && (
        <ul aria-label="Matching products" className="m-0 flex list-none flex-col gap-1 p-0">
          {rows.map((p) => (
            <li key={p.productId} className="flex items-center justify-between gap-2 text-sm">
              <span>{p.name}</span>
              <Button
                size="sm"
                variant="secondary"
                disabled={selected.includes(p.productId)}
                onClick={() => { setNames((cur) => ({ ...cur, [p.productId]: p.name })); onChange([...selected, p.productId]); }}
              >
                Add<span className="sr-only"> {p.name}</span>
              </Button>
            </li>
          ))}
        </ul>
      )}
    </fieldset>
  );
}
