'use client';

import { Suspense, useRef, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { AuthCard } from '@/components/auth/AuthCard';
import { RegisterForm } from '@/components/auth/RegisterForm';
import { RegistrationSuccess } from '@/components/auth/RegistrationSuccess';
import { HearthAuthShell } from '@/components/hearth/HearthAuthShell';
import { Check, House, Smartphone } from 'lucide-react';
import { useCaptcha } from '@/lib/hooks/useCaptcha';
import { useRegisterCustomer, useSignInCustomer } from '@/lib/hooks/useAuthMutations';
import { ApiRateLimitedError } from '@/lib/api/client';
import type { RegisterCustomerRequest } from '@/lib/api/auth';
import {
  CUSTOMER_RETURN_PREFIXES,
  DEFAULT_CUSTOMER_LANDING,
  withCustomerNext,
} from '@/lib/routing/portal-links';
import { sanitizeNextPath } from '@/lib/validation/nextPath';

// US-ACC-01. Customer-facing — warm layer.
const REASONS = [
  { text: 'One account for clinic, boarding and shop', tone: 'warm' as const, Icon: House },
  { text: 'Sign in with your mobile number or email', tone: 'teal' as const, Icon: Smartphone },
  { text: 'Keep your cats, addresses and orders together', tone: 'teal' as const, Icon: Check },
];

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
  const next = useSearchParams().get('next');
  const signInHref = withCustomerNext('/sign-in', next);
  const [success, setSuccess] = useState(false);
  const captcha = useCaptcha();
  const registerMutation = useRegisterCustomer();
  const signInMutation = useSignInCustomer();
  const router = useRouter();
  const queryClient = useQueryClient();
  const signedIn = useRef(false);

  // Owner, 2026-10-19: a new customer is signed in straight away with what they just typed and sent on to where they
  // were going (Checkout's `?next=/checkout`), instead of being asked to sign in a second time. Sign-in needs no
  // CAPTCHA, and the password never leaves this function. If that sign-in fails for any reason, the account still
  // exists and the "Your account is ready" screen offers Sign in, as before.
  async function registerThenSignIn(request: RegisterCustomerRequest) {
    const created = await registerMutation.mutateAsync(request);
    const identifier = request.mobileNumber?.trim() || request.emailAddress?.trim();
    if (identifier) {
      try {
        await signInMutation.mutateAsync({ identifier, password: request.password });
        signedIn.current = true;
      } catch {
        signedIn.current = false;
      }
    }
    return created;
  }

  function afterRegister() {
    if (!signedIn.current) {
      setSuccess(true);
      return;
    }
    // As the sign-in page does: drop every cached query (the guest's empty session included) before navigating.
    queryClient.clear();
    router.push(sanitizeNextPath(next, CUSTOMER_RETURN_PREFIXES, DEFAULT_CUSTOMER_LANDING));
  }

  // Hearth "Welcome home" register (owner-approved 2026-10-16, docs/design/accounts-identity/Register.dc.html and
  // RegisterSuccess.dc.html): the same frame as sign-in; the panel changes to "You're in." once the account exists.
  if (success) {
    return (
      <HearthAuthShell
        panelLabel="Welcome"
        eyebrow="All set"
        headline="You’re"
        highlight="in"
        intro="Welcome to PurrCare. Your cats are in good hands."
      >
        <RegistrationSuccess signInHref={signInHref} />
      </HearthAuthShell>
    );
  }

  return (
    <HearthAuthShell
      panelLabel="Why join"
      eyebrow="New here"
      headline="Join the"
      highlight="family"
      intro="It takes a minute. Then you can add your cats, book a visit and shop for supplies."
      reasons={REASONS}
    >
      <AuthCard
        bare
        title="Create your account"
        titleClassName="text-[30px] md:text-[40px]"
        description="Give a mobile number, an email, or both — you’ll use one to sign in."
        className="max-w-[460px] gap-5"
      >
        <RegisterForm
          captcha={captcha}
          onRegister={registerThenSignIn}
          isPending={registerMutation.isPending || signInMutation.isPending}
          rateLimitError={
            registerMutation.error instanceof ApiRateLimitedError ? registerMutation.error : null
          }
          onSuccess={afterRegister}
          signInHref={signInHref}
        />
      </AuthCard>
    </HearthAuthShell>
  );
}
