'use client';

import { Suspense } from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { buttonVariants } from '@/components/ui/button';
import { PageHeading } from '@/components/inventory/fields';
import { SurgeryCampaignTable } from '@/components/surgery-campaigns/SurgeryCampaignTable';
import { ApiForbiddenError } from '@/lib/api/client';
import { useFilterUrlUpdate } from '@/lib/hooks/useFilterUrlUpdate';
import { useStaffRoles } from '@/lib/hooks/useStaffRoles';
import { useSurgeryCampaigns } from '@/lib/hooks/useSurgeryCatalogue';

const PAGE_SIZE = 20;

function CampaignsView() {
  const params = useSearchParams();
  const setParam = useFilterUrlUpdate();
  const page = Math.max(1, Number(params.get('page')) || 1);
  const campaigns = useSurgeryCampaigns(page, PAGE_SIZE);
  const roles = useStaffRoles();
  if (campaigns.error instanceof ApiForbiddenError)
    return <AccessRefusedState error={campaigns.error} />;
  return (
    <div className="flex flex-col gap-5">
      <PageHeading
        title="Surgery campaigns"
        description="Reduced-price programmes for spay and neuter. A surgery booked under a campaign keeps the campaign price."
        actions={
          <Link href="/staff/surgeries" className={buttonVariants({ variant: 'secondary' })}>
            Theatre day list
          </Link>
        }
      />
      <SurgeryCampaignTable
        rows={campaigns.data?.items}
        canManage={roles.has('Administrator')}
        isLoading={campaigns.isPending}
        isError={campaigns.isError}
        onRetry={() => void campaigns.refetch()}
        pager={
          campaigns.data
            ? {
                pageNumber: campaigns.data.pageNumber,
                pageSize: campaigns.data.pageSize,
                totalCount: campaigns.data.totalCount,
                onPageChange: (next) => setParam('page', next > 1 ? String(next) : ''),
              }
            : undefined
        }
      />
    </div>
  );
}

/** `/staff/surgeries/campaigns` — HO-SRG-002 §11.7 (US-SRG-11): the campaign list and editor. */
export default function SurgeryCampaignsPage() {
  return (
    <Suspense fallback={null}>
      <CampaignsView />
    </Suspense>
  );
}
