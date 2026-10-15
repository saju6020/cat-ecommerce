import { Suspense } from 'react';
import { CodScreen } from '@/components/billing/CodScreen';

/** /staff/billing/reconciliation/cod - HO-BIL-002 section 11.7 (US-BIL-06 AC1). */
export default function CodPage() {
  return (
    <Suspense fallback={null}>
      <CodScreen />
    </Suspense>
  );
}
