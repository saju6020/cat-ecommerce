'use client';

import * as React from 'react';
import { MaintenanceNotice } from '@/components/shop/MaintenanceNotice';
import { useShopMaintenance } from '@/lib/hooks/useShopCatalogue';
import type { StorefrontStatus } from '@/lib/api/shop-catalogue';

/**
 * HO-SHP-002 §11.1 `MaintenanceGate`, US-ADM-08. The layout reads `GET /api/system/status` on the
 * server and passes it here as `initial`; this boundary confirms it on mount (a page cached for up
 * to 60 s must still give way quickly) and, while maintenance is on, renders the notice INSTEAD of
 * the page. The back office and the portal live in other route groups and are unaffected.
 */
export function MaintenanceGate({
  initial,
  children,
}: {
  initial: StorefrontStatus;
  children: React.ReactNode;
}) {
  const { data } = useShopMaintenance(initial);
  if (data.maintenanceMode) return <MaintenanceNotice message={data.message} />;
  return <>{children}</>;
}
