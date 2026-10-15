'use client';

import * as React from 'react';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { EmptyState } from '@/components/common/EmptyState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';

/**
 * `components/inventory/QueryBoundary.tsx` — HO-IMP-002 §8.5. The one place every Inventory and
 * Procurement screen decides what to show while its data is not there: loading, a server `403`
 * (`AccessRefusedState` → `PermissionDeniedState`, or the "pick a branch" banner), a `404`, any
 * other failure (never "empty" — an error is not an empty list), and — only once data arrived —
 * its content. The 403 comes from the server; the client decides nothing about permissions.
 */
export interface QueryLike<T> {
  data: T | undefined;
  isLoading: boolean;
  isError: boolean;
  error: unknown;
  refetch: () => unknown;
}

export function QueryBoundary<T>({
  query,
  children,
  loadingRows = 4,
  loadingLabel,
  notFoundTitle = 'This record could not be found',
  notFoundDescription = 'It may belong to another branch, or it may have been removed.',
  errorTitle,
}: {
  query: QueryLike<T>;
  children: (data: T) => React.ReactNode;
  loadingRows?: number;
  loadingLabel?: string;
  notFoundTitle?: string;
  notFoundDescription?: string;
  errorTitle?: string;
}) {
  if (query.error instanceof ApiForbiddenError) return <AccessRefusedState error={query.error} />;
  if (query.error instanceof ApiNotFoundError) {
    return <EmptyState title={notFoundTitle} description={notFoundDescription} />;
  }
  if (query.isError) {
    return <ErrorState title={errorTitle} onRetry={() => void query.refetch()} />;
  }
  if (query.isLoading || query.data === undefined) {
    return <LoadingState rows={loadingRows} label={loadingLabel} />;
  }
  return <>{children(query.data)}</>;
}
