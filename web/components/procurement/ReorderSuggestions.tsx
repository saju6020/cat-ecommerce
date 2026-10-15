'use client';

import * as React from 'react';
import Link from 'next/link';
import { buttonVariants } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Banner, Panel } from '@/components/inventory/fields';
import { StackedTable } from '@/components/inventory/StackedTable';
import { formatQuantity } from '@/lib/format/inventory';
import type { ReorderSuggestion } from '@/lib/api/procurement-reorder';

const keyOf = (s: ReorderSuggestion) => `${s.skuId}|${s.locationId}`;
const NO_SUPPLIER = 'none';

/**
 * `components/procurement/ReorderSuggestions.tsx` — HO-IMP-002 §11.2, US-IMP-12. Per item × location:
 * usable, reorder point, on order, average daily use, lead time and the SUGGESTED quantity — every
 * figure the server's (ADR-FE-040), none computed here. A disclosure puts the formula in one
 * sentence, worded from the first row's own fields. Selecting rows builds one purchase order per
 * preferred supplier: each group links to the PO builder pre-filled through URL parameters (OQ-2).
 * An item appears once per order, so a second location of an already-selected item is not offered.
 */
export function ReorderSuggestions({ items }: { items: ReorderSuggestion[] }) {
  const [selected, setSelected] = React.useState<Set<string>>(new Set());
  const chosenSkus = new Set(items.filter((i) => selected.has(keyOf(i))).map((i) => i.skuId));

  function toggle(item: ReorderSuggestion, checked: boolean) {
    setSelected((current) => {
      const next = new Set(current);
      if (checked) next.add(keyOf(item));
      else next.delete(keyOf(item));
      return next;
    });
  }

  const groups = new Map<string, ReorderSuggestion[]>();
  for (const item of items.filter((i) => selected.has(keyOf(i)))) {
    const id = item.preferredSupplierId ?? NO_SUPPLIER;
    groups.set(id, [...(groups.get(id) ?? []), item]);
  }
  const example = items[0];

  return (
    <div className="flex flex-col gap-5">
      <details className="rounded-[var(--radius-md)] border border-border bg-surface p-3 text-sm">
        <summary className="cursor-pointer font-semibold text-accent-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">
          How is the suggested quantity worked out?
        </summary>
        <p className="mt-2 text-ink-muted">
          The server multiplies the average daily use by the days to cover (the supplier&rsquo;s lead time plus the cover period), adds
          the reorder point, and takes away what is usable and already on order. A policy with a fixed reorder quantity uses that
          instead.
          {example && (
            <>
              {' '}
              For {example.displayName} at {example.locationCode ?? 'this location'}: {example.averageDailyConsumption} {example.stockUnit}{' '}
              a day, {example.leadTimeDays} days lead time plus {example.coverDays} days cover, reorder point{' '}
              {formatQuantity(example.reorderPoint, example.stockUnit)}, {formatQuantity(example.usable, example.stockUnit)} usable and{' '}
              {formatQuantity(example.onOrder, example.stockUnit)} on order.
            </>
          )}
        </p>
      </details>

      <StackedTable
        caption="Reorder suggestions"
        rows={items}
        getKey={keyOf}
        columns={[
          {
            id: 'select',
            header: 'Select',
            cell: (item: ReorderSuggestion) => {
              const blocked = !selected.has(keyOf(item)) && chosenSkus.has(item.skuId);
              return (
                <label className="flex items-center gap-2">
                  <input
                    type="checkbox"
                    className="h-4 w-4 accent-[var(--accent)]"
                    checked={selected.has(keyOf(item))}
                    disabled={blocked}
                    onChange={(e) => toggle(item, e.target.checked)}
                  />
                  <span className="sr-only">Select {item.displayName} at {item.locationCode ?? 'this location'}</span>
                  {blocked && <span className="text-xs text-ink-muted">Already selected for another location — an order lists each item once.</span>}
                </label>
              );
            },
          },
          {
            id: 'item',
            header: 'Item',
            rowHeader: true,
            cell: (item) => (
              <span>
                {item.displayName} <span className="font-normal text-ink-muted">— {item.skuCode}</span>
                <span className="block text-xs font-normal text-ink-muted">at {item.locationCode ?? 'this location'}</span>
              </span>
            ),
          },
          { id: 'usable', header: 'Usable', numeric: true, cell: (i) => formatQuantity(i.usable, i.stockUnit) },
          {
            id: 'point',
            header: 'Reorder point',
            numeric: true,
            cell: (i) => (
              <span>
                {formatQuantity(i.reorderPoint, i.stockUnit)} {i.isDefaultedPoint && <Badge variant="neutral">default</Badge>}
              </span>
            ),
          },
          { id: 'onOrder', header: 'On order', numeric: true, cell: (i) => formatQuantity(i.onOrder, i.stockUnit) },
          { id: 'daily', header: 'Average daily use', numeric: true, cell: (i) => `${i.averageDailyConsumption} ${i.stockUnit} a day` },
          { id: 'lead', header: 'Lead time', numeric: true, cell: (i) => `${i.leadTimeDays} days` },
          { id: 'suggested', header: 'Suggested', numeric: true, cell: (i) => <strong>{formatQuantity(i.suggestedQuantity, i.stockUnit)}</strong> },
          { id: 'supplier', header: 'Preferred supplier', cell: (i) => i.preferredSupplierName ?? 'None set' },
        ]}
      />

      <Panel title="Create purchase orders">
        {groups.size === 0 ? (
          <p className="text-sm text-ink-muted">Select suggestions above. One purchase order is built for each preferred supplier.</p>
        ) : (
          <div className="flex flex-col gap-3">
            <Banner tone="neutral">
              {groups.size === 1 ? 'One purchase order will be built.' : `${groups.size} purchase orders will be built, one per supplier.`}{' '}
              Quantities are filled in; you add the unit costs before saving each draft.
            </Banner>
            <ul className="flex flex-col gap-2">
              {[...groups.entries()].map(([supplierId, rows]) => {
                const query = new URLSearchParams();
                if (supplierId !== NO_SUPPLIER) query.set('supplierId', supplierId);
                query.set('lines', rows.map((r) => `${r.skuCode}:${r.suggestedQuantity}`).join(','));
                const name = rows[0]?.preferredSupplierName ?? 'No preferred supplier';
                return (
                  <li key={supplierId} className="flex flex-wrap items-center justify-between gap-2 text-sm">
                    <span>
                      <strong>{name}</strong> — {rows.length} {rows.length === 1 ? 'item' : 'items'}
                    </span>
                    <Link href={`/staff/procurement/purchase-orders/new?${query.toString()}`} className={buttonVariants({ variant: 'primary', size: 'sm' })}>
                      Build the order for {name}
                    </Link>
                  </li>
                );
              })}
            </ul>
          </div>
        )}
      </Panel>
    </div>
  );
}
