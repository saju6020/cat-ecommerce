import { useCustomerSession } from '@/lib/hooks/useSession';

/** The first word of a full name, or `null` when there is none. */
export function firstNameOf(fullName: string | null | undefined): string | null {
  const first = fullName?.trim().split(/\s+/)[0];
  return first ? first : null;
}

/**
 * The signed-in customer's first name for greeting headlines ("Almost there, Farhana"), or `null`
 * when the session does not have one (or has not loaded) - callers then drop the name rather than
 * show a placeholder.
 */
export function useCustomerFirstName(): string | null {
  const session = useCustomerSession();
  return firstNameOf(session.data?.fullName);
}
