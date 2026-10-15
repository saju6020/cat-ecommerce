'use client';

import type { ReactNode } from 'react';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { EmptyState } from '@/components/common/EmptyState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';

/**
 * `components/boarding/shared/QueryGate.tsx` — HO-BRD-002 §11 ("distinct loading / empty / error /
 * permission-denied states"). Wraps a TanStack query result: a skeleton while loading, the shared
 * `PermissionDeniedState` on a `403` (ADR-FE-107), a plain "we can't find that" on a `404` (someone
 * else's stay is the server's `404` — nothing is inferred, §19 gate 2), and the shared `ErrorState`
 * with **Try again** for anything else. The error text is ours, never the server's (NFR-USA-4).
 */
export interface GateQuery<T> {
  data: T | undefined;
  isPending: boolean;
  isError: boolean;
  error: unknown;
  refetch: () => unknown;
}

export function QueryGate<T>({
  query,
  loadingLabel = 'Loading…',
  loadingRows = 4,
  notFoundTitle = 'We can’t find that.',
  notFoundDescription = 'It may have been removed, or it may belong to someone else.',
  errorTitle,
  children,
}: {
  query: GateQuery<T>;
  loadingLabel?: string;
  loadingRows?: number;
  notFoundTitle?: string;
  notFoundDescription?: string;
  errorTitle?: string;
  children: (data: T) => ReactNode;
}) {
  if (query.isPending)
    return <LoadingState variant="card" rows={loadingRows} label={loadingLabel} />;
  if (query.isError || query.data === undefined) {
    if (query.error instanceof ApiForbiddenError) return <PermissionDeniedState />;
    if (query.error instanceof ApiNotFoundError) {
      return <EmptyState title={notFoundTitle} description={notFoundDescription} />;
    }
    return <ErrorState title={errorTitle} onRetry={() => void query.refetch()} />;
  }
  return <>{children(query.data)}</>;
}
