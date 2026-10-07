'use client';

import { use } from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useAdminBranch } from '@/lib/hooks/useAdminBranches';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import { cn } from '@/lib/utils';

const TABS = [
  { segment: '', label: 'Details' },
  { segment: 'hours', label: 'Opening hours' },
  { segment: 'closures', label: 'Closures' },
  { segment: 'rooms', label: 'Rooms' },
  { segment: 'roster', label: 'Roster' },
];

/** `/staff/admin/branches/[branchId]` — HO-ADM-002 §8.1. Tabs: Details · Opening hours ·
 *  Closures · Rooms · Roster. */
export default function BranchDetailLayout({
  children,
  params,
}: {
  children: React.ReactNode;
  params: Promise<{ branchId: string }>;
}) {
  const { branchId } = use(params);
  const { data: branch, isLoading, isError, error, refetch } = useAdminBranch(branchId);
  const pathname = usePathname();
  const basePath = `/staff/admin/branches/${branchId}`;

  if (error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  if (error instanceof ApiNotFoundError) {
    return <ErrorState title="Branch not found" description="This branch may have been removed." />;
  }
  if (isLoading) return <LoadingState rows={5} label="Loading branch…" />;
  if (isError && !branch) {
    return (
      <ErrorState description="We couldn't load this branch just now." onRetry={() => refetch()} />
    );
  }
  if (!branch) return null;

  return (
    <div className="flex flex-col gap-5">
      <div>
        <h1 className="font-display text-xl font-semibold text-ink">{branch.name}</h1>
        <p className="text-sm text-ink-muted">
          {branch.code} · {branch.timeZoneId}
        </p>
      </div>

      <nav
        aria-label="Branch sections"
        className="flex flex-wrap gap-1 border-b border-border pb-2"
      >
        {TABS.map((tab) => {
          const href = tab.segment ? `${basePath}/${tab.segment}` : basePath;
          const isActive = pathname === href;
          return (
            <Link
              key={tab.segment || 'details'}
              href={href}
              aria-current={isActive ? 'page' : undefined}
              className={cn(
                'rounded-[var(--radius-sm)] px-3 py-1.5 text-sm font-semibold no-underline',
                isActive
                  ? 'bg-accent-soft text-accent-ink'
                  : 'text-ink-muted hover:bg-surface-sunken',
              )}
            >
              {tab.label}
            </Link>
          );
        })}
      </nav>

      {children}
    </div>
  );
}
