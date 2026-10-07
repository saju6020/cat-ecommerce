'use client';

import { Suspense } from 'react';
import Link from 'next/link';
import { useRouter, usePathname, useSearchParams } from 'next/navigation';
import { buttonVariants } from '@/components/ui/button';
import { StaffAccountTable } from '@/components/admin/StaffAccountTable';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { useStaffAccounts, useStaffRoleCatalogue } from '@/lib/hooks/useStaffAdmin';
import { ApiForbiddenError } from '@/lib/api/client';

const PAGE_SIZE = 20;

// US-ACC-07. Navigation to admin screens may be hidden for non-administrators as presentation
// (nav/areas/accounts.ts, audience: 'staff' — not role-gated there); direct URL access still
// renders `PermissionDeniedState` from the server's `403` (ADR-FE-009).
export default function StaffAccountsPage() {
  return (
    <Suspense fallback={null}>
      <StaffAccountsPageContent />
    </Suspense>
  );
}

function StaffAccountsPageContent() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const search = searchParams.get('q') ?? undefined;
  const roleId = searchParams.get('roleId') ?? '';
  const pageNumber = Number(searchParams.get('page') ?? '1');

  const catalogue = useStaffRoleCatalogue();
  const accounts = useStaffAccounts({
    search,
    roleId: roleId || undefined,
    pageNumber,
    pageSize: PAGE_SIZE,
  });

  if (accounts.error instanceof ApiForbiddenError) {
    return <PermissionDeniedState />;
  }

  function handleRoleFilterChange(nextRoleId: string) {
    const params = new URLSearchParams(searchParams.toString());
    if (nextRoleId) params.set('roleId', nextRoleId);
    else params.delete('roleId');
    params.delete('page');
    router.replace(params.toString() ? `${pathname}?${params.toString()}` : pathname);
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="font-display text-xl font-semibold text-ink">Staff accounts</h1>
        <Link
          href="/staff/admin/staff-accounts/new"
          className={buttonVariants({ variant: 'primary' })}
        >
          New staff account
        </Link>
      </div>

      <StaffAccountTable
        rows={accounts.data?.items}
        totalCount={accounts.data?.totalCount}
        pageSize={PAGE_SIZE}
        isLoading={accounts.isLoading}
        isError={accounts.isError}
        onRetry={() => accounts.refetch()}
        roleIds={catalogue.data?.roleIds ?? []}
        roleFilter={roleId}
        onRoleFilterChange={handleRoleFilterChange}
      />
    </div>
  );
}
