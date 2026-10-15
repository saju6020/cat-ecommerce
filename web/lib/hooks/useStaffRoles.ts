import { useStaffSession } from './useSession';

/**
 * `lib/hooks/useStaffRoles.ts` — HO-PHM-002 §8.5. The signed-in staff member's role ids, for
 * PRESENTATION only (which button to show, whether to say "you cannot prescribe" before the vet types).
 * The server enforces every action regardless (HO-ACC-002 ADR-FE-009); a screen that hides a control
 * because of this still renders the server's `403` if a request goes out anyway.
 */
export function useStaffRoles(): {
  roleIds: string[];
  ready: boolean;
  has: (...roles: string[]) => boolean;
} {
  const { data, isLoading } = useStaffSession();
  const roleIds = data?.roleIds ?? [];
  return {
    roleIds,
    ready: !isLoading,
    has: (...roles: string[]) => roles.some((role) => roleIds.includes(role)),
  };
}
