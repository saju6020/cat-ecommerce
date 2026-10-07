'use client';

import { use } from 'react';
import { useStaffCustomer } from '@/lib/hooks/useStaffCustomers';
import { useStaffSession } from '@/lib/hooks/useSession';
import { CustomerMessagesLink } from '@/components/notifications/CustomerMessagesLink';
import { CustomerDetail } from '@/components/customer/CustomerDetail';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import { AppointmentSummaryPanel } from '@/components/appointments/AppointmentSummaryPanel';
import { useCustomerAppointmentSummary } from '@/lib/hooks/useStaffAppointments';

// US-ACC-10/11.
export default function StaffCustomerDetailPage({
  params,
}: {
  params: Promise<{ customerId: string }>;
}) {
  const { customerId } = use(params);
  const { data: customer, isLoading, isError, error, refetch } = useStaffCustomer(customerId);
  const { data: session } = useStaffSession();
  const appointments = useCustomerAppointmentSummary(customerId);

  if (isLoading) return <LoadingState variant="card" rows={5} label="Loading customer…" />;
  if (error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  if (error instanceof ApiNotFoundError) {
    return (
      <ErrorState
        title="Customer not found"
        description="This record may have been removed or merged."
      />
    );
  }
  // ARCHITECTURE gate third pass, HIGH — see the matching comment on
  // staff-accounts/[staffAccountId]/page.tsx: render cached data when present rather than swap the
  // whole page out for an error screen on `isError` alone. This screen is read-only today (the
  // gate's own note — nothing is lost here yet), but the same race is just as real, and the next
  // staff-customer edit feature would inherit it silently without this guard already in place.
  if (isError && !customer) {
    return (
      <ErrorState
        description="We couldn't load this customer just now."
        onRetry={() => refetch()}
      />
    );
  }
  if (!customer) return null;

  // HO-NTF-002 §11.5/§13 — a Messages link (presentation only; the log's own 403 still decides).
  return (
    <div className="flex flex-col gap-6">
      <CustomerDetail customer={customer} />
      <CustomerMessagesLink customerId={customer.id} roleIds={session?.roleIds ?? []} />
      {/* HO-APT-002 §11.7 — the Appointments panel (US-CLN-08). */}
      <AppointmentSummaryPanel
        customerId={customerId}
        summary={appointments.data}
        isLoading={appointments.isLoading}
        isError={appointments.isError}
        onRetry={() => void appointments.refetch()}
      />
    </div>
  );
}
