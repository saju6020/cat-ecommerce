'use client';

import { AuthCard } from '@/components/auth/AuthCard';
import { ForgotPasswordForm } from '@/components/auth/ForgotPasswordForm';
import { useRequestCustomerPasswordReset } from '@/lib/hooks/useAuthMutations';

// US-ACC-05 (customer). Customer-facing — warm layer.
// ARCH gate HIGH 3 (first pass) — this page wires useRequestCustomerPasswordReset();
// ForgotPasswordForm itself takes the result as props (HO-CAT-002 §8.2 rule 4).
export default function ForgotPasswordPage() {
  const mutation = useRequestCustomerPasswordReset();

  return (
    <div data-theme="customer" className="w-full">
      <AuthCard title="Reset your password">
        <ForgotPasswordForm
          audience="customer"
          onRequestReset={(identifier) => mutation.mutateAsync({ identifier })}
          isPending={mutation.isPending}
          error={mutation.error}
        />
      </AuthCard>
    </div>
  );
}
