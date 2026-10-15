import { Suspense } from 'react';
import { OfflineBoard } from '@/components/offline/OfflineBoard';
import { LoadingState } from '@/components/common/LoadingState';

// HO-XCT-002 §12.2 — contains NO server data, so the shell is cacheable by the service worker and
// safe to cache: everything shown comes from IndexedDB in the browser.
export const dynamic = 'force-static';

export default function OfflineBoardPage() {
  return (
    <Suspense fallback={<LoadingState variant="card" rows={4} />}>
      <OfflineBoard />
    </Suspense>
  );
}
