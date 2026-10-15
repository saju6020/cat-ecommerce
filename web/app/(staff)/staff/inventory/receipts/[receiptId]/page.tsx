'use client';

import { use } from 'react';
import { QueryBoundary } from '@/components/inventory/QueryBoundary';
import { ReceiptDetail } from '@/components/inventory/ReceiptDetail';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useGoodsReceipt } from '@/lib/hooks/useInventory';

/** `/staff/inventory/receipts/[receiptId]` — a posted receipt, read-only (US-IMP-04). */
export default function ReceiptPage({ params }: { params: Promise<{ receiptId: string }> }) {
  const { receiptId } = use(params);
  const query = useGoodsReceipt(receiptId);
  const zone = useBranchTimeZone();
  return (
    <QueryBoundary query={query} notFoundTitle="This receipt could not be found" notFoundDescription="It may belong to another branch.">
      {(receipt) => <ReceiptDetail receipt={receipt} timeZoneId={zone} />}
    </QueryBoundary>
  );
}
