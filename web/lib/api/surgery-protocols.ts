import { apiFetch } from './client';
import type { SurgeryFollowUpKind } from './surgeries';

/**
 * `lib/api/surgery-protocols.ts` — HO-SRG-002 §8.2. Built to `StaffSurgeryProtocolsController`,
 * `SurgeryProtocolRequests.cs` and `SurgeryProtocolViewModels.cs`. One row per surgical service;
 * `isDefault` is true while the service has no protocol of its own (*"Using defaults"*).
 */

export interface ProtocolFollowUp {
  kind: SurgeryFollowUpKind;
  dayOffset: number;
  windowFromDay: number;
  windowToDay: number;
}

export interface SurgeryProtocol {
  serviceId: string;
  serviceName: string;
  category: string;
  isDefault: boolean;
  fastingInstructions: string;
  fastingHours: number;
  postOpInstructions: string;
  followUps: ProtocolFollowUp[];
  updatedOn: string | null;
  version: number;
}

export interface UpsertProtocolRequest {
  fastingInstructions: string;
  fastingHours: number;
  postOpInstructions: string;
  followUps: ProtocolFollowUp[];
}

export function listSurgeryProtocols(): Promise<SurgeryProtocol[]> {
  return apiFetch('/api/staff/surgery-protocols');
}

export function upsertSurgeryProtocol(
  serviceId: string,
  body: UpsertProtocolRequest,
): Promise<{ serviceId: string }> {
  return apiFetch(`/api/staff/surgery-protocols/${encodeURIComponent(serviceId)}`, {
    method: 'PUT',
    body,
  });
}
