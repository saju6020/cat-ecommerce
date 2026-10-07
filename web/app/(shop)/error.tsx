'use client';

import { ErrorState } from '@/components/common/ErrorState';

/** Storefront error boundary: a plain-language message and a retry (NFR-USA-4) — never a stack. */
export default function ShopError({ reset }: { error: Error; reset: () => void }) {
  return (
    <ErrorState
      title="We couldn't load the shop just now."
      description="This does not mean the product is gone — please try again."
      onRetry={reset}
    />
  );
}
