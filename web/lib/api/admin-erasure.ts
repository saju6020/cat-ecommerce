import { apiFetch } from './client';

/** `lib/api/admin-erasure.ts` — HO-XCT-002 §8.2. Staff-taken erasure (HO-XCT-001 C3). The request
 *  is `{ channel, identityCheck }` since HO-XCT-001's 2026-10-12 gate fix: `identityCheck` is one of
 *  four fixed codes and there is no free-text reason (HO-XCT-002 §1 Amended 2026-10-13). */

export const ERASURE_CHANNELS = ['InPerson', 'Phone', 'Email', 'Letter'] as const;
export type ErasureChannel = (typeof ERASURE_CHANNELS)[number];

export const ERASURE_IDENTITY_CHECKS = [
  'RegisteredMobileCallback',
  'RegisteredEmailReply',
  'InPersonPhotoId',
  'WrittenRequestSigned',
] as const;
export type ErasureIdentityCheck = (typeof ERASURE_IDENTITY_CHECKS)[number];

export interface EraseCustomerRequest {
  channel: ErasureChannel;
  identityCheck: ErasureIdentityCheck;
}

export interface EraseCustomerResult {
  isReapplication: boolean;
}

export async function eraseCustomer(
  customerAccountId: string,
  request: EraseCustomerRequest,
): Promise<EraseCustomerResult> {
  return apiFetch<EraseCustomerResult>(`/api/admin/customers/${customerAccountId}/erasure`, {
    method: 'POST',
    body: request,
  });
}
