'use client';

import * as React from 'react';
import Link from 'next/link';
import { useRouter, usePathname, useSearchParams } from 'next/navigation';
import { Check, ShieldAlert } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { DataTable, type DataTableColumn } from '@/components/common/DataTable';
import { SelectField } from '@/components/inventory/fields';
import type { FormularyMedicineSummary } from '@/lib/api/formulary';
import { DOSING_BASIS_LABELS, formLabel } from '@/lib/format/pharmacy';

/**
 * `FormularyTable` — HO-PHM-002 §11.7, UI-4. Search, the controlled / active / stocked filters and
 * the sort all live in the URL (a filtered list is linkable and survives a reload); paging is the
 * server's. The server filters on controlled and active; "stocked" has no server filter, so it narrows
 * the page already fetched (HO-PHM-002 §1 Amended). The sort also orders the page in view, because the
 * list endpoint has no sort parameter. Every status is a word with an icon, never colour alone.
 */
export const FORMULARY_PAGE_SIZE = 25;

export function FormularyTable({
  rows,
  totalCount,
  isLoading,
  isError,
  onRetry,
}: {
  rows: FormularyMedicineSummary[] | undefined;
  totalCount: number;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}) {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const stocked = params.get('stocked') ?? '';
  const sort = params.get('sort');
  const direction = params.get('dir') === 'desc' ? -1 : 1;

  function setParam(name: string, value: string) {
    const next = new URLSearchParams(params.toString());
    if (value) next.set(name, value);
    else next.delete(name);
    next.delete('page');
    const query = next.toString();
    router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false });
  }

  const visible = React.useMemo(() => {
    let list = rows ?? [];
    if (stocked) list = list.filter((row) => String(row.isStocked) === stocked);
    if (sort) {
      const key = sort as keyof FormularyMedicineSummary;
      list = [...list].sort((a, b) => String(a[key]).localeCompare(String(b[key])) * direction);
    }
    return list;
  }, [rows, stocked, sort, direction]);

  const columns: Array<DataTableColumn<FormularyMedicineSummary>> = [
    { id: 'code', header: 'Code', sortable: true, cell: (row) => row.code },
    {
      id: 'name',
      header: 'Name',
      sortable: true,
      cell: (row) => (
        <Link
          href={`/staff/admin/formulary/${row.id}`}
          className="font-semibold text-accent-ink underline"
        >
          {row.name}
        </Link>
      ),
    },
    {
      id: 'strengthText',
      header: 'Strength',
      cell: (row) => <span data-clinical-value>{row.strengthText}</span>,
    },
    { id: 'form', header: 'Form', cell: (row) => formLabel(row.form) },
    {
      id: 'dosingBasis',
      header: 'Dosing',
      cell: (row) => DOSING_BASIS_LABELS[row.dosingBasis] ?? row.dosingBasis,
    },
    {
      id: 'isControlled',
      header: 'Controlled',
      cell: (row) =>
        row.isControlled ? (
          <Badge variant="warning" icon={<ShieldAlert size={12} aria-hidden="true" />}>
            Controlled
          </Badge>
        ) : (
          <span className="text-ink-muted">No</span>
        ),
    },
    {
      id: 'isStocked',
      header: 'Stock',
      cell: (row) =>
        row.isStocked ? (
          <Badge variant="success" icon={<Check size={12} aria-hidden="true" />}>
            Stocked
          </Badge>
        ) : (
          <Badge variant="neutral">Not stocked</Badge>
        ),
    },
    {
      id: 'isActive',
      header: 'Status',
      cell: (row) => (
        <Badge variant={row.isActive ? 'success' : 'neutral'}>
          {row.isActive ? 'Active' : 'Inactive'}
        </Badge>
      ),
    },
  ];

  return (
    <div className="flex flex-col gap-4">
      <div className="grid gap-3 sm:grid-cols-3">
        <SelectField
          id="ft-controlled"
          label="Controlled"
          value={params.get('controlled') ?? ''}
          onChange={(event) => setParam('controlled', event.target.value)}
        >
          <option value="">Any</option>
          <option value="true">Controlled only</option>
          <option value="false">Not controlled</option>
        </SelectField>
        <SelectField
          id="ft-active"
          label="Status"
          value={params.get('active') ?? ''}
          onChange={(event) => setParam('active', event.target.value)}
        >
          <option value="">Any</option>
          <option value="true">Active</option>
          <option value="false">Inactive</option>
        </SelectField>
        <SelectField
          id="ft-stocked"
          label="Stock"
          value={stocked}
          onChange={(event) => setParam('stocked', event.target.value)}
        >
          <option value="">Any</option>
          <option value="true">Stocked</option>
          <option value="false">Not stocked</option>
        </SelectField>
      </div>
      <DataTable
        caption="Formulary medicines"
        columns={columns}
        rows={isLoading || isError ? undefined : visible}
        getRowId={(row) => row.id}
        isLoading={isLoading}
        isError={isError}
        onRetry={onRetry}
        search={{ label: 'Search by name or code', placeholder: 'Name or code' }}
        pagination={{ totalCount, pageSize: FORMULARY_PAGE_SIZE }}
        emptyState={{
          title: 'No medicines match',
          description: 'Clear a filter, or add a medicine to the formulary.',
        }}
      />
    </div>
  );
}
