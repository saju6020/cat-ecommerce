'use client';

import { ResetPasswordForm } from '@/components/auth/ResetPasswordForm';
import { useConfirmPasswordReset } from '@/lib/hooks/useAuthMutations';

// ARCH gate HIGH 3 (first pass) — split out of page.tsx purely so that file can stay a server
// component (it exports `metadata`, which cannot coexist with `'use client'`) while this piece
// wires `useConfirmPasswordReset()`; `ResetPasswordForm` itself takes the result as props.
export function ResetPasswordPageClient() {
  const mutation = useConfirmPasswordReset();
  return (
    <ResetPasswordForm
      onConfirm={(request) => mutation.mutateAsync(request)}
      isPending={mutation.isPending}
    />
  );
}
