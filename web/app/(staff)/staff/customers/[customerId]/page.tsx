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
import { BillingSummaryPanel } from '@/components/billing/BillingSummaryPanel';
import { CustomerOrdersPanel } from '@/components/orders/CustomerOrdersPanel';
import { ErasePersonalDataPanel } from '@/components/retention/ErasePersonalDataPanel';

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
      {/* HO-BIL-002 §11.1/§13 — the Billing panel (US-BIL-03): the money figures and a link to the account. */}
      <BillingSummaryPanel customerId={customerId} />
      {/* HO-ORD-002 §11.14/§13 — the Orders panel (mount only). */}
      <CustomerOrdersPanel customerId={customerId} />
      {/* HO-XCT-002 §11.5/§13 — the Erase personal data panel (US-XCT-06). Shown to Administrators
          (presentation only: Customers.Erase is decided by the server). */}
      {(session?.roleIds ?? []).includes('Administrator') && (
        <ErasePersonalDataPanel customer={customer} />
      )}
    </div>
  );
}
