import { LimitedMarkup } from './LimitedMarkup';
import { EmptyState } from '@/components/common/EmptyState';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import type { PublishedFaq } from '@/lib/api/public-content';

/** `components/content/FaqList.tsx` — HO-ADM-002 §11.6, US-ADM-07. Published FAQs as a disclosure
 *  list (native `<details>`/`<summary>` — accessible, no JS needed to expand). */
export interface FaqListProps {
  faqs: PublishedFaq[] | undefined;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}

export function FaqList({ faqs, isLoading, isError, onRetry }: FaqListProps) {
  if (isLoading) return <LoadingState rows={4} />;
  if (isError) return <ErrorState onRetry={onRetry} />;
  if (!faqs || faqs.length === 0) return <EmptyState title="No FAQs published yet" />;

  return (
    <div className="flex flex-col gap-3">
      {faqs.map((faq) => (
        <details key={faq.slug} className="rounded-[var(--radius-md)] border border-border p-4">
          <summary className="cursor-pointer text-[15px] font-semibold text-ink">
            {faq.title}
          </summary>
          <LimitedMarkup body={faq.body} className="mt-3 text-sm text-ink" />
        </details>
      ))}
    </div>
  );
}
