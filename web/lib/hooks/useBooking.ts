import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  confirmAppointment,
  getBookableServices,
  getBookingPerformers,
  getBookingSlots,
  getClinicBranches,
  placeSlotHold,
  releaseSlotHold,
  type GetSlotsParams,
  type PlaceHoldRequest,
} from '@/lib/api/booking';
import { addDaysIso } from '@/lib/format/appointment';

/**
 * `lib/hooks/useBooking.ts` — HO-APT-002 §8.3. Slots are refetched on step entry and after any
 * `409` (`staleTime: 0` + `refetchOnMount: 'always'`); the hold itself is NOT cached anywhere —
 * it lives in the flow's component state (ADR-FE-024).
 */

export const BOOKING_SLOTS_KEY = ['booking', 'slots'] as const;

export function useBookableServices() {
  return useQuery({
    queryKey: ['booking', 'services'],
    queryFn: getBookableServices,
    staleTime: 60_000,
  });
}

export function useClinicBranches() {
  return useQuery({
    queryKey: ['booking', 'branches'],
    queryFn: getClinicBranches,
    staleTime: 60_000,
  });
}

export function useBookingPerformers(branchId: string | null, serviceId: string | null) {
  return useQuery({
    queryKey: ['booking', 'performers', branchId, serviceId],
    queryFn: () => getBookingPerformers(branchId!, serviceId!),
    enabled: !!branchId && !!serviceId,
  });
}

export function useBookingSlots(params: Partial<GetSlotsParams>) {
  const ready = !!params.branchId && !!params.serviceId && !!params.date;
  return useQuery({
    queryKey: [...BOOKING_SLOTS_KEY, params],
    queryFn: () => getBookingSlots(params as GetSlotsParams),
    enabled: ready,
    staleTime: 0,
    refetchOnMount: 'always',
  });
}

/** How many days ahead the "next day with times" search looks (§11.1). */
export const NEXT_DAY_LOOKAHEAD = 7;

/**
 * US-CLN-02/09 — when a day has no times, finds the next one that does: asks the server for one
 * day at a time, up to 7, and stops at the first with slots. The availability decision is always
 * the server's; this only walks the calendar.
 */
export function useNextDayWithSlots(params: Partial<GetSlotsParams>, enabled: boolean) {
  const ready = enabled && !!params.branchId && !!params.serviceId && !!params.date;
  return useQuery({
    queryKey: [...BOOKING_SLOTS_KEY, 'next-day', params],
    queryFn: async () => {
      for (let offset = 1; offset <= NEXT_DAY_LOOKAHEAD; offset += 1) {
        const date = addDaysIso(params.date!, offset);
        const day = await getBookingSlots({ ...(params as GetSlotsParams), date });
        if (day.slots.length > 0) return date;
      }
      return null;
    },
    enabled: ready,
    staleTime: 0,
  });
}

export function usePlaceSlotHold() {
  return useMutation({ mutationFn: (request: PlaceHoldRequest) => placeSlotHold(request) });
}

export function useReleaseSlotHold() {
  return useMutation({
    mutationFn: ({ appointmentId, keepalive }: { appointmentId: string; keepalive?: boolean }) =>
      releaseSlotHold(appointmentId, { keepalive }),
  });
}

export function useConfirmAppointment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      appointmentId,
      reasonForVisit,
    }: {
      appointmentId: string;
      reasonForVisit: string | null;
    }) => confirmAppointment(appointmentId, reasonForVisit),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['me', 'appointments'] });
      void queryClient.invalidateQueries({ queryKey: ['me', 'waitlist'] });
      void queryClient.invalidateQueries({ queryKey: BOOKING_SLOTS_KEY });
    },
  });
}
