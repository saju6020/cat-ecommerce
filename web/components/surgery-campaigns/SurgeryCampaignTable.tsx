'use client';

import { useState } from 'react';
import Link from 'next/link';
import { Plus } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ResponsiveTable, type Column } from '@/components/billing/ResponsiveTable';
import { toast } from '@/components/ui/use-toast';
import { describeSurgeryError } from '@/lib/api/errors';
import { formatMoney } from '@/lib/format/money';
import { formatDateOnly } from '@/lib/format/time';
import { useSetCampaignActive } from '@/lib/hooks/useSurgeryCatalogue';
import type { SurgeryCampaign } from '@/lib/api/surgery-campaigns';
import { SurgeryCampaignForm } from './SurgeryCampaignForm';

function ActiveToggle({ campaign }: { campaign: SurgeryCampaign }) {
  const toggle = useSetCampaignActive(campaign.id);
  return (
    <Button
      variant="secondary"
      size="sm"
      disabled={toggle.isPending}
      onClick={async () => {
        try {
          await toggle.mutateAsync(!campaign.isActive);
          toast({ title: campaign.isActive ? 'Campaign deactivated' : 'Campaign activated' });
        } catch (error) {
          toast({ title: 'That didn’t work', description: describeSurgeryError(error) });
        }
      }}
    >
      {campaign.isActive ? 'Deactivate' : 'Activate'}
    </Button>
  );
}

/**
 * `SurgeryCampaignTable` — HO-SRG-002 §11.7, US-SRG-11. Code, name, dates, prices, places used, status as
 * text, a link to the cases, and (administrators only) edit, activate/deactivate and **New campaign**.
 * A campaign is deactivated, never deleted.
 */
export function SurgeryCampaignTable({
  rows,
  canManage,
  isLoading,
  isError,
  onRetry,
  pager,
}: {
  rows: SurgeryCampaign[] | undefined;
  canManage: boolean;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  pager?: {
    pageNumber: number;
    pageSize: number;
    totalCount: number;
    onPageChange: (page: number) => void;
  };
}) {
  const [editing, setEditing] = useState<SurgeryCampaign | null | 'new'>(null);
  const columns: Array<Column<SurgeryCampaign>> = [
    {
      id: 'campaign',
      header: 'Campaign',
      primary: true,
      cell: (c) => (
        <span className="flex flex-col">
          <strong>{c.name}</strong>
          <span className="text-xs text-ink-muted">{c.code}</span>
        </span>
      ),
    },
    {
      id: 'dates',
      header: 'Runs',
      cell: (c) => `${formatDateOnly(c.validFrom)} to ${formatDateOnly(c.validTo)}`,
    },
    {
      id: 'prices',
      header: 'Prices',
      cell: (c) => c.prices.map((p) => `${p.serviceName} ${formatMoney(p.price)}`).join(', '),
    },
    {
      id: 'cases',
      header: 'Cases',
      cell: (c) =>
        c.maxCases === null
          ? `${c.casesBooked} booked`
          : `${c.casesBooked} of ${c.maxCases} booked`,
    },
    {
      id: 'status',
      header: 'Status',
      cell: (c) => (
        <Badge variant={c.isActive ? 'success' : 'neutral'}>
          {c.isActive ? 'Active' : 'Inactive'}
        </Badge>
      ),
    },
    {
      id: 'actions',
      header: 'Actions',
      cell: (c) => (
        <span className="flex flex-wrap gap-2">
          <Link
            href={`/staff/surgeries/campaigns/${c.id}`}
            aria-label={`Cases for ${c.code}`}
            className="text-sm font-semibold text-accent-ink underline underline-offset-2"
          >
            Cases
          </Link>
          {canManage && (
            <>
              <Button
                variant="secondary"
                size="sm"
                aria-label={`Edit ${c.code}`}
                onClick={() => setEditing(c)}
              >
                Edit
              </Button>
              <ActiveToggle campaign={c} />
            </>
          )}
        </span>
      ),
    },
  ];
  return (
    <div className="flex flex-col gap-4">
      {canManage && (
        <div>
          <Button onClick={() => setEditing('new')}>
            <Plus size={16} aria-hidden="true" /> New campaign
          </Button>
        </div>
      )}
      <ResponsiveTable
        caption="Surgery campaigns"
        columns={columns}
        rows={rows}
        getRowId={(c) => c.id}
        isLoading={isLoading}
        isError={isError}
        onRetry={onRetry}
        pager={pager}
        emptyState={{
          title: 'No campaigns yet.',
          description: canManage
            ? 'Create one to offer a reduced price for spay and neuter.'
            : undefined,
        }}
      />
      {editing && (
        <SurgeryCampaignForm
          campaign={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)}
        />
      )}
    </div>
  );
}
