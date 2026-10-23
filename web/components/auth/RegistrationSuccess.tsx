'use client';

import { useEffect, useRef } from 'react';
import Link from 'next/link';
import { ArrowRight, Check } from 'lucide-react';
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

  // Hearth "account ready" (owner-approved 2026-10-16): tick disc, big heading, the Sign in pill, one line on what's next.
  return (
    <div className="flex w-full max-w-[420px] flex-col gap-5">
      <span
        aria-hidden="true"
        className="grid size-[72px] place-items-center rounded-pill bg-accent-tint text-accent"
      >
        <Check size={34} strokeWidth={2.6} />
      </span>
      <h1
        ref={headingRef}
        tabIndex={-1}
        className="font-display text-[30px] font-semibold leading-tight text-ink focus:outline-none focus-visible:outline-none md:text-[40px]"
      >
        Your account is ready
      </h1>
      <p className="text-base leading-relaxed text-ink-muted">
        You can use it right away — sign in with the mobile number or email you just gave, and your
        password.
      </p>
      <Link
        href={signInHref}
        className={buttonVariants({
          variant: 'pill',
          size: 'pillLg',
          className: 'w-full gap-2.5 no-underline',
        })}
      >
        Sign in
        <ArrowRight size={16} aria-hidden="true" />
      </Link>
      <p className="text-[13px] text-ink-muted">
        After signing in you can add your cats, book a visit or shop for supplies.
      </p>
    </div>
  );
}
