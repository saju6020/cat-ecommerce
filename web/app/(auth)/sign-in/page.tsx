'use client';

import { Suspense } from 'react';
import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { CalendarDays, Check, Truck } from 'lucide-react';
import { AuthCard } from '@/components/auth/AuthCard';
import { SignInForm } from '@/components/auth/SignInForm';
import { HearthAuthShell } from '@/components/hearth/HearthAuthShell';
import { buttonVariants } from '@/components/ui/button';
import { useSignInCustomer } from '@/lib/hooks/useAuthMutations';
import { sanitizeNextPath } from '@/lib/validation/nextPath';
import {
  CUSTOMER_RETURN_PREFIXES,
  DEFAULT_CUSTOMER_LANDING,
  withCustomerNext,
} from '@/lib/routing/portal-links';

const REASONS = [
  { text: 'Track shop orders to your door', tone: 'warm' as const, Icon: Truck },
  { text: 'Book a clinic visit in a few taps', tone: 'teal' as const, Icon: CalendarDays },
  { text: 'Every vaccination and visit in one place', tone: 'teal' as const, Icon: Check },
];

// US-ACC-04. `data-theme="customer"` — a customer-facing (auth) screen (see the handover's
// warm-customer-layer rule); staff sign-in (`(auth)/staff/sign-in`) does not set this.
// Hearth "Welcome home" (owner-approved 2026-10-16, docs/design/accounts-identity/SignIn.dc.html) via HearthAuthShell.
export default function SignInPage() {
  return (
    <HearthAuthShell
      panelLabel="Why sign in"
      eyebrow="Clinic · Boarding · Shop"
      headline="Welcome"
      highlight="home"
      intro="One account for your cats’ visits, vaccinations, boarding stays and shop orders."
      reasons={REASONS}
    >
      <AuthCard
        bare
        title="Sign in"
        titleClassName="text-[30px] md:text-[40px]"
        description="Use the mobile number or email you registered with."
        className="max-w-[420px] gap-5"
      >
        <Suspense fallback={null}>
          <SignInPageContent />
        </Suspense>
      </AuthCard>
    </HearthAuthShell>
  );
}

function SignInPageContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const queryClient = useQueryClient();
  const signInMutation = useSignInCustomer();

  return (
    <>
      <SignInForm
        audience="customer"
        appearance="hearth"
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
          // HO-ORD-002 ADR-FE-098: `/cart` and `/checkout` also survive sign-in (the cart's Checkout button sends a
          // signed-out visitor here with `?next=/checkout`; the guard stays closed for everything else).
          // HO-APT-002 amendment (2026-09-22, before implementation): `/book` also survives sign-in
          // so a booking link kept mid-flow lands correctly, alongside the default `/portal/**`.
          // HO-BRD-002 §1 Amended (owner, 2026-10-14): `/boarding` also survives sign-in, so an interrupted
          // boarding search comes back with its parameters.
          const destination = sanitizeNextPath(
            searchParams.get('next'),
            CUSTOMER_RETURN_PREFIXES,
            DEFAULT_CUSTOMER_LANDING,
          );
          router.push(destination);
        }}
      />
      {/* Issues.md #5 — a new customer sent here by Checkout can create an account (with the CAPTCHA) and come back. */}
      <div className="flex items-center gap-3 text-[13px] text-ink-muted">
        <span aria-hidden="true" className="h-px flex-1 bg-border" />
        New to PurrCare?
        <span aria-hidden="true" className="h-px flex-1 bg-border" />
      </div>
      <Link
        href={withCustomerNext('/register', searchParams.get('next'))}
        className={buttonVariants({
          variant: 'pillSecondary',
          size: 'pillMd',
          className: 'w-full font-bold no-underline',
        })}
      >
        Create an account
      </Link>
    </>
  );
}
