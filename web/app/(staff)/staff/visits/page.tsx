import { Suspense } from 'react';
import { LoadingState } from '@/components/common/LoadingState';
import { OpenVisitsView } from './OpenVisitsView';

/** `/staff/visits` — HO-EMR-002 §8.1/§11.9: the branch's open visits (UI-5, polled every 30 s). */
export default function OpenVisitsPage() {
  return (
    <Suspense fallback={<LoadingState variant="table" rows={5} label="Loading the open visits…" />}>
      <OpenVisitsView />
    </Suspense>
  );
}
