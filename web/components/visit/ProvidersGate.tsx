'use client';

import { useContext, type ReactNode } from 'react';
import { QueryClientContext } from '@tanstack/react-query';
import { AppRouterContext } from 'next/dist/shared/lib/app-router-context.shared-runtime';

/**
 * `ProvidersGate` — HO-EMR-002 §13. The two additions to HO-APT-002's presentational `ClinicQueue` and
 * `StaffAppointmentDetail` (queue-card actions, appointment links) read the server through hooks,
 * but those two components are also rendered bare — no `QueryClientProvider`, no App Router — by
 * HO-APT-002's own component tests, which this handover must not edit. This renders its children
 * ONLY when the providers they need are mounted (always, in the real app), so the clinical additions
 * are invisible exactly where there is nothing for them to talk to, and everywhere else behave
 * normally. It adds no behaviour of its own.
 */
export function ProvidersGate({
  children,
  needsRouter = false,
}: {
  children: ReactNode;
  needsRouter?: boolean;
}) {
  const queryClient = useContext(QueryClientContext);
  const router = useContext(AppRouterContext);
  if (!queryClient || (needsRouter && !router)) return null;
  return <>{children}</>;
}
