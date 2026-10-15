'use client';

import { useEffect } from 'react';
import { serviceWorkerAllowed, unregisterOfflineServiceWorker } from '@/lib/offline/sw-register';

/**
 * `components/offline/ServiceWorkerCleanup.tsx` — HO-XCT-002 §12.1, ADR-FE-140, §20.9. Mounted in the
 * root layout so it runs on EVERY page load, including `/staff/sign-in` (which sits outside the
 * `(staff)` layout): in development and in mock mode any `/staff/` worker left by an earlier
 * production build is unregistered, because it would otherwise take over `/staff/**` and keep MSW
 * from seeing the sign-in request that would let the staff layout clean it up. Production builds
 * with mocks off leave it alone (the `(staff)` registrar owns it there). Renders nothing.
 */
export function ServiceWorkerCleanup() {
  useEffect(() => {
    const mayExist = serviceWorkerAllowed({
      nodeEnv: process.env.NODE_ENV,
      useMocks: process.env.NEXT_PUBLIC_USE_MOCKS,
      deviceEnabled: true,
    });
    if (!mayExist) void unregisterOfflineServiceWorker();
  }, []);
  return null;
}
