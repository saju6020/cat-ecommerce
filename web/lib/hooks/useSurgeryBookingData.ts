'use client';

import { useMemo, useState } from 'react';
import { useActiveBranchId } from './useActiveBranch';
import { useBranchRooms } from './useAdminBranches';
import { useFindCustomers, useStaffCustomer } from './useStaffCustomers';
import { useSurgeryAvailability, useSurgeryServices, useSurgeryStaff } from './useSurgeries';
import { useSurgeryNextDay } from './useSurgeryNextDay';
import { useApplicableCampaigns } from './useSurgeryCatalogue';
import type { BookingChoices } from './useSurgeryBookingParams';
import type { AvailabilityParams } from '@/lib/api/surgeries';

/**
 * `lib/hooks/useSurgeryBookingData.ts` — everything the booking and reschedule forms read: the customer
 * (search and cats), the server's services, staff and theatres, availability for the current choices,
 * the next day with a start when this one has none, and the campaigns that apply. All of it is the
 * server's answer; none of it is worked out here (ADR-FE-069).
 */
export function useSurgeryBookingData(
  choices: Pick<
    BookingChoices,
    'customerId' | 'catId' | 'serviceId' | 'date' | 'surgeonId' | 'assistantId' | 'theatreId'
  >,
  options: { excludeSurgeryId?: string; withCustomer?: boolean } = {},
) {
  const withCustomer = options.withCustomer ?? true;
  const [searchedTerm, setSearchedTerm] = useState<string | null>(null);
  const activeBranchId = useActiveBranchId();

  const search = useFindCustomers(
    searchedTerm?.includes('@') ? { email: searchedTerm } : { mobile: searchedTerm ?? undefined },
    withCustomer && !!searchedTerm,
  );
  const customer = useStaffCustomer(withCustomer ? choices.customerId : '');
  const services = useSurgeryServices();
  const staff = useSurgeryStaff(choices.serviceId);
  const rooms = useBranchRooms(activeBranchId ?? '');
  const theatres = useMemo(
    () => (rooms.data ?? []).filter((room) => room.kind === 'Theatre' && room.isActive),
    [rooms.data],
  );

  const availabilityParams: AvailabilityParams | null =
    choices.serviceId && choices.date
      ? {
          serviceId: choices.serviceId,
          date: choices.date,
          surgeonId: choices.surgeonId || undefined,
          assistantId: choices.assistantId || undefined,
          theatreId: choices.theatreId || undefined,
          catId: choices.catId || undefined,
          excludeSurgeryId: options.excludeSurgeryId,
        }
      : null;
  const availability = useSurgeryAvailability(availabilityParams);
  const noStarts = !!availability.data && availability.data.starts.length === 0;
  const nextFreeDay = useSurgeryNextDay(availabilityParams, noStarts);
  const campaigns = useApplicableCampaigns(choices.serviceId, choices.date);

  return {
    searchedTerm,
    setSearchedTerm,
    search,
    customer,
    services,
    staff,
    theatres,
    availability,
    nextFreeDay,
    campaigns,
  };
}
