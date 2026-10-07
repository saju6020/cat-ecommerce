import Link from 'next/link';
import { Button, buttonVariants } from '@/components/ui/button';
import { AuthCard } from '@/components/auth/AuthCard';
import { INVALID_UNSUBSCRIBE_LINK_MESSAGE } from '@/lib/api/errors';

export const MANAGE_PREFERENCES_HREF = '/sign-in?next=/portal/account/notifications';

/** "emails" / "text messages" / "in-app messages" for the channel the server reports. */
export function channelNoun(channel: string): string {
  switch (channel.toLowerCase()) {
    case 'email':
      return 'emails';
    case 'sms':
      return 'text messages';
    case 'inapp':
      return 'in-app messages';
    default:
      return 'messages';
  }
}

export type UnsubscribeView =
  | { phase: 'invalid' }
  | { phase: 'confirm'; error: string | null }
  | { phase: 'done'; channel: string; wasSubscribed: boolean };

function ManageLink() {
  return (
    <Link href={MANAGE_PREFERENCES_HREF} className={buttonVariants({ variant: 'secondary' })}>
      Manage all your preferences
    </Link>
  );
}

/**
 * `components/notifications/UnsubscribePanel.tsx` — HO-NTF-002 §11.4, US-NTF-04. The four states of
 * the public unsubscribe page: invalid link (no button), confirm (a button that is the ONLY thing
 * that sends — ADR-FE-161), done (true/false variants) and a retryable error inside confirm. Pure
 * presentation; `app/(auth)/unsubscribe/page.tsx` reads the token and owns the mutation.
 */
export function UnsubscribePanel({
  view,
  isPending,
  onConfirm,
}: {
  view: UnsubscribeView;
  isPending: boolean;
  onConfirm: () => void;
}) {
  if (view.phase === 'invalid') {
    return (
      <AuthCard title="This link isn't valid">
        <div className="flex flex-col gap-4">
          <p className="text-sm text-ink-muted">{INVALID_UNSUBSCRIBE_LINK_MESSAGE}</p>
          <ManageLink />
        </div>
      </AuthCard>
    );
  }

  if (view.phase === 'done') {
    return (
      <AuthCard title={view.wasSubscribed ? 'You have been unsubscribed' : 'Already unsubscribed'}>
        <div className="flex flex-col gap-4">
          <p role="status" className="text-sm text-ink">
            {view.wasSubscribed
              ? `Done. You won't receive marketing ${channelNoun(view.channel)} from us any more.`
              : 'You were already unsubscribed from these messages.'}
          </p>
          <ManageLink />
        </div>
      </AuthCard>
    );
  }

  return (
    <AuthCard title="Stop marketing messages?">
      <div className="flex flex-col gap-4">
        <p className="text-sm text-ink-muted">
          You&rsquo;ll still receive important messages about your account, your cats and your
          orders.
        </p>
        {view.error && (
          <p role="alert" className="text-sm font-semibold text-danger-ink">
            {view.error}
          </p>
        )}
        <Button onClick={onConfirm} disabled={isPending} aria-busy={isPending}>
          {isPending ? 'Unsubscribing…' : 'Unsubscribe'}
        </Button>
        <ManageLink />
      </div>
    </AuthCard>
  );
}
