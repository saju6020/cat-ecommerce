'use client';

import { use } from 'react';
import { FileDown } from 'lucide-react';
import { buttonVariants } from '@/components/ui/button';
import { getCatMedicalHistoryPdfUrl } from '@/lib/api/cats';

/** Medical-history PDF download — US-CAT-10 (HO-CAT-002 §8.3). */
export default function PortalMedicalHistoryPage({
  params,
}: {
  params: Promise<{ catId: string }>;
}) {
  const { catId } = use(params);

  return (
    <div className="flex flex-col gap-4">
      <h2 className="text-lg font-bold text-ink">Medical history</h2>
      <p className="text-sm text-ink-muted">
        Download a PDF of this cat&rsquo;s profile, vaccinations, parasite treatments and weight
        history.
      </p>
      <a
        href={getCatMedicalHistoryPdfUrl(catId)}
        download
        className={buttonVariants({ variant: 'primary', className: 'w-fit' })}
      >
        <FileDown size={16} aria-hidden="true" />
        Download medical history (PDF)
      </a>
    </div>
  );
}
