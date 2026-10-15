'use client';

import { Suspense, useMemo, useState } from 'react';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { LoadingState } from '@/components/common/LoadingState';
import { FilterBar } from '@/components/inventory/FilterBar';
import { PageHeading } from '@/components/inventory/fields';
import { LEDGER_KIND_LABELS, REFERENCE_KIND_LABELS } from '@/components/inventory/labels';
import { SkuPicker } from '@/components/inventory/SkuPicker';
import { LedgerSearch } from '@/components/stock/LedgerSearch';
import { ApiForbiddenError } from '@/lib/api/client';
import { getLedgerExportPath } from '@/lib/api/inventory-stock';
import { todayInZone } from '@/lib/format/time';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useLedger } from '@/lib/hooks/useInventory';
import { useInventoryListState } from '@/lib/hooks/useInventoryListState';
import { useLocations } from '@/lib/hooks/useInventoryLookup';

const FIELDS = ['from', 'to', 'kind', 'locationId', 'skuId', 'batchId', 'referenceKind', 'referenceId'] as const;
const DEFAULT_RANGE_DAYS = 7;
/** The server's rule (HO-IMP-001 `LedgerQueryMaxRangeDays`), stated before submit. */
const MAX_RANGE_DAYS = 366;
const DAY_MS = 86_400_000;

function shiftDate(date: string, days: number): string {
  return new Date(new Date(`${date}T00:00:00Z`).getTime() + days * DAY_MS).toISOString().slice(0, 10);
}

/** `/staff/inventory/ledger` — US-IMP-15: the stock ledger, read-only, with a server CSV. */
function LedgerContent() {
  const zone = useBranchTimeZone();
  const today = todayInZone(zone);
  const defaults = useMemo(() => ({ from: shiftDate(today, -(DEFAULT_RANGE_DAYS - 1)), to: today }), [today]);
  const list = useInventoryListState(FIELDS, { defaults, pageSize: 50 });
  const [itemLabel, setItemLabel] = useState('');
  const locations = useLocations();

  const from = list.values.from || defaults.from;
  const to = list.values.to || defaults.to;
  const days = Math.round((new Date(`${to}T00:00:00Z`).getTime() - new Date(`${from}T00:00:00Z`).getTime()) / DAY_MS) + 1;
  const rangeProblem =
    to < from ? 'The end date cannot be before the start date.' : days > MAX_RANGE_DAYS ? `The range cannot exceed ${MAX_RANGE_DAYS} days.` : null;

  const filters = {
    from,
    to,
    kind: list.values.kind || undefined,
    locationId: list.values.locationId || undefined,
    skuId: list.values.skuId || undefined,
    batchId: list.values.batchId || undefined,
    referenceKind: list.values.referenceKind || undefined,
    referenceId: list.values.referenceId || undefined,
  };
  // A request is never made for a range the server would refuse.
  const ready = rangeProblem === null;
  const query = useLedger({ ...filters, pageNumber: list.pageNumber, pageSize: list.pageSize }, ready);

  if (query.error instanceof ApiForbiddenError) return <AccessRefusedState error={query.error} />;

  const idFilters = [
    list.values.skuId && { label: `Item: ${itemLabel || 'selected'}`, onClear: () => list.setValue('skuId', '') },
    list.values.batchId && { label: 'One batch', onClear: () => list.setValue('batchId', '') },
    list.values.referenceId && {
      label: `Reference: ${list.values.referenceKind ? (REFERENCE_KIND_LABELS[list.values.referenceKind] ?? list.values.referenceKind) : 'one document'}`,
      onClear: () => {
        list.setValue('referenceId', '');
        list.setValue('referenceKind', '');
      },
    },
  ].filter((f): f is { label: string; onClear: () => void } => !!f);

  return (
    <div className="flex flex-col gap-4">
      <PageHeading
        title="Stock ledger"
        description={`Every stock movement, with times in ${zone}. A search covers at most ${MAX_RANGE_DAYS} days; the default is the last ${DEFAULT_RANGE_DAYS}.`}
      />
      <FilterBar
        label="Search the ledger"
        values={{ ...list.values, from, to }}
        onChange={list.setValue}
        onCommit={list.flush}
        fields={[
          { name: 'from', label: 'From', type: 'date' },
          { name: 'to', label: 'To', type: 'date' },
          {
            name: 'kind',
            label: 'What happened',
            type: 'select',
            options: [{ value: '', label: 'Anything' }, ...Object.entries(LEDGER_KIND_LABELS).map(([value, label]) => ({ value, label }))],
          },
          {
            name: 'locationId',
            label: 'Location',
            type: 'select',
            options: [{ value: '', label: 'All locations' }, ...(locations.data ?? []).map((l) => ({ value: l.id, label: l.name }))],
          },
        ]}
      />
      <div className="max-w-md">
        <SkuPicker
          id="ledger-sku"
          label="Limit to one item"
          onSelect={(sku) => {
            setItemLabel(`${sku.displayName} — ${sku.skuCode}`);
            list.setValue('skuId', sku.skuId);
          }}
        />
      </div>
      <LedgerSearch
        rows={ready ? query.data?.items : undefined}
        totalCount={query.data?.totalCount ?? 0}
        pageSize={list.pageSize}
        isLoading={ready && query.isLoading}
        isError={ready && query.isError}
        onRetry={() => void query.refetch()}
        rangeProblem={rangeProblem}
        exportPath={getLedgerExportPath(filters)}
        timeZoneId={zone}
        idFilters={idFilters}
      />
    </div>
  );
}

export default function LedgerPage() {
  return (
    <Suspense fallback={<LoadingState rows={5} />}>
      <LedgerContent />
    </Suspense>
  );
}
