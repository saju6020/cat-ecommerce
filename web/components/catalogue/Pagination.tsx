import Link from 'next/link';
import { cn } from '@/lib/utils';

export interface PaginationProps {
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  /** Builds the real link for a page (keeps every filter, sort and language). */
  hrefFor: (pageNumber: number) => string;
}

/** HO-SHP-002 §11.2 — pagination is real links, `aria-current="page"` on the current one. */
export function Pagination({ pageNumber, pageSize, totalCount, hrefFor }: PaginationProps) {
  const pageCount = Math.max(1, Math.ceil(totalCount / pageSize));
  if (pageCount <= 1) return null;

  const pages = new Set<number>([1, pageCount]);
  for (let p = pageNumber - 1; p <= pageNumber + 1; p += 1)
    if (p >= 1 && p <= pageCount) pages.add(p);
  const sorted = [...pages].sort((a, b) => a - b);

  const linkClass = 'rounded-[var(--radius-sm)] px-3 py-1.5 text-sm font-semibold no-underline';
  return (
    <nav aria-label="Pagination" className="flex flex-wrap items-center justify-center gap-1">
      {pageNumber > 1 && (
        <Link href={hrefFor(pageNumber - 1)} rel="prev" className={cn(linkClass, 'text-ink-muted')}>
          Previous
        </Link>
      )}
      {sorted.map((page, index) => {
        const previous = sorted[index - 1];
        return (
          <span key={page} className="flex items-center gap-1">
            {previous !== undefined && page - previous > 1 && (
              <span aria-hidden="true" className="text-ink-muted">
                …
              </span>
            )}
            <Link
              href={hrefFor(page)}
              aria-current={page === pageNumber ? 'page' : undefined}
              aria-label={`Page ${page}`}
              className={cn(
                linkClass,
                page === pageNumber
                  ? 'bg-accent-soft text-accent-ink'
                  : 'text-ink-muted hover:bg-surface-sunken',
              )}
            >
              {page}
            </Link>
          </span>
        );
      })}
      {pageNumber < pageCount && (
        <Link href={hrefFor(pageNumber + 1)} rel="next" className={cn(linkClass, 'text-ink-muted')}>
          Next
        </Link>
      )}
    </nav>
  );
}
