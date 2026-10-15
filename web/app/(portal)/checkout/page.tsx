'use client';

import { Suspense } from 'react';
import { CheckoutScreen } from '@/components/checkout/CheckoutScreen';
import { LoadingState } from '@/components/common/LoadingState';

/**
 * `/checkout` — HO-ORD-002 §8.1, ADR-FE-090/098. One route with a stepper; every choice lives in the
 * URL. It sits in `(portal)`, so a signed-out visitor is sent to sign-in with `?next=/checkout`.
 */
export default function CheckoutPage() {
  return (
    <Suspense fallback={<LoadingState rows={5} label="Loading your checkout…" />}>
      <CheckoutScreen />
    </Suspense>
  );
}
