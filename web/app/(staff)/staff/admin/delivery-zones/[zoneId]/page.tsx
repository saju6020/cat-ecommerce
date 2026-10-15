'use client';

import { use } from 'react';
import { EditZoneScreen } from '@/components/ordering-admin/EditScreens';

/** `/staff/admin/delivery-zones/[zoneId]` — HO-ORD-002 §11.12. */
export default function Page({ params }: { params: Promise<{ zoneId: string }> }) {
  const { zoneId } = use(params);
  return <EditZoneScreen zoneId={zoneId} />;
}
