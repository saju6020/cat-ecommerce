'use client';

import { Suspense } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { AuthCard } from '@/components/auth/AuthCard';
import { SignInForm } from '@/components/auth/SignInForm';
import { useSignInCustomer } from '@/lib/hooks/useAuthMutations';
import { sanitizeNextPath } from '@/lib/validation/nextPath';

// US-ACC-04. `data-theme="customer"` — a customer-facing (auth) screen (see the handover's
// warm-customer-layer rule); staff sign-in (`(auth)/staff/sign-in`) does not set this.
export default function SignInPage() {
  return (
    <div data-theme="customer" className="w-full">
      <AuthCard title="Sign in">
        <Suspense fallback={null}>
          <SignInPageContent />
        </Suspense>
      </AuthCard>
    </div>
  );
}

function SignInPageContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const queryClient = useQueryClient();
  const signInMutation = useSignInCustomer();

  return (
    <SignInForm
      audience="customer"
      onSignIn={(values) => signInMutation.mutateAsync(values)}
      isPending={signInMutation.isPending}
      error={signInMutation.error}
      onSuccess={() => {
        // SEC gate LOW (first pass) — the whole cache, not just the session key: if person B signs
        // in on a tab person A left signed in, B must never see A's still-cached data (admin-only
        // lists included) until it happens to go stale. §8.4's "invalidate before navigating, or
        // the guard sees a stale cached value and bounces back" is satisfied by `clear()` too — it
        // removes the session key outright, the same as sign-out/deletion/person-change already do.
        queryClient.clear();
        // HO-SHP-002 §13 (Amended 2026-10-04): `/shop` also survives sign-in (ADR-FE-038 — a guest who signs in
        // to save/alert/review returns to the same product and variant).
        // HO-APT-002 amendment (2026-09-22, before implementation): `/book` also survives sign-in
        // so a booking link kept mid-flow lands correctly, alongside the default `/portal/**`.
        const destination = sanitizeNextPath(
          searchParams.get('next'),
          ['/portal/', '/book', '/shop'],
          '/portal/cats',
        );
        router.push(destination);
      }}
    />
  );
}
