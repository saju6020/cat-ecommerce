'use client';

import { use } from 'react';
import { SurgeryWorkspace } from '@/components/surgery/SurgeryWorkspace';

/** `/staff/surgeries/[surgeryId]` — HO-SRG-002 §8.1: the surgery workspace (tabs by URL hash). */
export default function SurgeryPage({ params }: { params: Promise<{ surgeryId: string }> }) {
  const { surgeryId } = use(params);
  return <SurgeryWorkspace surgeryId={surgeryId} />;
}
