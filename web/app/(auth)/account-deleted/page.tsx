import Link from 'next/link';
import { buttonVariants } from '@/components/ui/button';
import { AuthCard } from '@/components/auth/AuthCard';

// US-ACC-13. Reached after account deletion — the browser holds no identity by this point
// (`queryClient.clear()` already ran), so this is a plain `(auth)` confirmation page.
export default function AccountDeletedPage() {
  return (
    <div data-theme="customer" className="w-full">
      <AuthCard title="Your account has been deleted">
        <div className="flex flex-col gap-4">
          <p className="text-sm text-ink-muted">
            Your personal details, contact details and addresses have been removed. Clinical and
            billing records are kept for the legally required period, with your name replaced by
            &ldquo;Anonymised customer&rdquo;.
          </p>
          <Link href="/sign-in" className={buttonVariants({ variant: 'secondary' })}>
            Back to sign in
          </Link>
        </div>
      </AuthCard>
    </div>
  );
}
