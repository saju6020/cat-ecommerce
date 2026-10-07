'use client';

import { Suspense, use, useMemo, useState } from 'react';
import { useSearchParams, useRouter } from 'next/navigation';
import { DataTable, type DataTableColumn } from '@/components/common/DataTable';
import { WeightTrendChart } from '@/components/cat/WeightTrendChart';
import { WeightEntryForm, type WeightEntryFormValues } from '@/components/cat/WeightEntryForm';
import { Button } from '@/components/ui/button';
import { useCatWeights, useCatWeightSeries } from '@/lib/hooks/useCatWeights';
import { useRecordCatWeight } from '@/lib/hooks/useCatMutations';
import { toast } from '@/components/ui/use-toast';
import { formatDateOnly } from '@/lib/dateOnly';
import type { CatWeightPoint } from '@/lib/api/cats';

/** Weight history + trend chart — US-CAT-02 AC2 (HO-CAT-002 §8.3). */
function WeightsPageContent({ catId }: { catId: string }) {
  const searchParams = useSearchParams();
  const router = useRouter();
  const page = Math.max(1, Number(searchParams.get('page')) || 1);
  const pageSize = 10;
  const [showForm, setShowForm] = useState(false);

  const seriesQuery = useCatWeightSeries(catId);
  const historyQuery = useCatWeights(catId, { pageNumber: page, pageSize });
  const recordWeight = useRecordCatWeight(catId);

  async function handleSubmit(values: WeightEntryFormValues) {
    await recordWeight.mutateAsync({
      kilograms: values.kilograms,
      recordedOn: values.recordedOn,
      notes: values.notes || null,
    });
    toast({ title: 'Weight recorded' });
    setShowForm(false);
    router.replace(`/staff/cats/${catId}/weights`, { scroll: false });
  }

  const columns: Array<DataTableColumn<CatWeightPoint>> = [
    { id: 'recordedOn', header: 'Date', cell: (point) => formatDateOnly(point.recordedOn) },
    { id: 'kilograms', header: 'Weight', cell: (point) => `${point.kilograms} kg` },
  ];

  const weightRows = historyQuery.data?.items;

  // `CatWeightPoint` (Q3) carries no id from the API (HO-CAT-001 §12.3 Q3), so `recordedOn` alone
  // can collide: two weights recorded on the same calendar date (intake, then again before a
  // same-day procedure) would otherwise share a React key. Pair `recordedOn` with this row's
  // position in the current page instead — built once per render with `.map((row, index) => …)`,
  // not a per-row `indexOf` lookup (CODE_REVIEW gate: that was O(n) per row, O(n²) over the whole
  // table). If the API ever adds an id, it goes into `lib/api/cats.ts`'s `CatWeightPoint`, and the
  // compiler leads back to this call site (ARCHITECTURE gate — a contract change should be a type
  // error here, not a silently optional field).
  const weightRowIds = useMemo(() => {
    const ids = new Map<CatWeightPoint, string>();
    weightRows?.forEach((row, index) => ids.set(row, `${row.recordedOn}-${index}`));
    return ids;
  }, [weightRows]);

  function weightRowId(point: CatWeightPoint): string {
    return weightRowIds.get(point) ?? point.recordedOn;
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-lg font-bold text-ink">Weights</h2>
        <Button variant="secondary" onClick={() => setShowForm((value) => !value)}>
          {showForm ? 'Cancel' : 'Record weight'}
        </Button>
      </div>

      {showForm && (
        <WeightEntryForm onSubmit={handleSubmit} isSubmitting={recordWeight.isPending} />
      )}

      {seriesQuery.data && seriesQuery.data.length > 0 && (
        <WeightTrendChart points={seriesQuery.data} />
      )}

      <DataTable
        caption="Weight history"
        columns={columns}
        rows={weightRows}
        getRowId={weightRowId}
        isLoading={historyQuery.isLoading}
        isError={historyQuery.isError}
        onRetry={() => historyQuery.refetch()}
        pagination={{ pageSize, totalCount: historyQuery.data?.totalCount ?? 0 }}
        emptyState={{
          title: 'No weights recorded yet.',
          description: 'Record a weight to start this cat’s trend.',
        }}
      />
    </div>
  );
}

export default function WeightsPage({ params }: { params: Promise<{ catId: string }> }) {
  const { catId } = use(params);
  return (
    <Suspense>
      <WeightsPageContent catId={catId} />
    </Suspense>
  );
}
