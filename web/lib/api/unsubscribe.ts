import { apiFetch } from './client';

/**
 * `lib/api/unsubscribe.ts` — HO-NTF-002 §8.2/§11.4, US-NTF-04. Public: no session is needed (the
 * token in the body is the whole credential). `channel` is the channel the marketing message
 * arrived on (`Email` | `Sms` | `InApp`); `wasSubscribed: false` means the preference was already
 * off. Verified against `NotificationUnsubscribeController.cs`.
 */
export interface UnsubscribeResult {
  channel: string;
  wasSubscribed: boolean;
}

export async function unsubscribeFromMarketing(token: string): Promise<UnsubscribeResult> {
  return apiFetch<UnsubscribeResult>('/api/notifications/unsubscribe', {
    method: 'POST',
    body: { token },
    skipSessionErrorHandling: true,
  });
}
