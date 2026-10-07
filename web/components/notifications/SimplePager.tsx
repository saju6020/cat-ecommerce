import { Button } from '@/components/ui/button';

/**
 * `components/notifications/SimplePager.tsx` — a small Previous / Next pager shared by the inbox and
 * the notification log (HO-NTF-002 §11.1, §11.5, UI-4). Presentational: the page owns the `page`
 * URL param and passes `onPageChange`. `DataTable`'s own pager is URL-wired internally and cannot be
 * reused here because the log renders a card list below 768 px instead of a table.
 */
export interface SimplePagerProps {
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
}

export function SimplePager({ pageNumber, pageSize, totalCount, onPageChange }: SimplePagerProps) {
  const pageCount = Math.max(1, Math.ceil(totalCount / pageSize));
  if (pageCount <= 1) return null;
  return (
    <nav
      aria-label="Pages"
      className="flex flex-wrap items-center justify-between gap-2 text-sm text-ink-muted"
    >
      <span>
        Page {pageNumber} of {pageCount} · {totalCount} total
      </span>
      <div className="flex gap-2">
        <Button
          variant="secondary"
          size="sm"
          disabled={pageNumber <= 1}
          onClick={() => onPageChange(pageNumber - 1)}
        >
          Previous
        </Button>
        <Button
          variant="secondary"
          size="sm"
          disabled={pageNumber >= pageCount}
          onClick={() => onPageChange(pageNumber + 1)}
        >
          Next
        </Button>
      </div>
    </nav>
  );
}
