'use client';

import { use } from 'react';
import { MyCatVisits } from '@/components/visit/MyCatVisits';

/** `/portal/cats/[catId]/visits` — HO-EMR-002 §8.1/§11.5 (US-CLN-16, FR-POR-3): finalised visits and lab results. */
export default function PortalCatVisitsPage({ params }: { params: Promise<{ catId: string }> }) {
  const { catId } = use(params);
  return <MyCatVisits catId={catId} />;
}
