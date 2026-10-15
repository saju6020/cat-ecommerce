'use client';

import { Suspense, use } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { LoadingState } from '@/components/common/LoadingState';
import { PageHeading } from '@/components/inventory/fields';
import { QueryBoundary } from '@/components/inventory/QueryBoundary';
import { PurchaseOrderBuilder, lineFromSku } from '@/components/procurement/PurchaseOrderBuilder';
import { PurchaseOrderDetail } from '@/components/procurement/PurchaseOrderDetail';
import type { PurchaseOrderDetail as PurchaseOrderDetailData } from '@/lib/api/procurement-orders';
import { todayInZone } from '@/lib/format/time';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useLocations, useSkusByCode } from '@/lib/hooks/useInventoryLookup';
import {
  usePurchaseOrder,
  usePurchaseOrderActions,
  useReplacePurchaseOrderDraft,
  useSuppliers,
} from '@/lib/hooks/useProcurement';

function EditDraft({ order }: { order: PurchaseOrderDetailData }) {
  const router = useRouter();
  const suppliers = useSuppliers({ pageSize: 100 });
  const locations = useLocations();
  const replace = useReplacePurchaseOrderDraft(order.id);
  const resolved = useSkusByCode(order.lines.map((l) => l.skuCode));
  const zone = useBranchTimeZone();
  if (resolved.some((r) => r.isLoading) || suppliers.isLoading) return <LoadingState rows={5} />;
  const lines = order.lines.flatMap((l, index) => {
    const sku = resolved[index]?.data;
    return sku ? [lineFromSku(sku, l.orderedQuantity, l.unitCost.amount)] : [];
  });
  return (
    <div className="flex flex-col gap-4">
      <PageHeading
        title={`Edit ${order.number}`}
        description="Only a draft can be edited. Save, then issue it from the order page."
      />
      <PurchaseOrderBuilder
        initial={{
          supplierId: order.supplierId,
          deliverToLocationId: order.deliverToLocationId,
          currencyCode: order.currencyCode,
          expectedDeliveryOn: order.expectedDeliveryOn,
          notes: order.notes ?? '',
          lines,
        }}
        suppliers={suppliers.data?.items ?? []}
        locations={locations.data}
        today={todayInZone(zone)}
        submitLabel="Save draft"
        isPending={replace.isPending}
        onSubmit={async (request) => {
          await replace.mutateAsync(request);
          router.push(`/staff/procurement/purchase-orders/${order.id}`);
        }}
      />
    </div>
  );
}

function PurchaseOrderContent({ id }: { id: string }) {
  const editing = useSearchParams().get('edit') === '1';
  const query = usePurchaseOrder(id);
  const actions = usePurchaseOrderActions(id);
  const locations = useLocations();
  const zone = useBranchTimeZone();
  return (
    <QueryBoundary
      query={query}
      notFoundTitle="This purchase order could not be found"
      notFoundDescription="It may belong to another branch."
    >
      {(order) =>
        editing && order.allowedActions.includes('edit') ? (
          <EditDraft order={order} />
        ) : (
          <PurchaseOrderDetail
            order={order}
            deliverTo={locations.data?.find((l) => l.id === order.deliverToLocationId)?.name ?? '—'}
            timeZoneId={zone}
            onIssue={() => actions.issue.mutateAsync()}
            onCancel={(reason) => actions.cancel.mutateAsync(reason)}
            onClose={(reason) => actions.close.mutateAsync(reason)}
          />
        )
      }
    </QueryBoundary>
  );
}

/** `/staff/procurement/purchase-orders/[purchaseOrderId]` — US-IMP-02 (`?edit=1` edits a draft). */
export default function PurchaseOrderPage({
  params,
}: {
  params: Promise<{ purchaseOrderId: string }>;
}) {
  const { purchaseOrderId } = use(params);
  return (
    <Suspense fallback={<LoadingState rows={5} />}>
      <PurchaseOrderContent id={purchaseOrderId} />
    </Suspense>
  );
}
