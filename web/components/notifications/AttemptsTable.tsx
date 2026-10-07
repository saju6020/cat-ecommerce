import { formatInZone } from '@/lib/format/time';
import type { NotificationAttempt } from '@/lib/api/notifications-log';

/**
 * `components/notifications/AttemptsTable.tsx` — HO-NTF-002 §11.5, US-NTF-06 AC1 ("see every
 * attempt"): time, outcome, provider, provider message id and the provider's response, all as plain
 * text (a provider response can contain anything — ADR-FE-162). Times are in the branch zone.
 */
export function AttemptsTable({
  attempts,
  timeZoneId,
}: {
  attempts: NotificationAttempt[];
  timeZoneId: string;
}) {
  if (attempts.length === 0) {
    return <p className="text-sm text-ink-muted">No delivery attempts have been made yet.</p>;
  }
  return (
    <div
      role="region"
      aria-label="Delivery attempts (scrolls horizontally)"
      tabIndex={0}
      className="w-full overflow-x-auto rounded-[var(--radius-md)] border border-border"
    >
      <table className="w-full border-collapse bg-surface text-sm">
        <caption className="sr-only">Delivery attempts</caption>
        <thead>
          <tr className="text-left text-xs font-semibold text-ink-muted">
            {['Time', 'Outcome', 'Provider', 'Provider message id', 'Provider response'].map(
              (heading) => (
                <th key={heading} scope="col" className="border-b border-border p-2">
                  {heading}
                </th>
              ),
            )}
          </tr>
        </thead>
        <tbody>
          {attempts.map((attempt, index) => (
            <tr key={`${attempt.attemptedOn}-${index}`} className="align-top">
              <td className="border-b border-border p-2">
                {formatInZone(attempt.attemptedOn, timeZoneId)}
              </td>
              <td className="border-b border-border p-2 font-semibold">{attempt.outcome}</td>
              <td className="border-b border-border p-2">{attempt.providerName}</td>
              <td className="break-all border-b border-border p-2">
                {attempt.providerMessageId ?? '—'}
              </td>
              <td className="whitespace-pre-wrap break-words border-b border-border p-2">
                {attempt.providerResponse ?? '—'}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
