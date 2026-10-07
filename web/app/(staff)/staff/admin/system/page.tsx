'use client';

import { useSystemStatus, useSetMaintenanceMode } from '@/lib/hooks/useSystemStatus';
import { MaintenanceSwitch } from '@/components/admin/MaintenanceSwitch';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import { ApiForbiddenError } from '@/lib/api/client';

/** `/staff/admin/system` — US-ADM-08. */
export default function SystemPage() {
  const { data, isLoading, error, refetch } = useSystemStatus();
  const mutation = useSetMaintenanceMode();

  if (error instanceof ApiForbiddenError) return <AccessRefusedState error={error} />;
  if (isLoading) return <LoadingState rows={3} />;
  if (!data) {
    return (
      <ErrorState
        description="We couldn't load the system status just now."
        onRetry={() => void refetch()}
      />
    );
  }

  return (
    <MaintenanceSwitch
      status={data}
      isPending={mutation.isPending}
      onSetMaintenanceMode={(maintenanceMode, message) =>
        mutation.mutateAsync({ maintenanceMode, message })
      }
    />
  );
}
