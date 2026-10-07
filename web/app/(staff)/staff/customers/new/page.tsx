'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { WalkInCustomerForm } from '@/components/customer/WalkInCustomerForm';
import { useRegisterWalkInCustomer } from '@/lib/hooks/useStaffCustomerMutations';
import { useFindCustomers } from '@/lib/hooks/useStaffCustomers';

// US-ACC-10.
// ARCH gate HIGH 3 (first pass) — this page wires useRegisterWalkInCustomer()/useFindCustomers();
// WalkInCustomerForm itself takes the results as props, including which mobile number (if any) is
// currently a reported duplicate, which now lives here rather than inside the form.
export default function NewWalkInCustomerPage() {
  const router = useRouter();
  const [duplicateMobile, setDuplicateMobile] = useState<string | null>(null);
  const registerMutation = useRegisterWalkInCustomer();
  const lookup = useFindCustomers({ mobile: duplicateMobile ?? undefined }, !!duplicateMobile);

  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-xl font-semibold text-ink">Register a walk-in customer</h1>
      <WalkInCustomerForm
        onRegister={(request) => registerMutation.mutateAsync(request)}
        isPending={registerMutation.isPending}
        onCreated={(customerId) => router.push(`/staff/customers/${customerId}`)}
        duplicateMobile={duplicateMobile}
        onDuplicateFound={setDuplicateMobile}
        existingCustomers={lookup.data}
      />
    </div>
  );
}
