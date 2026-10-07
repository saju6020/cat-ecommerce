'use client';

import { AuthCard } from '@/components/auth/AuthCard';
import { ForgotPasswordForm } from '@/components/auth/ForgotPasswordForm';
import { useRequestStaffPasswordReset } from '@/lib/hooks/useStaffAuthMutations';

// US-ACC-05/14 (staff) — also a new staff member's first password (US-ACC-14 AC3), reached via
// "Set or reset password" on the staff sign-in page. No `data-theme` — staff, not warm layer.
// ARCH gate HIGH 3 (first pass) — this page wires useRequestStaffPasswordReset(); see the
// customer forgot-password page's own comment.
export default function StaffForgotPasswordPage() {
  const mutation = useRequestStaffPasswordReset();

  return (
    <AuthCard title="Set or reset password">
      <ForgotPasswordForm
        audience="staff"
        onRequestReset={(identifier) => mutation.mutateAsync(identifier)}
        isPending={mutation.isPending}
        error={mutation.error}
      />
    </AuthCard>
  );
}
