'use client';

import { use } from 'react';
import { ReturnDetailScreen } from '@/components/returns/ReturnDetail';

/** `/staff/returns/[returnRequestId]` — HO-ORD-002 §11.10 (US-ORD-13, 14): approve, reject, receive. */
export default function ReturnPage({ params }: { params: Promise<{ returnRequestId: string }> }) {
  const { returnRequestId } = use(params);
  return <ReturnDetailScreen returnRequestId={returnRequestId} />;
}
