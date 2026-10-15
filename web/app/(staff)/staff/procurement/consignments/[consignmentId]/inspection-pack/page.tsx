'use client';

import { use } from 'react';
import { QueryBoundary } from '@/components/inventory/QueryBoundary';
import { InspectionPack } from '@/components/procurement/InspectionPack';
import { useInspectionPack } from '@/lib/hooks/useProcurement';

/** `/staff/procurement/consignments/[consignmentId]/inspection-pack` — US-IMP-03 AC2. */
export default function InspectionPackPage({ params }: { params: Promise<{ consignmentId: string }> }) {
  const { consignmentId } = use(params);
  const query = useInspectionPack(consignmentId);
  return <QueryBoundary query={query}>{(pack) => <InspectionPack pack={pack} />}</QueryBoundary>;
}
