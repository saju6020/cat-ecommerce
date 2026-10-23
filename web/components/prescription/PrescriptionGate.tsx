import Link from 'next/link';
import { ShieldAlert } from 'lucide-react';
import { buttonVariants } from '@/components/ui/button';
import { formatBestBefore } from '@/lib/format/shop';
import type { SkuPurchasability } from '@/lib/api/shop-customer';

export const CONSULTATION_HREF = '/book?category=Consultation';

export interface PrescriptionGateProps {
  /** `guest` = no customer session; `customer` = signed in. */
  audience: 'guest' | 'customer';
  /** The selected SKU's purchasability (customers only); `undefined` while it loads. */
  purchasability?: SkuPurchasability;
  isLoading?: boolean;
  /** The purchasability read failed: say so neutrally (never claim "no authorisation"). */
  isError?: boolean;
  onRetry?: () => void;
  /** Where a guest is sent to sign in and come straight back (ADR-FE-038). */
  signInHref: string;
}

/**
 * HO-SHP-002 §11.3 `PrescriptionGate` — ADR-FE-033, US-SHP-10 AC1. The product stays visible to
 * everyone; this says why a customer cannot buy it yet and offers the way forward. It renders only
 * what the API decided (`purchaseBlockReason`, `eligibleCats`), and NOTHING clinical — no
 * diagnosis, no condition, no history — only each cat's name and the end date of its authorisation.
 *
 * Under the interim fail-closed rule (HO-SHP-001 §1 Amended 2026-10-04) every prescription product
 * reads as not purchasable until HO-PHM-001 supplies authorisations; the message stays honest.
 */
export function PrescriptionGate({
  audience,
  purchasability,
  isLoading,
  isError,
  onRetry,
  signInHref,
}: PrescriptionGateProps) {
  const eligible = purchasability?.eligibleCats ?? [];
  return (
    <section
      aria-labelledby="rx-gate-heading"
      className="flex flex-col gap-2 rounded-card border border-warning-ink/30 bg-warning-bg p-3 text-warning-ink"
    >
      <h2 id="rx-gate-heading" className="flex items-center gap-2 text-sm font-bold">
        <ShieldAlert size={16} aria-hidden="true" />
        Vet authorisation needed
      </h2>
      <p className="text-sm">
        This is a prescription diet. It can be bought only for a cat with a current authorisation
        from our vets.
      </p>
      {audience === 'guest' && (
        <Link href={signInHref} className="text-sm font-semibold underline">
          Sign in to check whether your cat is authorised
        </Link>
      )}
      {audience === 'customer' && isLoading && (
        <p role="status" className="text-sm">
          Checking your cats…
        </p>
      )}
      {audience === 'customer' && !isLoading && isError && (
        <div role="alert" className="flex flex-wrap items-center gap-2 text-sm font-semibold">
          <span>We couldn&apos;t check right now — try again.</span>
          {onRetry && (
            <button type="button" onClick={onRetry} className="underline">
              Try again
            </button>
          )}
        </div>
      )}
      {audience === 'customer' && !isLoading && !isError && eligible.length > 0 && (
        <ul className="m-0 list-none p-0 text-sm font-semibold">
          {eligible.map((cat) => (
            <li key={cat.catId}>
              Authorised for {cat.name} until {formatBestBefore(cat.validTo)}
            </li>
          ))}
        </ul>
      )}
      {audience === 'customer' &&
        !isLoading &&
        !isError &&
        eligible.length === 0 &&
        purchasability?.purchaseBlockReason !== 'NotOnSale' && (
          <>
            <p className="text-sm font-semibold">
              None of your cats has a current authorisation for this food.
            </p>
            <Link
              href={CONSULTATION_HREF}
              className={buttonVariants({ variant: 'primary', size: 'sm' }) + ' self-start'}
            >
              Book a consultation
            </Link>
          </>
        )}
    </section>
  );
}
