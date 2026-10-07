'use client';

import Link from 'next/link';
import { Download } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { buttonVariants } from '@/components/ui/button';
import { DataTable } from '@/components/common/DataTable';
import { formatInstantDate } from '@/lib/format/time';
import { categoryLabel } from '@/lib/shop/labels';
import type { StaffProductSummary } from '@/lib/api/staff-shop';

export type StaffProductFilterName =
  'status' | 'category' | 'brand' | 'kind' | 'listed' | 'barcode' | 'sort';

export interface StaffProductTableProps {
  items: StaffProductSummary[] | undefined;
  totalCount?: number;
  pageSize: number;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  filters: Record<StaffProductFilterName, string>;
  onFilter: (name: StaffProductFilterName, value: string) => void;
  /** Writes a pending text field straight to the URL (Enter from a barcode scanner, HW-2). */
  onFlush: (name: StaffProductFilterName) => void;
  exportHref: string;
}

const STATUS_VARIANT = { Draft: 'neutral', Active: 'success', Archived: 'neutral' } as const;
const select =
  'rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink';

function SelectFilter({
  id,
  label,
  value,
  options,
  onChange,
}: {
  id: string;
  label: string;
  value: string;
  options: Array<[string, string]>;
  onChange: (value: string) => void;
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-xs font-semibold text-ink-muted">
        {label}
      </label>
      <select id={id} value={value} onChange={(e) => onChange(e.target.value)} className={select}>
        <option value="">All</option>
        {options.map(([v, text]) => (
          <option key={v} value={v}>
            {text}
          </option>
        ))}
      </select>
    </div>
  );
}

/**
 * HO-SHP-002 §11.6 `StaffProductTable` — UI-4. Search (name, brand, SKU code — DataTable's `q`),
 * **barcode** (a scanner's keyboard-wedge input plus Enter, HW-2), status, category, kind, listed and
 * sort, all in the URL; pagination; and **Export CSV** from the server with the same filters.
 */
export function StaffProductTable(props: StaffProductTableProps) {
  const { filters, onFilter, onFlush } = props;
  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="font-display text-2xl font-semibold text-ink">Products</h1>
        <div className="flex gap-2">
          <a href={props.exportHref} className={buttonVariants({ variant: 'secondary' })}>
            <Download size={16} aria-hidden="true" />
            Export CSV
          </a>
          <Link href="/staff/shop/products/new" className={buttonVariants({ variant: 'primary' })}>
            New product
          </Link>
        </div>
      </div>

      <div className="flex flex-wrap items-end gap-3">
        <SelectFilter
          id="f-status"
          label="Status"
          value={filters.status}
          onChange={(v) => onFilter('status', v)}
          options={[
            ['Draft', 'Draft'],
            ['Active', 'Active'],
            ['Archived', 'Archived'],
          ]}
        />
        <SelectFilter
          id="f-category"
          label="Category"
          value={filters.category}
          onChange={(v) => onFilter('category', v)}
          options={[
            'CatFood',
            'Litter',
            'LitterPotsAndAccessories',
            'BagsAndCarriers',
            'HealthAndGrooming',
            'ToysAndFurniture',
          ].map((c) => [c, categoryLabel(c)] as [string, string])}
        />
        <SelectFilter
          id="f-kind"
          label="Kind"
          value={filters.kind}
          onChange={(v) => onFilter('kind', v)}
          options={[
            ['Standard', 'Standard'],
            ['Bundle', 'Kit'],
          ]}
        />
        <SelectFilter
          id="f-listed"
          label="Listed in shop"
          value={filters.listed}
          onChange={(v) => onFilter('listed', v)}
          options={[
            ['true', 'Listed'],
            ['false', 'Not listed'],
          ]}
        />
        <SelectFilter
          id="f-sort"
          label="Sort"
          value={filters.sort}
          onChange={(v) => onFilter('sort', v)}
          options={[
            ['name', 'Name A–Z'],
            ['name_desc', 'Name Z–A'],
          ]}
        />
        <div className="flex flex-col gap-1.5">
          <label htmlFor="f-brand" className="text-xs font-semibold text-ink-muted">
            Brand
          </label>
          <input
            id="f-brand"
            className={select}
            value={filters.brand}
            onChange={(e) => onFilter('brand', e.target.value)}
            onBlur={() => onFlush('brand')}
          />
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="f-barcode" className="text-xs font-semibold text-ink-muted">
            Barcode
          </label>
          <input
            id="f-barcode"
            inputMode="numeric"
            placeholder="Scan or type, then Enter"
            className={select}
            value={filters.barcode}
            onChange={(e) => onFilter('barcode', e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                e.preventDefault();
                onFlush('barcode');
              }
            }}
            onBlur={() => onFlush('barcode')}
          />
        </div>
      </div>

      <DataTable
        caption="Products"
        rows={props.items}
        getRowId={(row) => row.productId}
        isLoading={props.isLoading}
        isError={props.isError}
        onRetry={props.onRetry}
        emptyState={{
          title: 'No products match',
          description: 'Clear a filter, or add a new product.',
        }}
        search={{ label: 'Search products', placeholder: 'Name, brand or SKU code' }}
        pagination={{ totalCount: props.totalCount ?? 0, pageSize: props.pageSize }}
        rowHref={(row) => `/staff/shop/products/${row.productId}`}
        columns={[
          { id: 'name', header: 'Name', cell: (row) => row.name },
          { id: 'brand', header: 'Brand', cell: (row) => row.brand },
          { id: 'category', header: 'Category', cell: (row) => categoryLabel(row.category) },
          {
            id: 'kind',
            header: 'Kind',
            cell: (row) => (row.kind === 'Bundle' ? 'Kit' : 'Standard'),
          },
          {
            id: 'status',
            header: 'Status',
            cell: (row) => <Badge variant={STATUS_VARIANT[row.status]}>{row.status}</Badge>,
          },
          { id: 'skus', header: 'SKUs', cell: (row) => row.skuCount },
          {
            id: 'listed',
            header: 'Listed',
            cell: (row) => (row.listedOnStorefront ? 'Yes' : 'No'),
          },
          { id: 'updated', header: 'Updated', cell: (row) => formatInstantDate(row.lastUpdatedOn) },
        ]}
      />
    </div>
  );
}
