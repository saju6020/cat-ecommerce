'use client';

import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { ApiForbiddenError } from '@/lib/api/client';
import { todayInZone } from '@/lib/format/time';
import { useActiveBranchZone } from '@/lib/hooks/billing/useActiveBranchZone';
import { useCodCollections, useCodReconciliation } from '@/lib/hooks/billing/useBillingQueries';
import { useRecordCodRemittance } from '@/lib/hooks/billing/useBillingMutations';
import { useUrlFilterState } from '@/lib/hooks/useUrlFilterState';
import { CARD_NUMBER_MESSAGE, looksLikeCardNumber } from '@/lib/validation/cardNumberGuard';
import { useState } from 'react';
import type { CodStatus } from '@/lib/api/billing-reconciliation';
import { CodCollectionsTable, CodReconciliationSummary } from './CodTables';
import { SelectField, TextField } from './Fields';
import { PageHeader, Section } from './PageHeader';
import { QueryBoundary } from './QueryBoundary';
import { RecordRemittanceForm } from './RecordRemittanceForm';

/**
 * `components/billing/CodScreen.tsx` — HO-BIL-002 §11.7 (US-BIL-06 AC1), `/staff/billing/reconciliation/cod`.
 * Per-courier reconciliation, the collections list (filter by courier and status, URL-backed) and the
 * remittance form. Accountants and administrators; the server's `403` is rendered as such.
 */
const FIELDS = ['courier', 'status'] as const;
const STATUSES: Array<{ value: CodStatus; label: string }> = [
  { value: 'unremitted', label: 'Unremitted' },
  { value: 'shortfall', label: 'Shortfall' },
  { value: 'remitted', label: 'Remitted' },
];

export function CodScreen() {
  const { zone } = useActiveBranchZone();
  const { values, setValue } = useUrlFilterState(FIELDS);
  const [courierError, setCourierError] = useState<string | undefined>();
  const collections = useCodCollections({ courierCode: values.courier || undefined, status: (values.status || undefined) as CodStatus | undefined, pageSize: 100 });
  const everything = useCodCollections({ pageSize: 200 });
  const reconciliation = useCodReconciliation({ courierCode: values.courier || undefined });
  const record = useRecordCodRemittance();

  const refused = [collections.error, reconciliation.error].find((e) => e instanceof ApiForbiddenError);
  if (refused) return <AccessRefusedState error={refused} />;

  return (
    <div className="flex flex-col gap-5">
      <PageHeader title="COD reconciliation" description="Cash collected by couriers on delivery, and what they have handed over." />
      <QueryBoundary query={reconciliation} loadingLabel="Loading the courier summary…">
        {(data) => <CodReconciliationSummary couriers={data.couriers} />}
      </QueryBoundary>
      <Section title="Collections">
        <form role="search" aria-label="Collection filters" className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4" onSubmit={(e) => e.preventDefault()}>
          <TextField
            id="cod-courier"
            label="Courier code"
            value={values.courier}
            error={courierError}
            autoComplete="off"
            onChange={(e) => {
              // C-5: a card number is never written to the URL, the history or a request log.
              if (looksLikeCardNumber(e.target.value)) {
                setCourierError(CARD_NUMBER_MESSAGE);
                return;
              }
              setCourierError(undefined);
              setValue('courier', e.target.value);
            }}
          />
          <SelectField id="cod-status" label="Status" value={values.status} onChange={(e) => setValue('status', e.target.value)}>
            <option value="">Any status</option>
            {STATUSES.map((s) => (
              <option key={s.value} value={s.value}>{s.label}</option>
            ))}
          </SelectField>
        </form>
        <CodCollectionsTable
          collections={collections.data?.items}
          isLoading={collections.isLoading}
          isError={collections.isError}
          onRetry={() => void collections.refetch()}
        />
      </Section>
      <Section title="Record a remittance" description="When a courier hands money over, record it against the collections it covers.">
        <RecordRemittanceForm
          collections={everything.data?.items ?? []}
          today={todayInZone(zone)}
          defaultCourier={values.courier}
          onSubmit={(request) => record.mutateAsync(request)}
        />
      </Section>
    </div>
  );
}
