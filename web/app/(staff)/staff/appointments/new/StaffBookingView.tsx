'use client';

import { useCallback, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { StaffBookingForm, type BookingQuery } from '@/components/appointments/StaffBookingForm';
import { toast } from '@/components/ui/use-toast';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { useAdminServices } from '@/lib/hooks/useAdminCatalogue';
import { useAdminBranches } from '@/lib/hooks/useAdminBranches';
import { useActiveBranchId } from '@/lib/hooks/useActiveBranch';
import { useBookingPerformers } from '@/lib/hooks/useBooking';
import {
  useBookStaffAppointment,
  useCustomerAppointmentSummary,
  useStaffSlots,
} from '@/lib/hooks/useStaffAppointments';
import { useFindCustomers, useStaffCustomer } from '@/lib/hooks/useStaffCustomers';
import { ApiForbiddenError } from '@/lib/api/client';
import type { StaffCustomerSummary } from '@/lib/api/staff-customers';
import { CatHeader } from '../CatHeader';

/** Container for staff booking (HO-APT-002 §11.3). */
export function StaffBookingView() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const [customerId, setCustomerId] = useState<string | null>(searchParams.get('customerId'));
  const [searchedTerm, setSearchedTerm] = useState<string | null>(null);
  const [query, setQuery] = useState<BookingQuery>({
    catId: '',
    serviceId: '',
    date: '',
    performerId: '',
  });
  const onQueryChange = useCallback((next: BookingQuery) => {
    setQuery((current) =>
      current.catId === next.catId &&
      current.serviceId === next.serviceId &&
      current.date === next.date &&
      current.performerId === next.performerId
        ? current
        : next,
    );
  }, []);

  const branches = useAdminBranches();
  const activeBranchId = useActiveBranchId();
  const zone =
    branches.data?.find((b) => b.id === activeBranchId)?.timeZoneId ??
    branches.data?.[0]?.timeZoneId ??
    'UTC';
  const search = useFindCustomers(
    searchedTerm?.includes('@') ? { email: searchedTerm } : { mobile: searchedTerm ?? undefined },
    !!searchedTerm,
  );
  const customer = useStaffCustomer(customerId ?? '');
  const summary = useCustomerAppointmentSummary(customerId);
  const services = useAdminServices();
  const performers = useBookingPerformers(activeBranchId, query.serviceId || null);
  const slots = useStaffSlots({
    serviceId: query.serviceId || undefined,
    date: query.date || undefined,
    performerId: query.performerId || undefined,
    catId: query.catId || undefined,
  });
  const book = useBookStaffAppointment();

  if (customer.error instanceof ApiForbiddenError) return <PermissionDeniedState />;

  const picked = customer.data
    ? {
        id: customer.data.id,
        fullName: customer.data.fullName,
        mobileNumber: customer.data.mobileNumber,
        noShowCount: summary.data?.noShowCount ?? null,
      }
    : null;

  return (
    <div className="flex flex-col gap-5">
      <h1 className="font-display text-2xl font-semibold text-ink">Book an appointment</h1>
      <StaffBookingForm
        customer={picked}
        onSelectCustomer={(c: StaffCustomerSummary) => setCustomerId(c.id)}
        onClearCustomer={() => {
          setCustomerId(null);
          setSearchedTerm(null);
        }}
        onSearch={setSearchedTerm}
        results={search.data}
        isSearching={search.isFetching}
        searchedTerm={searchedTerm}
        cats={(customer.data?.cats ?? [])
          .filter((cat) => !cat.isDeceased)
          .map((cat) => ({ id: cat.catId, name: cat.name }))}
        initialCatId={searchParams.get('catId') ?? undefined}
        initialDate={searchParams.get('date') ?? undefined}
        initialPerformerId={searchParams.get('performerId') ?? undefined}
        initialTime={searchParams.get('time') ?? undefined}
        catHeader={query.catId ? <CatHeader catId={query.catId} /> : null}
        services={(services.data ?? [])
          .filter((service) => service.isActive && service.isPriced)
          .map((service) => ({ id: service.id, name: service.name }))}
        performers={performers.data ?? []}
        slots={slots.data}
        slotsLoading={slots.isFetching}
        slotsError={slots.isError}
        zone={zone}
        onQueryChange={onQueryChange}
        onSlotTaken={() => void slots.refetch()}
        onBook={async (values) => {
          const result = await book.mutateAsync({
            customerId: values.customerId,
            catId: values.catId,
            serviceId: values.serviceId,
            startsOn: values.startsOn,
            performerId: values.performerId,
            roomId: values.roomId,
            reasonForVisit: values.reasonForVisit.trim() || undefined,
          });
          toast({ title: 'Appointment booked' });
          router.push(`/staff/appointments/${result.appointmentId}`);
        }}
      />
    </div>
  );
}
