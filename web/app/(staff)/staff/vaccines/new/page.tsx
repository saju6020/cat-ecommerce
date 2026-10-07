'use client';

import { useRouter } from 'next/navigation';
import { VaccineForm, type VaccineFormValues } from '@/components/cat/VaccineForm';
import { useRegisterVaccine } from '@/lib/hooks/useVaccineMutations';
import { toast } from '@/components/ui/use-toast';

/** Register a vaccine — the catalogue entry US-CAT-05 depends on (HO-CAT-002 §8.3). */
export default function RegisterVaccinePage() {
  const router = useRouter();
  const registerVaccine = useRegisterVaccine();

  async function handleSubmit(values: VaccineFormValues) {
    await registerVaccine.mutateAsync({
      code: values.code,
      name: values.name,
      isCore: values.isCore,
      validityMonths: values.validityMonths,
      description: values.description || null,
    });
    toast({ title: 'Vaccine registered' });
    router.push('/staff/vaccines');
  }

  return (
    <div className="flex flex-col gap-6">
      <h1 className="font-display text-2xl font-semibold text-ink">Register a vaccine</h1>
      <VaccineForm onSubmit={handleSubmit} isSubmitting={registerVaccine.isPending} />
    </div>
  );
}
