import type { RetentionReview } from '@/lib/api/retention';
import { formatDateOnly } from '@/lib/format/time';
import { recordClassInfo } from '@/lib/retention/labels';

/**
 * `components/retention/ReviewEligiblePanel.tsx` — HO-XCT-002 §11.5. Counts per class of records
 * past their statutory period, always with the sentence "Eligible for review. Nothing is deleted
 * automatically." (US-XCT-06 AC2). With no rows it says so, rather than showing nothing.
 */
export function ReviewEligiblePanel({ review }: { review: RetentionReview }) {
  return (
    <section
      aria-labelledby="review-eligible-title"
      className="flex flex-col gap-2 rounded-[var(--radius-md)] border border-border bg-surface p-4"
    >
      <h2 id="review-eligible-title" className="text-[15px] font-semibold text-ink">
        Records eligible for review
      </h2>
      <p className="text-sm text-ink">Eligible for review. Nothing is deleted automatically.</p>
      {review.rows.length === 0 ? (
        <p className="text-sm text-ink-muted">
          No records are past their retention period as of {formatDateOnly(review.asOf)}.
        </p>
      ) : (
        <ul className="flex flex-col gap-1 text-sm">
          {review.rows.map((row) => (
            <li key={`${row.sourceKey}-${row.recordKind}`} className="flex flex-wrap gap-x-3">
              <span className="font-semibold text-ink">
                {row.count} {row.recordKind.toLowerCase()}
              </span>
              <span className="text-ink-muted">
                {recordClassInfo(row.recordClass).title}
                {row.oldestAnchor ? ` · oldest from ${formatDateOnly(row.oldestAnchor)}` : ''}
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
