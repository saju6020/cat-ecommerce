'use client';

import { Suspense, useMemo } from 'react';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { LoadingState } from '@/components/common/LoadingState';
import { FilterBar } from '@/components/inventory/FilterBar';
import { PageHeading } from '@/components/inventory/fields';
import { ReorderPolicyTable, type PolicyUnit } from '@/components/procurement/ReorderPolicyTable';
import { ApiForbiddenError } from '@/lib/api/client';
import { useInventoryListState } from '@/lib/hooks/useInventoryListState';
import { useLocations, useSkusByCode } from '@/lib/hooks/useInventoryLookup';
import { useReorderPolicies, useSetReorderPolicy, useSuppliers } from '@/lib/hooks/useProcurement';
import { useStaffSession } from '@/lib/hooks/useSession';

const FIELDS = ['locationId'] as const;
/** Presentation only (HO-IMP-001 §12.3 `ReorderPolicies.Manage`): the server answers any save. */
const MANAGE_ROLES = ['Inventory', 'Administrator'];

/** `/staff/inventory/reorder-policies` — US-IMP-12. */
function PoliciesContent() {
  const list = useInventoryListState(FIELDS);
  const query = useReorderPolicies(list.params);
  const locations = useLocations();
  const suppliers = useSuppliers({ isActive: 'true', pageSize: 100 });
  const save = useSetReorderPolicy();
  const session = useStaffSession();
  const codes = useMemo(() => [...new Set((query.data?.items ?? []).map((p) => p.skuCode))], [query.data]);
  const skus = useSkusByCode(codes);
  const units = useMemo(() => {
    const map: Record<string, PolicyUnit | undefined> = {};
    skus.forEach((result) => {
      const sku = result.data;
      if (sku) map[sku.skuId] = { stockUnit: sku.stockUnit, allowsFractional: sku.allowsFractionalQuantity };
    });
    return map;
  }, [skus]);
  if (query.error instanceof ApiForbiddenError) return <AccessRefusedState error={query.error} />;
  return (
    <div className="flex flex-col gap-4">
      <PageHeading title="Reorder policies" description="When an item at a location drops to its reorder point, it shows up in alerts and reorder suggestions." />
      <FilterBar
        label="Filter reorder policies"
        values={list.values}
        onChange={list.setValue}
        fields={[
          {
            name: 'locationId',
            label: 'Location',
            type: 'select',
            options: [{ value: '', label: 'All locations' }, ...(locations.data ?? []).map((l) => ({ value: l.id, label: l.name }))],
          },
        ]}
      />
      <ReorderPolicyTable
        rows={query.data?.items}
        totalCount={query.data?.totalCount ?? 0}
        pageSize={list.pageSize}
        isLoading={query.isLoading}
        isError={query.isError}
        onRetry={() => void query.refetch()}
        units={units}
        suppliers={suppliers.data?.items ?? []}
        canEdit={(session.data?.roleIds ?? []).some((role) => MANAGE_ROLES.includes(role))}
        onSave={(policy, request) => save.mutateAsync({ skuId: policy.skuId, locationId: policy.locationId, request })}
      />
    </div>
  );
}

export default function ReorderPoliciesPage() {
  return (
    <Suspense fallback={<LoadingState rows={5} />}>
      <PoliciesContent />
    </Suspense>
  );
}
