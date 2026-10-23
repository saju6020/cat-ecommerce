import Link from 'next/link';
import { Wrench } from 'lucide-react';
import { buttonVariants } from '@/components/ui/button';

/**
 * HO-SHP-002 §11.1 `MaintenanceNotice` — US-ADM-08. Replaces the storefront page; the admin's
 * message is shown as TEXT, and the two areas that still work are linked.
 */
export function MaintenanceNotice({ message }: { message?: string | null }) {
  return (
    <section
      aria-labelledby="maintenance-heading"
      className="mx-auto flex max-w-xl flex-col items-start gap-4 rounded-band border border-border bg-surface p-8 md:p-12"
    >
      <span
        aria-hidden="true"
        className="flex h-12 w-12 items-center justify-center rounded-full bg-surface-sunken text-ink-muted"
      >
        <Wrench size={22} />
      </span>
      <h1 id="maintenance-heading" className="font-display text-2xl font-semibold text-ink">
        We’ll be back soon
      </h1>
      {message && <p className="whitespace-pre-line text-base text-ink">{message}</p>}
      <p className="text-sm text-ink-muted">
        The shop is temporarily closed. Clinic bookings and your account are still available.
      </p>
      <div className="flex flex-wrap gap-3">
        <Link href="/book" className={buttonVariants({ variant: 'pill', size: 'pillMd' })}>
          Book a clinic visit
        </Link>
        <Link
          href="/portal"
          className={buttonVariants({ variant: 'pillSecondary', size: 'pillMd' })}
        >
          Go to your account
        </Link>
      </div>
    </section>
  );
}
