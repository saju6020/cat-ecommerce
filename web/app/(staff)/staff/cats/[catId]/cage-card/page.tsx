'use client';

import { use } from 'react';
import { CageCard } from '@/components/cat/CageCard';
import { Button } from '@/components/ui/button';
import { useCat } from '@/lib/hooks/useCat';
import { getCatCageCardImageUrl } from '@/lib/api/cats';

/** Printable cage card with QR — US-CAT-11 (HO-CAT-002 §8.3). */
export default function CageCardPage({ params }: { params: Promise<{ catId: string }> }) {
  const { catId } = use(params);
  const catQuery = useCat(catId);

  if (!catQuery.data) return null;
  const cat = catQuery.data;

  return (
    <div className="flex flex-col gap-6 print:gap-0">
      <div className="flex items-center justify-between print:hidden">
        <h2 className="text-lg font-bold text-ink">Cage card</h2>
        <Button onClick={() => window.print()}>Print</Button>
      </div>
      <CageCard
        catName={cat.name}
        qrImageUrl={getCatCageCardImageUrl(catId)}
        isHighRisk={cat.isHighRisk}
        handlingNote={cat.handlingNote}
      />
    </div>
  );
}
