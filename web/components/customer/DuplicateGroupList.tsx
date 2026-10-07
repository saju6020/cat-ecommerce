'use client';

import Link from 'next/link';
import { Skull } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { buttonVariants } from '@/components/ui/button';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import { EmptyState } from '@/components/common/EmptyState';
import { NotYetRecordedNotice } from './NotYetRecordedNotice';
import { CustomerStatusBadge } from './CustomerStatusBadge';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { ApiForbiddenError } from '@/lib/api/client';
import type { PagedResult } from '@/lib/api/types';
import type { DuplicateCustomerGroup } from '@/lib/api/staff-customers';

export const DUPLICATE_GROUPS_PAGE_SIZE = 20;

/**
 * `components/customer/DuplicateGroupList.tsx` — US-ACC-11, HO-ACC-002 §11.12.
 *
 * CR MEDIUM 2 (first-pass gate fix round, item 13): `recordStatus`/`registrationChannel` are now
 * shown per member — HO-ACC-001's `DuplicateCustomerMemberViewModel` used to carry neither, unlike
 * §11.12's prose, a §20.7 contract gap that round closed rather than continuing to work around here.
 *
 * ARCH gate HIGH 3 (first pass) — takes `data`/`isLoading`/`isError`/`error`/`onRetry`/
 * `pageNumber`/`onPageChange` as props; the page
 * (`app/(staff)/staff/customers/duplicates/page.tsx`) wires `useDuplicateCustomerGroups()` and
 * owns the `?page=` URL param this component used to read/write itself.
 */
export interface DuplicateGroupListProps {
  data: PagedResult<DuplicateCustomerGroup> | undefined;
  isLoading: boolean;
  isError: boolean;
  error: unknown;
  onRetry: () => void;
  pageNumber: number;
  onPageChange: (pageNumber: number) => void;
}

export function DuplicateGroupList({
  data,
  isLoading,
  isError,
  error,
  onRetry,
  pageNumber,
  onPageChange,
}: DuplicateGroupListProps) {
  if (isLoading)
    return <LoadingState variant="card" rows={6} label="Loading duplicate customers…" />;
  if (error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  if (isError || !data) {
    return (
      <ErrorState description="We couldn't load duplicate customers just now." onRetry={onRetry} />
    );
  }
  if (data.items.length === 0) {
    return <EmptyState title="No duplicate customers found" />;
  }

  const pageCount = Math.max(1, Math.ceil(data.totalCount / DUPLICATE_GROUPS_PAGE_SIZE));

  return (
    <div className="flex flex-col gap-6">
      {data.items.map((group, index) => {
        const ids = group.members.map((m) => m.customerId).join(',');
        return (
          <div
            key={ids || index}
            className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-5"
          >
            <ul className="flex flex-col gap-3">
              {group.members.map((member) => (
                <li
                  key={member.customerId}
                  className="flex flex-col gap-1 border-b border-border pb-2 last:border-0 last:pb-0"
                >
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-semibold text-ink">{member.fullName}</span>
                    <CustomerStatusBadge recordStatus={member.recordStatus} />
                    {member.mobileMatchedOn === 'Additional' && (
                      <Badge variant="warning">Additional number only</Badge>
                    )}
                  </div>
                  <span className="text-sm text-ink-muted">
                    {member.mobileNumber ?? '—'} · {member.emailAddress ?? '—'} ·{' '}
                    {member.registrationChannel === 'Reception' ? 'Walk-in' : 'Self-service'}
                  </span>
                  {member.cats.length > 0 && (
                    <ul className="flex flex-wrap gap-2 text-xs text-ink-muted">
                      {member.cats.map((cat) => (
                        <li key={cat.catId} className="flex items-center gap-1">
                          {cat.name}
                          {cat.isDeceased && (
                            <Badge variant="neutral" icon={<Skull size={10} aria-hidden="true" />}>
                              Deceased
                            </Badge>
                          )}
                        </li>
                      ))}
                    </ul>
                  )}
                </li>
              ))}
            </ul>
            <NotYetRecordedNotice />
            <Link
              href={`/staff/customers/duplicates/review?ids=${encodeURIComponent(ids)}`}
              className={buttonVariants({ variant: 'primary', size: 'sm' })}
            >
              Review merge
            </Link>
          </div>
        );
      })}

      <nav aria-label="Pages" className="flex items-center justify-between text-sm text-ink-muted">
        <span>
          Page {pageNumber} of {pageCount}
        </span>
        <div className="flex gap-2">
          <button
            type="button"
            disabled={pageNumber <= 1}
            onClick={() => onPageChange(pageNumber - 1)}
            className="rounded-[var(--radius-sm)] border border-border-strong px-3 py-1.5 disabled:opacity-50"
          >
            Previous
          </button>
          <button
            type="button"
            disabled={pageNumber >= pageCount}
            onClick={() => onPageChange(pageNumber + 1)}
            className="rounded-[var(--radius-sm)] border border-border-strong px-3 py-1.5 disabled:opacity-50"
          >
            Next
          </button>
        </div>
      </nav>
    </div>
  );
}
