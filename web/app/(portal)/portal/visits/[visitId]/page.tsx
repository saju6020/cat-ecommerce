'use client';

import { use } from 'react';
import { MyVisitSummary } from '@/components/visit/MyVisitSummary';

/** `/portal/visits/[visitId]` — HO-EMR-002 §8.1/§11.5 (US-CLN-16): the discharge summary and its PDF. */
export default function PortalVisitPage({ params }: { params: Promise<{ visitId: string }> }) {
  const { visitId } = use(params);
  return <MyVisitSummary visitId={visitId} />;
}
