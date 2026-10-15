'use client';

import { Suspense, use, useMemo } from 'react';
import { useSearchParams } from 'next/navigation';
import { LoadingState } from '@/components/common/LoadingState';
import { QueryBoundary } from '@/components/inventory/QueryBoundary';
import { StorageBreachDetailView } from '@/components/inventory/StorageBreachDetail';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useLocations, useSkusByCode } from '@/lib/hooks/useInventoryLookup';
import { useBreach, useBreachActions } from '@/lib/hooks/useInventoryRecalls';

function BreachContent({ id }: { id: string }) {
  const reported = useSearchParams().get('reported');
  const query = useBreach(id);
  const actions = useBreachActions(id);
  const locations = useLocations();
  const zone = useBranchTimeZone();
  const codes = useMemo(
    () => [...new Set((query.data?.affectedBatches ?? []).flatMap((b) => (b.skuCode ? [b.skuCode] : [])))],
    [query.data],
  );
  const skus = useSkusByCode(codes);
  const unitBySku = useMemo(
    () => Object.fromEntries(skus.flatMap((r) => (r.data ? [[r.data.skuCode, r.data.stockUnit]] : []))),
    [skus],
  );
  const quarantine = locations.data?.find((l) => l.kind === 'Quarantine' && l.isActive);
  return (
    <QueryBoundary query={query} notFoundTitle="This storage-breach report could not be found" notFoundDescription="It may belong to another branch.">
      {(breach) => (
        <StorageBreachDetailView
          breach={breach}
          unitBySku={unitBySku}
          quarantineName={quarantine ? `${quarantine.name} (${quarantine.code})` : null}
          justReported={reported === null ? null : parseInt(reported, 10)}
          timeZoneId={zone}
          onQuarantine={() => actions.quarantine.mutateAsync()}
          onClose={(note) => actions.close.mutateAsync(note)}
        />
      )}
    </QueryBoundary>
  );
}

/** `/staff/inventory/storage-breaches/[breachId]` — US-IMP-16 AC2. */
export default function BreachPage({ params }: { params: Promise<{ breachId: string }> }) {
  const { breachId } = use(params);
  return (
    <Suspense fallback={<LoadingState rows={4} />}>
      <BreachContent id={breachId} />
    </Suspense>
  );
}
