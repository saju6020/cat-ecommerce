'use client';

import { use } from 'react';
import { VisitView } from './VisitView';

/** `/staff/visits/[visitId]` — HO-EMR-002 §8.1 (US-CLN-11…22): the consultation workspace / record. */
export default function VisitPage({ params }: { params: Promise<{ visitId: string }> }) {
  const { visitId } = use(params);
  return <VisitView visitId={visitId} />;
}
