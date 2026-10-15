'use client';

import { Suspense } from 'react';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { Tabs } from '@/components/billing/Tabs';
import { BalancesTable, IntegrityPanel } from '@/components/controlled-drugs/BalancesAndIntegrity';
import { RegisterTable } from '@/components/controlled-drugs/RegisterTable';
import { RegisterToolbar } from '@/components/controlled-drugs/RegisterToolbar';
import { PageHeading } from '@/components/inventory/fields';
import { ApiForbiddenError } from '@/lib/api/client';
import type { RegisterFilters } from '@/lib/api/controlled-drugs';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import {
  useRegister,
  useRegisterBalances,
  useRegisterVerification,
} from '@/lib/hooks/useControlledDrugs';
import { useFormularyList } from '@/lib/hooks/useFormulary';

const PAGE_SIZE = 25;
type TabId = 'entries' | 'balances' | 'integrity';

/**
 * `/staff/pharmacy/controlled-drugs` — FR-PHM-7: the register, balances and integrity. Filters, tab and
 * page are URL parameters (a filtered register is linkable). Read-only: no screen here changes a
 * register entry (ADR-FE-065).
 */
function RegisterPage() {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const zone = useBranchTimeZone();
  const tab = (
    ['entries', 'balances', 'integrity'].includes(params.get('tab') ?? '')
      ? params.get('tab')
      : 'entries'
  ) as TabId;
  const page = Math.max(1, Number(params.get('page')) || 1);
  const filters: RegisterFilters = {
    medicineId: params.get('medicineId') ?? undefined,
    batchId: params.get('batchId') ?? undefined,
    kind: params.get('kind') ?? undefined,
    from: params.get('from') ?? undefined,
    to: params.get('to') ?? undefined,
  };
  const register = useRegister({ ...filters, pageNumber: page, pageSize: PAGE_SIZE });
  const balances = useRegisterBalances(true);
  const verification = useRegisterVerification(tab === 'integrity');
  const medicines = useFormularyList({ isControlled: true, pageSize: 100 });

  function update(next: Record<string, string | undefined>) {
    const search = new URLSearchParams(params.toString());
    for (const [key, value] of Object.entries(next)) {
      if (value) search.set(key, value);
      else search.delete(key);
    }
    if (!('page' in next)) search.delete('page');
    const query = search.toString();
    router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false });
  }

  if (register.error instanceof ApiForbiddenError)
    return <AccessRefusedState error={register.error} />;
  const lots = (balances.data ?? [])
    .filter((row) => row.batchId && row.lotNumber)
    .map((row) => ({
      batchId: row.batchId as string,
      label: `${row.medicineName} — lot ${row.lotNumber}`,
    }));

  return (
    <div className="flex flex-col gap-4">
      <PageHeading
        title="Controlled-drug register"
        description="Every movement of a controlled drug, with its running balance. Entries cannot be changed; a mistake is corrected by a new, linked entry."
      />
      <Tabs
        label="Register views"
        idPrefix="cd"
        selected={tab}
        onSelect={(id) => update({ tab: id === 'entries' ? undefined : id })}
        tabs={[
          {
            id: 'entries',
            label: 'Entries',
            panel: (
              <div className="flex flex-col gap-4">
                <RegisterToolbar
                  filters={filters}
                  medicines={(medicines.data?.items ?? []).map((m) => ({ id: m.id, name: m.name }))}
                  lots={lots}
                  onChange={(next) => update(next as Record<string, string | undefined>)}
                />
                <RegisterTable
                  rows={register.data?.items}
                  zone={zone}
                  isLoading={register.isLoading}
                  isError={register.isError}
                  onRetry={() => void register.refetch()}
                  pager={{
                    pageNumber: page,
                    pageSize: PAGE_SIZE,
                    totalCount: register.data?.totalCount ?? 0,
                    onPageChange: (next) => update({ page: String(next) }),
                  }}
                />
              </div>
            ),
          },
          {
            id: 'balances',
            label: 'Balances',
            panel: (
              <BalancesTable
                rows={balances.data}
                isLoading={balances.isLoading}
                isError={balances.isError}
                onRetry={() => void balances.refetch()}
              />
            ),
          },
          {
            id: 'integrity',
            label: 'Integrity',
            panel: (
              <IntegrityPanel
                data={verification.data}
                zone={zone}
                isLoading={verification.isLoading}
                isError={verification.isError}
                onRetry={() => void verification.refetch()}
              />
            ),
          },
        ]}
      />
    </div>
  );
}

export default function ControlledDrugsPage() {
  return (
    <Suspense fallback={null}>
      <RegisterPage />
    </Suspense>
  );
}
