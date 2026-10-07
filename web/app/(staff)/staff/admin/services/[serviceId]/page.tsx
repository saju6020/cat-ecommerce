'use client';

import { use, useState } from 'react';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import { ServiceForm } from '@/components/admin/ServiceForm';
import { EffectiveDatedHistory } from '@/components/admin/EffectiveDatedHistory';
import { ScheduleChangeDialog } from '@/components/admin/ScheduleChangeDialog';
import {
  useAdminService,
  useUpdateClinicService,
  useSetServiceActivation,
  useServicePrices,
  useSetServicePrice,
  useTaxClasses,
} from '@/lib/hooks/useAdminCatalogue';
import { useStaffRoleCatalogue } from '@/lib/hooks/useStaffAdmin';
import { useMyBranches } from '@/lib/hooks/useActiveBranch';
import { useAdminBranches } from '@/lib/hooks/useAdminBranches';
import { formatMoney } from '@/lib/format/money';

/** `/staff/admin/services/[serviceId]` — US-ADM-04. */
export default function ServiceDetailPage({ params }: { params: Promise<{ serviceId: string }> }) {
  const { serviceId } = use(params);
  const service = useAdminService(serviceId);
  const updateMutation = useUpdateClinicService(serviceId);
  const activationMutation = useSetServiceActivation(serviceId);
  const prices = useServicePrices(serviceId);
  const setPriceMutation = useSetServicePrice(serviceId);
  const myBranches = useMyBranches();
  const branches = useAdminBranches();
  const roleCatalogue = useStaffRoleCatalogue();
  const taxClasses = useTaxClasses();
  const [scheduleOpen, setScheduleOpen] = useState(false);
  const [activationConfirmOpen, setActivationConfirmOpen] = useState(false);

  const primaryZone =
    branches.data?.find((b) => b.id === myBranches.data?.primaryBranchId)?.timeZoneId ??
    'Asia/Dhaka';

  // §18 A3 — see the tax-class detail page: 403 → PermissionDeniedState (a branch refusal is the
  // usual cause here, Services.View being open to all staff); never an endless spinner on error.
  if (service.error instanceof ApiForbiddenError || prices.error instanceof ApiForbiddenError) {
    return <AccessRefusedState error={service.error ?? prices.error} />;
  }
  if (service.error instanceof ApiNotFoundError) {
    return (
      <ErrorState title="Service not found" description="This service may have been removed." />
    );
  }
  if (service.isLoading) return <LoadingState rows={6} />;
  if (!service.data) {
    return (
      <ErrorState
        description="We couldn't load this service just now."
        onRetry={() => void service.refetch()}
      />
    );
  }

  return (
    <div className="flex flex-col gap-8">
      <h1 className="font-display text-xl font-semibold text-ink">{service.data.name}</h1>
      <ServiceForm
        service={service.data}
        roleCatalogue={roleCatalogue}
        taxClasses={taxClasses}
        isPending={updateMutation.isPending}
        onSave={(values) => updateMutation.mutateAsync(values)}
      />

      <div className="flex items-center gap-3">
        <Button variant="secondary" size="sm" onClick={() => setActivationConfirmOpen(true)}>
          {service.data.isActive ? 'Deactivate' : 'Reactivate'}
        </Button>
      </div>

      <div className="flex flex-col gap-3">
        <div className="flex items-center justify-between gap-3">
          <h2 className="text-sm font-semibold text-ink">Price history</h2>
          <Button variant="secondary" size="sm" onClick={() => setScheduleOpen(true)}>
            Schedule a new price
          </Button>
        </div>
        <EffectiveDatedHistory
          timeZoneId={primaryZone}
          rows={(prices.data ?? []).map((p) => ({
            id: p.id,
            effectiveFrom: p.effectiveFrom,
            label: formatMoney(p.amount),
          }))}
        />
      </div>

      <ScheduleChangeDialog
        open={scheduleOpen}
        onOpenChange={setScheduleOpen}
        title="Schedule a new price"
        valueLabel="Amount (BDT)"
        onSchedule={(value, effectiveFrom) =>
          setPriceMutation.mutateAsync({ amount: value, currencyCode: 'BDT', effectiveFrom })
        }
      />

      <ConfirmDialog
        open={activationConfirmOpen}
        onOpenChange={setActivationConfirmOpen}
        title={service.data.isActive ? 'Deactivate this service?' : 'Reactivate this service?'}
        consequence={
          service.data.isActive
            ? `${service.data.name} will no longer be offered for new bookings.`
            : `${service.data.name} will be available for booking again.`
        }
        confirmLabel={service.data.isActive ? 'Deactivate' : 'Reactivate'}
        variant={service.data.isActive ? 'destructive' : 'primary'}
        onConfirm={() => activationMutation.mutateAsync(!service.data!.isActive)}
      />
    </div>
  );
}
