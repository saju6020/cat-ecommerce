import { Suspense } from 'react';
import { LoadingState } from '@/components/common/LoadingState';
import { PendingDiagnosticsView } from './PendingDiagnosticsView';

/** `/staff/diagnostics` — HO-EMR-002 §8.1/§11.9 (US-CLN-14): orders waiting for results (polled every 30 s). */
export default function PendingDiagnosticsPage() {
  return (
    <Suspense
      fallback={<LoadingState variant="table" rows={5} label="Loading the pending orders…" />}
    >
      <PendingDiagnosticsView />
    </Suspense>
  );
}
