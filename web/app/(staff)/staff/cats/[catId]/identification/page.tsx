'use client';

import { use } from 'react';
import { NeuterStatusForm, type NeuterStatusFormValues } from '@/components/cat/NeuterStatusForm';
import { DeceasedBanner } from '@/components/cat/DeceasedBanner';
import { useCat } from '@/lib/hooks/useCat';
import { useRecordCatNeuterStatus } from '@/lib/hooks/useCatMutations';
import { toast } from '@/components/ui/use-toast';
import type { NeuterStatus } from '@/lib/api/cats';

/** Neuter status + date — US-CAT-02 (HO-CAT-002 §8.3). */
export default function IdentificationPage({ params }: { params: Promise<{ catId: string }> }) {
  const { catId } = use(params);
  const catQuery = useCat(catId);
  const recordNeuterStatus = useRecordCatNeuterStatus(catId);

  if (!catQuery.data) return null;
  const cat = catQuery.data;
  const isDeceased = cat.lifeStatus === 'Deceased';

  async function handleSubmit(values: NeuterStatusFormValues) {
    await recordNeuterStatus.mutateAsync({
      neuterStatus: values.neuterStatus as NeuterStatus,
      neuteredOn: values.neuterStatus === 'Neutered' ? (values.neuteredOn ?? null) : null,
    });
    toast({ title: 'Neuter status saved' });
  }

  return (
    <div className="flex flex-col gap-6">
      <h2 className="text-lg font-bold text-ink">Identification</h2>
      {isDeceased && <DeceasedBanner deceasedOn={cat.deceasedOn} deceasedNote={cat.deceasedNote} />}
      <fieldset disabled={isDeceased} className="contents">
        <NeuterStatusForm
          defaultValues={{ neuterStatus: cat.neuterStatus, neuteredOn: cat.neuteredOn ?? '' }}
          onSubmit={handleSubmit}
          isSubmitting={recordNeuterStatus.isPending}
        />
      </fieldset>
    </div>
  );
}
