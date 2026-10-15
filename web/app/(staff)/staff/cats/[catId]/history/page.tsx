'use client';

import { use } from 'react';
import { ClinicalHistoryView } from '@/components/clinical-history/ClinicalHistoryView';

/**
 * `/staff/cats/[catId]/history` — HO-EMR-002 §8.1/§11.4 (US-CLN-13): the same sections as the drawer, as
 * a page under the cat layout (which carries the FULL cat header — UI-7). A left section list on a
 * wide screen, stacked on a phone.
 */
export default function CatClinicalHistoryPage({ params }: { params: Promise<{ catId: string }> }) {
  const { catId } = use(params);
  return (
    <div className="flex flex-col gap-4">
      <h2 className="text-lg font-bold text-ink">Clinical history</h2>
      <ClinicalHistoryView catId={catId} layout="page" />
    </div>
  );
}
