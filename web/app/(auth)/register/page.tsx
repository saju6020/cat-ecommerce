'use client';

import { Suspense, useState } from 'react';
import { useSearchParams } from 'next/navigation';
import { AuthCard } from '@/components/auth/AuthCard';
import { RegisterForm } from '@/components/auth/RegisterForm';
import { RegistrationSuccess } from '@/components/auth/RegistrationSuccess';
import { useCaptcha } from '@/lib/hooks/useCaptcha';
import { useRegisterCustomer } from '@/lib/hooks/useAuthMutations';
import { ApiRateLimitedError } from '@/lib/api/client';
import { withCustomerNext } from '@/lib/routing/portal-links';

// US-ACC-01. Customer-facing — warm layer.
// ARCH gate HIGH 3 (first pass) — this page wires useCaptcha()/useRegisterCustomer(); RegisterForm
// itself takes their results as props (HO-CAT-002 §8.2 rule 4, its own comment).
export default function RegisterPage() {
  return (
    <Suspense fallback={null}>
      <RegisterPageContent />
    </Suspense>
  );
}

function RegisterPageContent() {
  // Issues.md #5 — a visitor sent from Checkout keeps `?next=` through register and sign-in.
  const signInHref = withCustomerNext('/sign-in', useSearchParams().get('next'));
  const [success, setSuccess] = useState(false);
  const captcha = useCaptcha();
  const registerMutation = useRegisterCustomer();

  return (
    <div data-theme="customer" className="w-full">
      {success ? (
        <div className="mx-auto flex w-full max-w-md flex-col gap-6 rounded-[var(--radius-md)] border border-border bg-surface p-8 shadow-modal">
          <RegistrationSuccess signInHref={signInHref} />
        </div>
      ) : (
        <AuthCard title="Create your account">
          <RegisterForm
            captcha={captcha}
            onRegister={(request) => registerMutation.mutateAsync(request)}
            isPending={registerMutation.isPending}
            rateLimitError={
              registerMutation.error instanceof ApiRateLimitedError ? registerMutation.error : null
            }
            onSuccess={() => setSuccess(true)}
            signInHref={signInHref}
          />
        </AuthCard>
      )}
    </div>
  );
}
