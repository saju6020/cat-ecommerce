'use client';

import type { ReactNode } from 'react';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';

/** The slice of a TanStack Query result this boundary reads (the Billing hooks type their error as `unknown`). */
export interface QueryLike<T> {
  data: T | undefined;
  isLoading: boolean;
  isError: boolean;
  error: unknown;
  refetch: () => unknown;
}

/**
 * `components/billing/QueryBoundary.tsx` — every Billing read screen has the same four states
 * (HO-BIL-002 §11 preamble): **loading**, **error** (with retry), **not found** (a `404`, including
 * another customer's document on the portal) and **permission denied** (a `403`, or a branch refusal
 * with its own wording). Cached data is shown through a refetch error rather than swapped for an
 * error screen (the HO-ACC-002 precedent).
 */
export function QueryBoundary<T>({
  query,
  children,
  notFoundTitle = 'Not found',
  notFoundDescription = 'This record may have been removed, or it may not be yours to see.',
  loadingLabel = 'Loading…',
  errorTitle,
  loadingVariant = 'card',
  loadingRows = 5,
}: {
  query: QueryLike<T>;
  children: (data: T) => ReactNode;
  notFoundTitle?: string;
  notFoundDescription?: string;
  loadingLabel?: string;
  errorTitle?: string;
  loadingVariant?: 'card' | 'table';
  loadingRows?: number;
}) {
  const { data, isLoading, isError, error, refetch } = query;
  if (data === undefined) {
    if (isLoading) {
      return <LoadingState variant={loadingVariant} rows={loadingRows} label={loadingLabel} />;
    }
    if (error instanceof ApiForbiddenError) return <AccessRefusedState error={error} />;
    if (error instanceof ApiNotFoundError) {
      return (
        <ErrorState title={notFoundTitle} description={notFoundDescription} headingLevel={2} />
      );
    }
    if (isError) return <ErrorState title={errorTitle} onRetry={() => void refetch()} />;
    return null;
  }
  return <>{children(data)}</>;
}
