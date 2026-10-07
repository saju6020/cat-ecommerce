'use client';

import { use } from 'react';
import { useRouter } from 'next/navigation';
import { CatForm, type CatFormValues } from '@/components/cat/CatForm';
import { DeceasedBanner } from '@/components/cat/DeceasedBanner';
import { useCat } from '@/lib/hooks/useCat';
import { useUpdateCatProfile } from '@/lib/hooks/useCatMutations';
import { toast } from '@/components/ui/use-toast';
import type { CatSex, DateOfBirthPrecision } from '@/lib/api/cats';

/** Edit a cat's identity fields — US-CAT-01 (HO-CAT-002 §8.3). Blocked server-side by
 *  `CatIsDeceased` when the cat is deceased (HO-CAT-001 §8.2's `EnsureAlive()`); the form is
 *  disabled client-side too, with the explanation (US-CAT-09 AC2, component test U30). */
export default function EditCatPage({ params }: { params: Promise<{ catId: string }> }) {
  const { catId } = use(params);
  const router = useRouter();
  const catQuery = useCat(catId);
  const updateProfile = useUpdateCatProfile(catId);

  if (!catQuery.data) return null;
  const cat = catQuery.data;
  const isDeceased = cat.lifeStatus === 'Deceased';

  async function handleSubmit(values: CatFormValues) {
    await updateProfile.mutateAsync({
      name: values.name,
      sex: values.sex as CatSex,
      dateOfBirth: values.dateOfBirth || null,
      dateOfBirthPrecision: values.dateOfBirthPrecision as DateOfBirthPrecision,
      breed: values.breed || null,
      colourAndMarkings: values.colourAndMarkings || null,
    });
    toast({ title: 'Profile updated' });
    router.push(`/staff/cats/${catId}`);
  }

  return (
    <div className="flex flex-col gap-6">
      <h2 className="text-lg font-bold text-ink">Edit profile</h2>
      {isDeceased && <DeceasedBanner deceasedOn={cat.deceasedOn} deceasedNote={cat.deceasedNote} />}
      <fieldset disabled={isDeceased} className="contents">
        <CatForm
          mode="edit"
          defaultValues={{
            name: cat.name,
            species: cat.species,
            sex: cat.sex,
            dateOfBirthPrecision: cat.dateOfBirthPrecision,
            dateOfBirth: cat.dateOfBirth ?? '',
            breed: cat.breed ?? '',
            colourAndMarkings: cat.colourAndMarkings ?? '',
          }}
          onSubmit={handleSubmit}
          isSubmitting={updateProfile.isPending}
        />
      </fieldset>
    </div>
  );
}
