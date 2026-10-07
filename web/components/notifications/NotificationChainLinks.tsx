import Link from 'next/link';
import type { NotificationChain } from '@/lib/api/notifications-log';

function ChainLink({ id, label }: { id: string; label: string }) {
  return (
    <Link
      href={`/staff/notifications/${id}`}
      className="font-semibold text-accent-ink underline underline-offset-2"
    >
      {label}
    </Link>
  );
}

/**
 * `components/notifications/NotificationChainLinks.tsx` — HO-NTF-002 §11.5. The chain of one
 * request: the notification this one is a fallback for, the fallbacks it triggered, the message it
 * is a resend of, and the resends made from it — each a link to that notification.
 */
export function NotificationChainLinks({ chain }: { chain: NotificationChain }) {
  const rows: Array<{ label: string; ids: string[] }> = [
    { label: 'Fallback for', ids: chain.fallbackFromId ? [chain.fallbackFromId] : [] },
    { label: 'Fallbacks sent', ids: chain.fallbacks },
    { label: 'Resend of', ids: chain.resendOfId ? [chain.resendOfId] : [] },
    { label: 'Resent as', ids: chain.resends },
  ].filter((row) => row.ids.length > 0);

  if (rows.length === 0) {
    return <p className="text-sm text-ink-muted">This message has no fallbacks or resends.</p>;
  }
  return (
    <dl className="m-0 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5 text-sm">
      {rows.map((row) => (
        <div key={row.label} className="contents">
          <dt className="font-semibold text-ink">{row.label}</dt>
          <dd className="m-0 flex flex-wrap gap-x-3">
            {row.ids.map((id, index) => (
              <ChainLink
                key={id}
                id={id}
                label={row.ids.length > 1 ? `Message ${index + 1}` : 'View message'}
              />
            ))}
          </dd>
        </div>
      ))}
    </dl>
  );
}
