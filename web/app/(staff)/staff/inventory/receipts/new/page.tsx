'use client';

import { Suspense, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { LoadingState } from '@/components/common/LoadingState';
import { GoodsReceiptForm } from '@/components/inventory/GoodsReceiptForm';
import { PageHeading } from '@/components/inventory/fields';
import { linesFromPurchaseOrder } from '@/components/inventory/receiptModel';
import { ApiForbiddenError } from '@/lib/api/client';
import type { SkuLookup } from '@/lib/api/inventory-lookup';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { usePostGoodsReceipt } from '@/lib/hooks/useInventory';
import { useBarcodeLookup, useLocations, useSkusByCode } from '@/lib/hooks/useInventoryLookup';
import { useConsignments, usePurchaseOrder, usePurchaseOrders } from '@/lib/hooks/useProcurement';
import { useStaffSession } from '@/lib/hooks/useSession';

/**
 * `/staff/inventory/receipts/new` — US-IMP-04, the goods-receipt screen. A person who cannot read
 * purchase orders cannot receive (every receiver holds `Procurement.View` too), so the server's
 * `403` on the order list is this page's permission check — `PermissionDeniedState`, not a UI rule.
 */
function NewReceiptContent() {
  const router = useRouter();
  const params = useSearchParams();
  const initialPo = params.get('purchaseOrderId') ?? '';
  const initialCn = params.get('consignmentId') ?? '';
  const session = useStaffSession();
  const orders = usePurchaseOrders({ status: 'Issued', pageSize: 100 });
  const consignments = useConsignments({ status: 'Cleared', pageSize: 100 });
  const locations = useLocations();
  const [poId, setPoId] = useState(initialPo);
  const po = usePurchaseOrder(poId || null);
  const skus = useSkusByCode(po.data?.lines.map((l) => l.skuCode) ?? []);
  const lookup = useBarcodeLookup();
  const post = usePostGoodsReceipt();
  const zone = useBranchTimeZone();

  if (orders.error instanceof ApiForbiddenError) return <AccessRefusedState error={orders.error} />;
  const staffAccountId = session.data?.staffAccountId;
  if (!staffAccountId || orders.isLoading || locations.isLoading) return <LoadingState rows={5} />;

  const skusByCode: Record<string, SkuLookup | null | undefined> = {};
  po.data?.lines.forEach((l, i) => {
    skusByCode[l.skuCode] = skus[i]?.data;
  });
  const ready = po.data && skus.every((q) => !q.isLoading);

  return (
    <div className="flex flex-col gap-4">
      <PageHeading
        title="Receive goods"
        description="Scan or pick each item, enter the lot and dates, then post the receipt in one action."
      />
      <GoodsReceiptForm
        staffAccountId={staffAccountId}
        locations={locations.data}
        purchaseOrders={orders.data?.items ?? []}
        consignments={consignments.data?.items ?? []}
        purchaseOrderLines={ready ? { purchaseOrderId: po.data.id, lines: linesFromPurchaseOrder(po.data, skusByCode) } : null}
        initial={
          initialPo
            ? { source: 'purchase-order', purchaseOrderId: initialPo }
            : initialCn
              ? { source: 'consignment', consignmentId: initialCn }
              : undefined
        }
        onPurchaseOrderChange={setPoId}
        onScan={lookup}
        isPosting={post.isPending}
        timeZoneLabel={zone}
        onPost={async (request) => {
          const posted = await post.mutateAsync(request);
          router.push(`/staff/inventory/receipts/${posted.goodsReceiptId}`);
        }}
      />
    </div>
  );
}

export default function NewReceiptPage() {
  return (
    <Suspense fallback={<LoadingState rows={5} />}>
      <NewReceiptContent />
    </Suspense>
  );
}
