import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  cancelMyAppointment,
  getMyAppointment,
  getMyAppointments,
  getMyWaitlist,
  joinWaitlist,
  rescheduleMyAppointment,
  withdrawFromWaitlist,
  type AppointmentScope,
  type JoinWaitlistRequest,
  type RescheduleRequest,
} from '@/lib/api/my-appointments';
import { ApiNotFoundError } from '@/lib/api/client';

/** `lib/hooks/useMyAppointments.ts` — HO-APT-002 §8.3, keys `['me','appointments', …]`. */

function noRetryOn404(failureCount: number, error: unknown) {
  return !(error instanceof ApiNotFoundError) && failureCount < 1;
}

export function useMyAppointments(scope: AppointmentScope) {
  return useQuery({
    queryKey: ['me', 'appointments', 'list', scope],
    queryFn: () => getMyAppointments(scope),
  });
}

export function useMyAppointment(appointmentId: string) {
  return useQuery({
    queryKey: ['me', 'appointments', 'detail', appointmentId],
    queryFn: () => getMyAppointment(appointmentId),
    enabled: !!appointmentId,
    retry: noRetryOn404,
  });
}

export function useMyWaitlist() {
  return useQuery({ queryKey: ['me', 'waitlist'], queryFn: getMyWaitlist });
}

function useInvalidateMine() {
  const queryClient = useQueryClient();
  return () => {
    void queryClient.invalidateQueries({ queryKey: ['me', 'appointments'] });
    void queryClient.invalidateQueries({ queryKey: ['me', 'waitlist'] });
    void queryClient.invalidateQueries({ queryKey: ['booking', 'slots'] });
  };
}

export function useRescheduleMyAppointment(appointmentId: string) {
  const invalidate = useInvalidateMine();
  return useMutation({
    mutationFn: (request: RescheduleRequest) => rescheduleMyAppointment(appointmentId, request),
    onSuccess: invalidate,
  });
}

export function useCancelMyAppointment(appointmentId: string) {
  const invalidate = useInvalidateMine();
  return useMutation({
    mutationFn: (reason: string | null) => cancelMyAppointment(appointmentId, reason),
    onSuccess: invalidate,
  });
}

export function useJoinWaitlist() {
  const invalidate = useInvalidateMine();
  return useMutation({
    mutationFn: (request: JoinWaitlistRequest) => joinWaitlist(request),
    onSuccess: invalidate,
  });
}

export function useWithdrawFromWaitlist() {
  const invalidate = useInvalidateMine();
  return useMutation({
    mutationFn: (waitlistEntryId: string) => withdrawFromWaitlist(waitlistEntryId),
    onSuccess: invalidate,
  });
}
