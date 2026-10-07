'use client';

import { Suspense, useRef, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { CatForm, type CatFormValues } from '@/components/cat/CatForm';
import { useRegisterCat } from '@/lib/hooks/useCatMutations';
import { toast } from '@/components/ui/use-toast';
import { resolveAddCatReturn, safeBookingPath } from '@/lib/validation/bookingReturnPath';
import type { CatSex, DateOfBirthPrecision } from '@/lib/api/cats';

/** `RegisterCatRequest.OwnerId` is a required GUID on the wire, but for a customer caller the
 *  server replaces it with the caller's own account id (CatsController.RegisterCat, F19) — so the
 *  portal sends the empty GUID and never chooses an owner. */
const SERVER_FORCED_OWNER_ID = '00000000-0000-0000-0000-000000000000';

/**
 * Add a cat — HO-CAT-002-ADDCAT (backlog C-APT1), US-CAT-01 from the owner portal. `?next=` (a
 * `/book…` path, checked by `sanitizeNextPath`) is where the booking flow sent the customer from:
 * on success they return there with the new cat chosen; otherwise they land on the cat's page.
 */
function AddCatContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const registerCat = useRegisterCat();
  // `isPending` ends when the POST resolves, before `router.push` completes — a second click in
  // that gap would register a duplicate cat, so the form stays busy until the page is left.
  const [opening, setOpening] = useState(false);
  const submitted = useRef(false);
  const next = safeBookingPath(searchParams.get('next'));

  async function handleSubmit(values: CatFormValues) {
    if (submitted.current) return;
    submitted.current = true;
    let result: { catId: string };
    try {
      result = await registerCat.mutateAsync({
        ownerId: SERVER_FORCED_OWNER_ID,
        name: values.name,
        species: values.species || 'Cat',
        sex: values.sex as CatSex,
        dateOfBirth: values.dateOfBirthPrecision === 'Exact' ? values.dateOfBirth || null : null,
        dateOfBirthPrecision: values.dateOfBirthPrecision as DateOfBirthPrecision,
        breed: values.breed || null,
        colourAndMarkings: values.colourAndMarkings || null,
      });
    } catch (error) {
      submitted.current = false; // a failed save may be retried
      throw error;
    }
    setOpening(true);
    toast({ title: 'Cat added', description: `${values.name} is now on your account.` });
    router.push(resolveAddCatReturn(next, result.catId));
  }

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="font-display text-2xl font-semibold text-ink">Add a cat</h1>
        <p className="mt-1 text-sm text-ink-muted">
          Tell us about your cat. Only the name is needed &mdash; you can add the rest later.
        </p>
      </div>
      <CatForm
        mode="register"
        audience="customer"
        submitLabel="Add cat"
        cancelHref={next || '/portal/cats'}
        onSubmit={handleSubmit}
        isSubmitting={registerCat.isPending || opening}
      />
    </div>
  );
}

export default function AddCatPage() {
  return (
    <Suspense>
      <AddCatContent />
    </Suspense>
  );
}
