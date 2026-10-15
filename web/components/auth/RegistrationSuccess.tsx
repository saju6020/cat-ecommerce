'use client';

import { useEffect, useRef } from 'react';
import Link from 'next/link';
import { buttonVariants } from '@/components/ui/button';

/**
 * `components/auth/RegistrationSuccess.tsx` — US-ACC-01 §11.4. No mention of verification, codes
 * or checking email (§4.1 — withdrawn scope); does not echo the password or the full mobile
 * number. The heading receives focus (§8.6).
 */
export function RegistrationSuccess({ signInHref = '/sign-in' }: { signInHref?: string }) {
  const headingRef = useRef<HTMLHeadingElement>(null);

  useEffect(() => {
    headingRef.current?.focus();
  }, []);

  return (
    <div className="flex flex-col gap-4 text-center">
      <h1
        ref={headingRef}
        tabIndex={-1}
        className="font-display text-2xl font-semibold text-ink focus:outline-none"
      >
        Your account is ready
      </h1>
      <p className="text-sm text-ink-muted">
        You can use it right away — sign in with the mobile number or email you just gave, and your
        password.
      </p>
      <Link href={signInHref} className={buttonVariants({ variant: 'primary' })}>
        Sign in
      </Link>
    </div>
  );
}
