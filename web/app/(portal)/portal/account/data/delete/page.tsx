'use client';

import { useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { DeleteAccountForm } from '@/components/account/DeleteAccountForm';
import { useDeleteAccount } from '@/lib/hooks/useAccountMutations';

// US-ACC-13.
// ARCH gate HIGH 3 (first pass) — this page wires useDeleteAccount() and owns the success
// behaviour (clear the whole cache, route to /account-deleted); DeleteAccountForm itself only
// submits the current password and interprets whatever onDelete throws.
export default function DeleteAccountPage() {
  const mutation = useDeleteAccount();
  const queryClient = useQueryClient();
  const router = useRouter();

  return (
    <DeleteAccountForm
      onDelete={async (currentPassword) => {
        await mutation.mutateAsync({ currentPassword });
        queryClient.clear();
        router.push('/account-deleted');
      }}
      isPending={mutation.isPending}
      error={mutation.error}
    />
  );
}
