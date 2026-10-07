import { Suspense } from 'react';
import { LoadingState } from '@/components/common/LoadingState';
import { FrontDeskView } from './FrontDeskView';

/** `/staff/front-desk` — HO-APT-002 §8.1/§11.5 (US-CLN-05, 08, 10): *Today* and *Queue*. */
export default function FrontDeskPage() {
  return (
    <Suspense fallback={<LoadingState variant="card" rows={5} label="Loading the front desk…" />}>
      <FrontDeskView />
    </Suspense>
  );
}
