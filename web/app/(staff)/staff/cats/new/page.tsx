'use client';

import { useRouter } from 'next/navigation';
import { CatForm, type CatFormValues } from '@/components/cat/CatForm';
import { useRegisterCat } from '@/lib/hooks/useCatMutations';
import { toast } from '@/components/ui/use-toast';
import type { CatSex, DateOfBirthPrecision } from '@/lib/api/cats';

/** Register a cat — US-CAT-01 (HO-CAT-002 §8.3). E1/E2. */
export default function RegisterCatPage() {
  const router = useRouter();
  const registerCat = useRegisterCat();

  async function handleSubmit(values: CatFormValues) {
    const result = await registerCat.mutateAsync({
      ownerId: values.ownerId ?? '',
      name: values.name,
      species: values.species || 'Cat',
      sex: values.sex as CatSex,
      dateOfBirth: values.dateOfBirth || null,
      dateOfBirthPrecision: values.dateOfBirthPrecision as DateOfBirthPrecision,
      breed: values.breed || null,
      colourAndMarkings: values.colourAndMarkings || null,
    });
    toast({ title: 'Cat registered', description: `${values.name} was registered successfully.` });
    router.push(`/staff/cats/${result.catId}`);
  }

  return (
    <div className="flex flex-col gap-6">
      <h1 className="font-display text-2xl font-semibold text-ink">Register a cat</h1>
      <CatForm mode="register" onSubmit={handleSubmit} isSubmitting={registerCat.isPending} />
    </div>
  );
}
