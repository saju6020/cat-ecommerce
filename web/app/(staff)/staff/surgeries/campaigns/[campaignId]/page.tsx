'use client';

import { Suspense, use } from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { ActiveBranchZoneGate } from '@/components/appointments/ActiveBranchZoneGate';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { ErrorState } from '@/components/common/ErrorState';
import { buttonVariants } from '@/components/ui/button';
import { PageHeading } from '@/components/inventory/fields';
import { CampaignCasesTable } from '@/components/surgery-campaigns/CampaignCasesTable';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import { useFilterUrlUpdate } from '@/lib/hooks/useFilterUrlUpdate';
import { useCampaignCases } from '@/lib/hooks/useSurgeryCatalogue';
import { inputClass, labelClass } from '@/components/surgery/styles';

function CasesView({ campaignId, zone }: { campaignId: string; zone: string }) {
  const params = useSearchParams();
  const setParam = useFilterUrlUpdate();
  const from = params.get('from') ?? '';
  const to = params.get('to') ?? '';
  const page = Math.max(1, Number(params.get('page')) || 1);
  const cases = useCampaignCases(campaignId, {
    from: from || undefined,
    to: to || undefined,
    pageNumber: page,
  });
  if (cases.error instanceof ApiForbiddenError) return <AccessRefusedState error={cases.error} />;
  if (cases.error instanceof ApiNotFoundError) {
    return <ErrorState headingLevel={1} title="We couldn’t find that campaign." />;
  }
  return (
    <div className="flex flex-col gap-5">
      <PageHeading
        title={cases.data ? `${cases.data.name} — cases` : 'Campaign cases'}
        description={
          cases.data
            ? `Code ${cases.data.code}. Surgeries booked under this campaign at the active branch.`
            : undefined
        }
        actions={
          <Link
            href="/staff/surgeries/campaigns"
            className={buttonVariants({ variant: 'secondary' })}
          >
            All campaigns
          </Link>
        }
      />
      <div className="flex flex-wrap gap-4">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="cases-from" className={labelClass}>
            From
          </label>
          <input
            id="cases-from"
            type="date"
            className={inputClass}
            value={from}
            onChange={(event) => setParam('from', event.target.value)}
          />
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="cases-to" className={labelClass}>
            To
          </label>
          <input
            id="cases-to"
            type="date"
            className={inputClass}
            value={to}
            onChange={(event) => setParam('to', event.target.value)}
          />
        </div>
      </div>
      <CampaignCasesTable
        data={cases.data}
        zone={zone}
        isLoading={cases.isPending}
        isError={cases.isError}
        onRetry={() => void cases.refetch()}
        onPageChange={(next) => setParam('page', next > 1 ? String(next) : '')}
      />
    </div>
  );
}

/** `/staff/surgeries/campaigns/[campaignId]` — HO-SRG-002 §11.7 (US-SRG-11): a campaign's cases and totals. */
export default function CampaignPage({ params }: { params: Promise<{ campaignId: string }> }) {
  const { campaignId } = use(params);
  return (
    <Suspense fallback={null}>
      <ActiveBranchZoneGate>
        {(zone) => <CasesView campaignId={campaignId} zone={zone} />}
      </ActiveBranchZoneGate>
    </Suspense>
  );
}
