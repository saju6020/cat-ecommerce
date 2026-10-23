'use client';

import { useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { forgetPreservedBranch } from '@/lib/branch';
import { useSignOutCustomer } from '@/lib/hooks/useAuthMutations';
import { useSignOutStaff } from '@/lib/hooks/useStaffAuthMutations';

/**
 * `components/auth/SignOutButton.tsx` — HO-ACC-002 §11.3/§8.4. Calls the matching sign-out
 * endpoint, then `queryClient.clear()` (the WHOLE cache — leaving one customer's data in memory
 * for the next person at the same browser is a data leak), then routes to the matching sign-in
 * page.
 */
export function SignOutButton({
  audience,
  className,
  children,
}: {
  audience: 'customer' | 'staff';
  /** Replaces the default secondary-button look (the customer account sidebar shows it as a row). */
  className?: string;
  /** Extra leading content, e.g. an icon. */
  children?: React.ReactNode;
}) {
  const queryClient = useQueryClient();
  const router = useRouter();
  const customerMutation = useSignOutCustomer();
  const staffMutation = useSignOutStaff();
  const mutation = audience === 'customer' ? customerMutation : staffMutation;

  async function handleSignOut() {
    try {
      await mutation.mutateAsync();
    } finally {
      // An explicit sign-out clears everything, the active branch included (ADM 12).
      forgetPreservedBranch(queryClient);
      router.replace(audience === 'customer' ? '/sign-in' : '/staff/sign-in');
    }
  }

  return (
    <Button
      variant={className ? 'ghost' : 'secondary'}
      size="sm"
      onClick={handleSignOut}
      disabled={mutation.isPending}
      className={className}
    >
      {children}
      {mutation.isPending ? 'Signing out…' : 'Sign out'}
    </Button>
  );
}
