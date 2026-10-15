import { Suspense } from 'react';
import { ExportPanel } from '@/components/billing/ExportPanel';

/** /staff/billing/exports - HO-BIL-002 section 11.9 (US-BIL-09). */
export default function ExportsPage() {
  return (
    <Suspense fallback={null}>
      <ExportPanel />
    </Suspense>
  );
}
