'use client';

import { use, useState } from 'react';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import { Button } from '@/components/ui/button';
import { TaxClassForm } from '@/components/admin/TaxClassForm';
import { EffectiveDatedHistory } from '@/components/admin/EffectiveDatedHistory';
import { ScheduleChangeDialog } from '@/components/admin/ScheduleChangeDialog';
import {
  useTaxClass,
  useUpdateTaxClass,
  useTaxRates,
  useSetTaxRate,
} from '@/lib/hooks/useAdminCatalogue';
import { useMyBranches } from '@/lib/hooks/useActiveBranch';
import { useAdminBranches } from '@/lib/hooks/useAdminBranches';

/** `/staff/admin/tax-classes/[taxClassId]` — US-ADM-04. */
export default function TaxClassDetailPage({
  params,
}: {
  params: Promise<{ taxClassId: string }>;
}) {
  const { taxClassId } = use(params);
  const taxClass = useTaxClass(taxClassId);
  const updateMutation = useUpdateTaxClass(taxClassId);
  const rates = useTaxRates(taxClassId);
  const setRateMutation = useSetTaxRate(taxClassId);
  const myBranches = useMyBranches();
  const branches = useAdminBranches();
  const [scheduleOpen, setScheduleOpen] = useState(false);

  const primaryZone =
    branches.data?.find((b) => b.id === myBranches.data?.primaryBranchId)?.timeZoneId ??
    'Asia/Dhaka';

  // §18 A3: a 403 (TaxClasses.View is Administrator/Accountant/ShopManager only) shows
  // PermissionDeniedState; loading, 404 and any other error are each their own state — a failed
  // request must never leave the page spinning.
  if (taxClass.error instanceof ApiForbiddenError || rates.error instanceof ApiForbiddenError) {
    return <AccessRefusedState error={taxClass.error ?? rates.error} />;
  }
  if (taxClass.error instanceof ApiNotFoundError) {
    return (
      <ErrorState title="Tax class not found" description="This tax class may have been removed." />
    );
  }
  if (taxClass.isLoading) return <LoadingState rows={4} />;
  if (!taxClass.data) {
    return (
      <ErrorState
        description="We couldn't load this tax class just now."
        onRetry={() => void taxClass.refetch()}
      />
    );
  }

  return (
    <div className="flex flex-col gap-8">
      <h1 className="font-display text-xl font-semibold text-ink">{taxClass.data.name}</h1>
      <TaxClassForm
        taxClass={taxClass.data}
        isPending={updateMutation.isPending}
        onSave={(values) =>
          updateMutation.mutateAsync({ name: values.name, isActive: values.isActive })
        }
      />

      <div className="flex flex-col gap-3">
        <div className="flex items-center justify-between gap-3">
          <h2 className="text-sm font-semibold text-ink">Rate history</h2>
          <Button variant="secondary" size="sm" onClick={() => setScheduleOpen(true)}>
            Schedule a new rate
          </Button>
        </div>
        <EffectiveDatedHistory
          timeZoneId={primaryZone}
          rows={(rates.data ?? []).map((r) => ({
            id: r.id,
            effectiveFrom: r.effectiveFrom,
            label: `${r.ratePercent}%`,
          }))}
        />
      </div>

      <ScheduleChangeDialog
        open={scheduleOpen}
        onOpenChange={setScheduleOpen}
        title="Schedule a new tax rate"
        valueLabel="Rate (%)"
        onSchedule={(value, effectiveFrom) =>
          setRateMutation.mutateAsync({ ratePercent: value, effectiveFrom })
        }
      />
    </div>
  );
}
