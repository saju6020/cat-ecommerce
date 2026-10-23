'use client';

import { useEffect, useRef } from 'react';
import { cn } from '@/lib/utils';

/**
 * `components/auth/AuthCard.tsx` — HO-ACC-002 §11.3. The `(auth)` card shell: register, sign-in,
 * forgot-password, reset-password and account-deleted all render inside one of these. The heading
 * receives focus on arrival (§8.6 — "focus to the success heading on success" generalises here to
 * "every `(auth)` screen announces its own heading first", since these pages have no persistent
 * nav landmark a screen-reader user would otherwise land on).
 */
export interface AuthCardProps {
  /** The page's own `<h1>` text — also becomes the initial focus target. */
  title: string;
  description?: React.ReactNode;
  children: React.ReactNode;
  className?: string;
  /** No border/shadow/padding: the Hearth sign-in sets its form straight on the page (2026-10-16). */
  bare?: boolean;
  titleClassName?: string;
}

export function AuthCard({
  title,
  description,
  children,
  className,
  bare,
  titleClassName,
}: AuthCardProps) {
  const headingRef = useRef<HTMLHeadingElement>(null);

  useEffect(() => {
    headingRef.current?.focus();
  }, []);

  return (
    <div
      className={cn(
        bare
          ? 'mx-auto flex w-full max-w-md flex-col gap-6'
          : 'mx-auto flex w-full max-w-md flex-col gap-6 rounded-[var(--radius-md)] border border-border bg-surface p-8 shadow-modal',
        className,
      )}
    >
      <div className="flex flex-col gap-1.5">
        <h1
          ref={headingRef}
          tabIndex={-1}
          className={cn(
            'font-display text-2xl font-semibold text-ink focus:outline-none focus-visible:outline-none',
            titleClassName,
          )}
        >
          {title}
        </h1>
        {description && (
          <p className={cn('text-ink-muted', bare ? 'text-[15px]' : 'text-sm')}>{description}</p>
        )}
      </div>
      {children}
    </div>
  );
}
