'use client';

import { useState } from 'react';
import Link from 'next/link';
import { ArrowLeft, Mail } from 'lucide-react';
import { AuthCard } from '@/components/auth/AuthCard';
import { ForgotPasswordForm } from '@/components/auth/ForgotPasswordForm';
import { HearthAuthShell } from '@/components/hearth/HearthAuthShell';
import { buttonVariants } from '@/components/ui/button';
import { useRequestCustomerPasswordReset } from '@/lib/hooks/useAuthMutations';

// Hearth "Welcome home" forgot password (owner-approved 2026-10-16, docs/design/accounts-identity/ForgotPassword.dc.html):
// the same frame as sign-in; after sending, "Check your messages" with the one message every identifier gets, so a
// stranger can never learn whether an account exists.
export default function ForgotPasswordPage() {
  const mutation = useRequestCustomerPasswordReset();
  const [sent, setSent] = useState(false);

  if (sent) {
    return (
      <HearthAuthShell
        panelLabel="What happens next"
        eyebrow="On its way"
        headline="Almost"
        highlight="there"
        intro="Open the link on this device, then choose a new password."
        art="envelope"
      >
        <div className="flex w-full max-w-[420px] flex-col gap-5">
          <span
            aria-hidden="true"
            className="grid size-[72px] place-items-center rounded-pill bg-accent-tint text-accent"
          >
            <Mail size={32} />
          </span>
          <AuthCard
            bare
            title="Check your messages"
            titleClassName="text-[30px] md:text-[40px]"
            className="gap-3"
          >
            <p
              role="status"
              aria-live="polite"
              className="text-base leading-relaxed text-ink-muted"
            >
              If an account matches, we&rsquo;ve sent a link to reset the password. It expires in 15
              minutes.
            </p>
          </AuthCard>
          <div className="flex flex-col gap-1.5 rounded-card border border-border bg-surface px-[18px] py-4 text-sm text-ink-muted">
            <strong className="text-ink">Nothing arrived?</strong>
            <span>
              Check the number or email you typed, and your spam folder. You can ask for a new link
              in a few minutes.
            </span>
          </div>
          <Link
            href="/sign-in"
            className={buttonVariants({
              variant: 'pillSecondary',
              size: 'pillMd',
              className: 'w-full font-bold no-underline',
            })}
          >
            Back to sign in
          </Link>
        </div>
      </HearthAuthShell>
    );
  }

  return (
    <HearthAuthShell
      panelLabel="How it works"
      eyebrow="No worries"
      headline="It happens to"
      highlight="everyone"
      intro="We’ll send a link to the mobile number or email on your account, and you can choose a new password."
      steps={[
        'Tell us your mobile number or email',
        'Open the link we send you',
        'Choose a new password',
      ]}
      art="key"
    >
      <div className="flex w-full max-w-[420px] flex-col gap-5">
        <Link
          href="/sign-in"
          className="inline-flex items-center gap-1.5 self-start text-sm font-semibold text-accent-ink no-underline hover:underline"
        >
          <ArrowLeft size={14} aria-hidden="true" />
          Back to sign in
        </Link>
        <AuthCard
          bare
          title="Reset your password"
          titleClassName="text-[30px] md:text-[40px]"
          description="Enter the mobile number or email on your account and we’ll send a link to reset your password."
          className="gap-5"
        >
          <ForgotPasswordForm
            audience="customer"
            appearance="hearth"
            onRequestReset={(identifier) => mutation.mutateAsync({ identifier })}
            isPending={mutation.isPending}
            error={mutation.error}
            onSent={() => setSent(true)}
          />
        </AuthCard>
      </div>
    </HearthAuthShell>
  );
}
