'use client';

import { LimitedMarkup } from '@/components/content/LimitedMarkup';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { ApiNotFoundError } from '@/lib/api/client';
import { usePublishedContent } from '@/lib/hooks/usePublicContent';
import { BOARDING_PAYMENT_INSTRUCTIONS_SLUG } from '@/lib/routing/boarding-links';

/**
 * `components/boarding/booking/HowToPay.tsx` — HO-BRD-002 §11.1 step 7, US-BRD-06 AC1. The
 * `boarding-payment-instructions` content page (HO-ADM-001 §8.8, HO-BRD-001 OQ-9) rendered with the
 * same `LimitedMarkup` the public page uses — no HTML path. It **describes** how to pay offline
 * (cash or card at the front desk, bank transfer); there is no control here that pays anything
 * (ADR-FE-106).
 */
export function HowToPay({ slug = BOARDING_PAYMENT_INSTRUCTIONS_SLUG }: { slug?: string }) {
  const page = usePublishedContent('Page', slug);

  if (page.isPending) return <LoadingState rows={4} label="Loading how to pay…" />;
  if (page.error instanceof ApiNotFoundError) {
    return (
      <p className="text-sm text-ink-muted">
        Ask the front desk how to pay — we accept cash or card at the clinic, or a bank transfer.
      </p>
    );
  }
  if (page.isError || !page.data) {
    return <ErrorState title="We couldn’t load how to pay." onRetry={() => void page.refetch()} />;
  }
  return (
    <section aria-labelledby="how-to-pay-heading" className="flex flex-col gap-2">
      <h3 id="how-to-pay-heading" className="font-display text-base font-semibold text-ink">
        How to pay
      </h3>
      <LimitedMarkup body={page.data.body} className="text-sm text-ink" />
    </section>
  );
}
