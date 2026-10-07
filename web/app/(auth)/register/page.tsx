'use client';

import { useState } from 'react';
import { AuthCard } from '@/components/auth/AuthCard';
import { RegisterForm } from '@/components/auth/RegisterForm';
import { RegistrationSuccess } from '@/components/auth/RegistrationSuccess';
import { useCaptcha } from '@/lib/hooks/useCaptcha';
import { useRegisterCustomer } from '@/lib/hooks/useAuthMutations';
import { ApiRateLimitedError } from '@/lib/api/client';

// US-ACC-01. Customer-facing — warm layer.
// ARCH gate HIGH 3 (first pass) — this page wires useCaptcha()/useRegisterCustomer(); RegisterForm
// itself takes their results as props (HO-CAT-002 §8.2 rule 4, its own comment).
export default function RegisterPage() {
  const [success, setSuccess] = useState(false);
  const captcha = useCaptcha();
  const registerMutation = useRegisterCustomer();

  return (
    <div data-theme="customer" className="w-full">
      {success ? (
        <div className="mx-auto flex w-full max-w-md flex-col gap-6 rounded-[var(--radius-md)] border border-border bg-surface p-8 shadow-modal">
          <RegistrationSuccess />
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
          />
        </AuthCard>
      )}
    </div>
  );
}
