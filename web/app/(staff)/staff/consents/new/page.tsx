import { Suspense } from 'react';
import { LoadingState } from '@/components/common/LoadingState';
import { ConsentNewView } from './ConsentNewView';

/** `/staff/consents/new?catId&appointmentId&kind` — HO-EMR-002 §8.1/§11.6 (US-CLN-21): consent capture. */
export default function NewConsentPage() {
  return (
    <Suspense fallback={<LoadingState variant="card" rows={6} label="Loading the consent form…" />}>
      <ConsentNewView />
    </Suspense>
  );
}
