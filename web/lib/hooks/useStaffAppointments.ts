import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  bookStaffAppointment,
  cancelStaffAppointment,
  checkInAppointment,
  completeAppointment,
  getCalendar,
  getCustomerAppointmentSummary,
  getDayList,
  getFlaggedAppointments,
  getStaffAppointment,
  getStaffSlots,
  moveAppointment,
  recordNoShow,
  setTriagePriority,
  startAppointment,
  type CalendarParams,
  type MoveRequest,
  type StaffBookRequest,
  type StaffSlotsParams,
  type StartRequest,
} from '@/lib/api/staff-appointments';
import type { TriagePriority } from '@/lib/api/types';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import { useBranchScope } from './useBranchScope';

/**
 * `lib/hooks/useStaffAppointments.ts` — HO-APT-002 §8.3. Calendar and day list poll every 30 s
 * (UI-5, ADR-FE-027); every key carries the active branch so a branch switch refetches. Polling
 * only swaps the data — callers keep dialogs and drags in their own state (and freeze the data
 * during a drag with `useFrozenWhile`), so nothing open is reset by a poll.
 */

export const STAFF_POLL_MS = 30_000;

function noRetryOnRefusal(failureCount: number, error: unknown) {
  if (error instanceof ApiForbiddenError || error instanceof ApiNotFoundError) return false;
  return failureCount < 1;
}

export function useCalendar(
  params: CalendarParams,
  options: { enabled?: boolean; poll?: boolean } = {},
) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: [
      'staff',
      'calendar',
      branchId,
      params.from,
      params.to,
      params.view,
      params.ids ?? [],
    ],
    queryFn: () => getCalendar(params),
    enabled: ready && (options.enabled ?? true),
    refetchInterval: options.poll === false ? false : STAFF_POLL_MS,
    retry: noRetryOnRefusal,
  });
}

export function useDayList(date: string) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['staff', 'day', branchId, date],
    queryFn: () => getDayList(date),
    enabled: ready,
    refetchInterval: STAFF_POLL_MS,
    retry: noRetryOnRefusal,
  });
}

export function useStaffAppointment(appointmentId: string, options?: { poll?: boolean }) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['staff', 'appointment', branchId, appointmentId],
    queryFn: () => getStaffAppointment(appointmentId),
    enabled: ready && !!appointmentId,
    refetchInterval: options?.poll ? STAFF_POLL_MS : false,
    retry: noRetryOnRefusal,
  });
}

/**
 * The day list and the queue carry no `allowedTransitions` (only the detail does — HO-APT-001
 * §12.4), and ADR-FE-028 forbids guessing the action list from a status, so the Front desk reads
 * each open row's detail (polled with the list) and renders actions only from that answer.
 */
export function useStaffAppointmentDetails(appointmentIds: string[]) {
  const { branchId, ready } = useBranchScope();
  return useQueries({
    queries: appointmentIds.map((appointmentId) => ({
      queryKey: ['staff', 'appointment', branchId, appointmentId],
      queryFn: () => getStaffAppointment(appointmentId),
      enabled: ready,
      refetchInterval: STAFF_POLL_MS,
      retry: noRetryOnRefusal,
    })),
  });
}

export function useStaffSlots(params: Partial<StaffSlotsParams>) {
  const { branchId, ready } = useBranchScope();
  const hasInputs = !!params.serviceId && !!params.date;
  return useQuery({
    queryKey: ['staff', 'slots', branchId, params],
    queryFn: () => getStaffSlots(params as StaffSlotsParams),
    enabled: ready && hasInputs,
    staleTime: 0,
    retry: noRetryOnRefusal,
  });
}

export function useFlaggedAppointments() {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['staff', 'flagged', branchId],
    queryFn: getFlaggedAppointments,
    enabled: ready,
    retry: noRetryOnRefusal,
  });
}

export function useCustomerAppointmentSummary(customerId: string | null) {
  const { ready } = useBranchScope();
  return useQuery({
    queryKey: ['staff', 'customer-appointments', customerId],
    queryFn: () => getCustomerAppointmentSummary(customerId!),
    enabled: ready && !!customerId,
    retry: noRetryOnRefusal,
  });
}

function useInvalidateStaffAppointments() {
  const queryClient = useQueryClient();
  return () => {
    for (const key of ['calendar', 'day', 'appointment', 'queue', 'flagged', 'slots']) {
      void queryClient.invalidateQueries({ queryKey: ['staff', key] });
    }
    void queryClient.invalidateQueries({ queryKey: ['staff', 'customer-appointments'] });
  };
}

export function useBookStaffAppointment() {
  const invalidate = useInvalidateStaffAppointments();
  return useMutation({
    mutationFn: (request: StaffBookRequest) => bookStaffAppointment(request),
    onSuccess: invalidate,
  });
}

export function useMoveAppointment() {
  const invalidate = useInvalidateStaffAppointments();
  return useMutation({
    mutationFn: ({ appointmentId, request }: { appointmentId: string; request: MoveRequest }) =>
      moveAppointment(appointmentId, request),
    // A refused move (400/409) changes nothing, but the calendar the user dragged on may be stale
    // (the 409 case) — refetch on settle either way.
    onSettled: invalidate,
  });
}

export function useCancelStaffAppointment() {
  const invalidate = useInvalidateStaffAppointments();
  return useMutation({
    mutationFn: ({ appointmentId, reason }: { appointmentId: string; reason: string }) =>
      cancelStaffAppointment(appointmentId, reason),
    onSuccess: invalidate,
  });
}

export function useCheckInAppointment() {
  const invalidate = useInvalidateStaffAppointments();
  return useMutation({
    mutationFn: ({
      appointmentId,
      triagePriority,
    }: {
      appointmentId: string;
      triagePriority?: TriagePriority;
    }) => checkInAppointment(appointmentId, triagePriority),
    onSuccess: invalidate,
  });
}

export function useSetTriagePriority() {
  const invalidate = useInvalidateStaffAppointments();
  return useMutation({
    mutationFn: ({
      appointmentId,
      triagePriority,
    }: {
      appointmentId: string;
      triagePriority: TriagePriority;
    }) => setTriagePriority(appointmentId, triagePriority),
    onSuccess: invalidate,
  });
}

export function useStartAppointment() {
  const invalidate = useInvalidateStaffAppointments();
  return useMutation({
    mutationFn: ({ appointmentId, request }: { appointmentId: string; request?: StartRequest }) =>
      startAppointment(appointmentId, request),
    onSuccess: invalidate,
  });
}

export function useCompleteAppointment() {
  const invalidate = useInvalidateStaffAppointments();
  return useMutation({
    mutationFn: (appointmentId: string) => completeAppointment(appointmentId),
    onSuccess: invalidate,
  });
}

export function useRecordNoShow() {
  const invalidate = useInvalidateStaffAppointments();
  return useMutation({
    mutationFn: (appointmentId: string) => recordNoShow(appointmentId),
    onSuccess: invalidate,
  });
}
