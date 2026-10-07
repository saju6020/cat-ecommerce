import { apiFetch } from './client';
import type { AppointmentStatus, TriagePriority } from './types';

/**
 * `lib/api/front-desk.ts` — HO-APT-002 §8.2: the live queue and walk-in registration
 * (HO-APT-001 §12.4, `StaffQueueController` / `StaffWalkInsController`). The queue is recomputed
 * by the server on every read — order and estimated waits included (§8.7).
 */

export interface QueueEntry {
  appointmentId: string;
  position: number;
  catName: string;
  catIsHighRisk: boolean;
  customerName: string;
  service: string;
  triagePriority: TriagePriority;
  isWalkIn: boolean;
  dueOn: string;
  checkedInOn: string | null;
  status: AppointmentStatus;
  performerName: string | null;
  estimatedWaitMinutes: number | null;
}

export interface ClinicQueue {
  generatedOn: string;
  performersOnShift: number;
  entries: QueueEntry[];
}

/** `performerId` is a staff id, or the literal `"me"` (resolved by the server to the caller). */
export async function getClinicQueue(performerId?: string): Promise<ClinicQueue> {
  const query = performerId ? `?performerId=${encodeURIComponent(performerId)}` : '';
  return apiFetch<ClinicQueue>(`/api/staff/queue${query}`);
}

export interface RegisterWalkInRequest {
  customerId: string;
  catId: string;
  serviceId: string;
  triagePriority: TriagePriority;
  presentingComplaint?: string;
}

export async function registerWalkIn(
  request: RegisterWalkInRequest,
): Promise<{ appointmentId: string }> {
  return apiFetch<{ appointmentId: string }>('/api/staff/walk-ins', {
    method: 'POST',
    body: request,
  });
}
