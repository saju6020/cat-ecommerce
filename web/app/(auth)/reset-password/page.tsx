import { AuthCard } from '@/components/auth/AuthCard';
import { ResetPasswordPageClient } from './ResetPasswordPageClient';

// US-ACC-05 — ADR-FE-007. Serves BOTH customer and staff reset links (one endpoint, HO-ACC-001
// §12.4); this page never hints which kind of account a link is for, so it sets no `data-theme`
// (neither exclusively the warm customer layer nor the staff chrome).
export const metadata = {
  referrer: 'no-referrer' as const,
};

export default function ResetPasswordPage() {
  return (
    <AuthCard title="Set a new password">
      <ResetPasswordPageClient />
    </AuthCard>
  );
}
