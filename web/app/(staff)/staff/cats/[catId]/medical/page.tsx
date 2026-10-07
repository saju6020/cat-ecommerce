'use client';

import { use } from 'react';
import {
  MedicalProfileForm,
  type MedicalProfileFormValues,
} from '@/components/cat/MedicalProfileForm';
import { DeceasedBanner } from '@/components/cat/DeceasedBanner';
import { useCat } from '@/lib/hooks/useCat';
import { useUpdateCatMedicalProfile } from '@/lib/hooks/useCatMutations';
import { toast } from '@/components/ui/use-toast';
import type { AllergySeverity } from '@/lib/api/cats';

/** Allergies, conditions, medications — US-CAT-03 (HO-CAT-002 §8.3). */
export default function MedicalProfilePage({ params }: { params: Promise<{ catId: string }> }) {
  const { catId } = use(params);
  const catQuery = useCat(catId);
  const updateMedicalProfile = useUpdateCatMedicalProfile(catId);

  if (!catQuery.data) return null;
  const cat = catQuery.data;
  const isDeceased = cat.lifeStatus === 'Deceased';

  async function handleSubmit(values: MedicalProfileFormValues) {
    await updateMedicalProfile.mutateAsync({
      allergies: values.allergies.map((a) => ({ ...a, severity: a.severity as AllergySeverity })),
      chronicConditions: values.chronicConditions.map((c) => ({
        condition: c.condition,
        diagnosedOn: c.diagnosedOn || null,
        notes: c.notes || null,
      })),
      medications: values.medications.map((m) => ({ ...m, endsOn: m.endsOn || null })),
      dietaryNotes: values.dietaryNotes || null,
      behaviouralNotes: values.behaviouralNotes || null,
    });
    toast({ title: 'Medical profile saved' });
  }

  return (
    <div className="flex flex-col gap-6">
      <h2 className="text-lg font-bold text-ink">Medical profile</h2>
      {isDeceased && <DeceasedBanner deceasedOn={cat.deceasedOn} deceasedNote={cat.deceasedNote} />}
      <fieldset disabled={isDeceased} className="contents">
        <MedicalProfileForm
          defaultValues={{
            allergies: cat.allergies.map((a) => ({
              allergen: a.allergen,
              reaction: a.reaction,
              severity: a.severity,
              recordedOn: a.recordedOn,
            })),
            chronicConditions: cat.chronicConditions.map((c) => ({
              condition: c.condition,
              diagnosedOn: c.diagnosedOn ?? '',
              notes: c.notes ?? '',
            })),
            medications: cat.medications.map((m) => ({
              medicationName: m.medicationName,
              dosage: m.dosage,
              frequency: m.frequency,
              startedOn: m.startedOn,
              endsOn: m.endsOn ?? '',
            })),
            dietaryNotes: cat.dietaryNotes ?? '',
            behaviouralNotes: cat.behaviouralNotes ?? '',
          }}
          onSubmit={handleSubmit}
          isSubmitting={updateMedicalProfile.isPending}
        />
      </fieldset>
    </div>
  );
}
