'use client';

import { Suspense, useMemo } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { LoadingState } from '@/components/common/LoadingState';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { PageHeading } from '@/components/inventory/fields';
import { PurchaseOrderBuilder, lineFromSku } from '@/components/procurement/PurchaseOrderBuilder';
import type { PoLineState } from '@/components/procurement/PurchaseOrderLineEditor';
import { ApiForbiddenError } from '@/lib/api/client';
import { todayInZone } from '@/lib/format/time';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useLocations, useSkusByCode } from '@/lib/hooks/useInventoryLookup';
import { useCreatePurchaseOrder, useSuppliers } from '@/lib/hooks/useProcurement';

/** `/staff/procurement/purchase-orders/new` — US-IMP-02. Pre-filled from `?supplierId=&lines=CODE:qty,…`
 *  (the reorder hand-off, OQ-2): the lines arrive with their quantities, the cost is still typed. */
function NewPurchaseOrderContent() {
  const router = useRouter();
  const params = useSearchParams();
  const supplierId = params.get('supplierId') ?? '';
  const prefill = useMemo(
    () =>
      (params.get('lines') ?? '')
        .split(',')
        .map((part) => part.split(':'))
        .filter((pair): pair is [string, string] => pair.length === 2 && pair[0] !== ''),
    [params],
  );
  const resolved = useSkusByCode(prefill.map(([code]) => code));
  const suppliers = useSuppliers({ isActive: 'true', pageSize: 100 });
  const locations = useLocations();
  const create = useCreatePurchaseOrder();
  const zone = useBranchTimeZone();

  if (suppliers.error instanceof ApiForbiddenError) return <AccessRefusedState error={suppliers.error} />;
  if (suppliers.isLoading || resolved.some((r) => r.isLoading)) return <LoadingState rows={5} />;

  const lines: PoLineState[] = prefill.flatMap(([, quantity], index) => {
    const sku = resolved[index]?.data;
    return sku ? [lineFromSku(sku, quantity)] : [];
  });
  const supplier = suppliers.data?.items.find((s) => s.id === supplierId);

  return (
    <div className="flex flex-col gap-4">
      <PageHeading
        title="New purchase order"
        description={
          prefill.length > 0
            ? 'Pre-filled from your reorder suggestions. Add the unit costs, then save the draft.'
            : 'Save a draft, then issue it from the order page.'
        }
      />
      <PurchaseOrderBuilder
        key={`${supplierId}|${prefill.length}`}
        initial={{
          supplierId,
          deliverToLocationId: '',
          currencyCode: supplier?.currencyCode ?? '',
          expectedDeliveryOn: '',
          notes: '',
          lines,
        }}
        suppliers={suppliers.data?.items ?? []}
        locations={locations.data}
        today={todayInZone(zone)}
        submitLabel="Save draft"
        isPending={create.isPending}
        onSubmit={async (request) => {
          const created = await create.mutateAsync(request);
          router.push(`/staff/procurement/purchase-orders/${created.purchaseOrderId}`);
        }}
      />
    </div>
  );
}

export default function NewPurchaseOrderPage() {
  return (
    <Suspense fallback={<LoadingState rows={5} />}>
      <NewPurchaseOrderContent />
    </Suspense>
  );
}
