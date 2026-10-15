'use client';

import { useEffect } from 'react';
import { useDeviceSettings } from '@/lib/hooks/useOfflineSnapshot';
import { reconcileServiceWorker } from '@/lib/offline/sw-register';

/**
 * `components/offline/ServiceWorkerRegistrar.tsx` — HO-XCT-002 §12.1, ADR-FE-139/140. Registers
 * `/staff/sw.js` (scope `/staff/`) only in a production build, never in mock mode and only while
 * the device opt-in is on; in every other case it removes any worker this feature left behind.
 * Mounted in the `(staff)` layout, so a customer route never reaches it. Renders nothing.
 */
export function ServiceWorkerRegistrar() {
  const { settings, loaded } = useDeviceSettings();

  useEffect(() => {
    if (!loaded) return;
    void reconcileServiceWorker({
      nodeEnv: process.env.NODE_ENV,
      useMocks: process.env.NEXT_PUBLIC_USE_MOCKS,
      deviceEnabled: settings.offlineEnabled,
    });
  }, [loaded, settings.offlineEnabled]);

  return null;
}
