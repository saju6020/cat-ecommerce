import { Suspense } from 'react';
import { AgeingTable } from '@/components/billing/AgeingTable';

/** /staff/billing/receivables - HO-BIL-002 section 11.5 (US-BIL-03 AC2). */
export default function ReceivablesPage() {
  return (
    <Suspense fallback={null}>
      <AgeingTable />
    </Suspense>
  );
}
