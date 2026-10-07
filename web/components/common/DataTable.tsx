'use client';

import * as React from 'react';
import Link from 'next/link';
import { useRouter, usePathname, useSearchParams } from 'next/navigation';
import { ChevronDown, ChevronUp, ChevronsUpDown, Search } from 'lucide-react';
import {
  TableBody,
  TableCaption,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Button } from '@/components/ui/button';
import { LoadingState } from '@/components/common/LoadingState';
import { EmptyState, type EmptyStateProps } from '@/components/common/EmptyState';
import { ErrorState } from '@/components/common/ErrorState';
import { cn } from '@/lib/utils';

/**
 * `components/common/DataTable.tsx` — HO-CAT-002 §11.7, UI-4: "Filter, sort, paginate.
 * URL-driven (§8.4). One implementation, used by every list." (Reused explicitly by HO-IMP-002
 * §5/§13.)
 *
 * DataTable owns exactly the state that is generic across every area's lists — a free-text
 * search box, one sortable-column sort, and pagination — and writes all three to the URL via
 * `useSearchParams` (component test U29), so a filtered/sorted/paged list is linkable and
 * survives reload (NFR-USA-5, E22). **Domain-specific filters** (species, status, category, …)
 * are not generic enough to live here: render them above `<DataTable>` as your own filter form
 * and drive your own `useSearchParams` the same way — see HO-CAT-002 §8.4's query-key
 * convention (`['cats', filters]`) for how the resulting params should flow into your `lib/hooks`
 * query.
 *
 * DataTable is presentational plus URL-writing only — it never fetches. The caller's `lib/hooks`
 * hook reads the same `useSearchParams` values and supplies `rows`, `totalCount`, `isLoading` and
 * `isError`.
 *
 * Next.js App Router note: a component that calls `useSearchParams` opts the page it's rendered
 * on out of static rendering unless wrapped in `<Suspense>` — wrap the page/route that renders a
 * `<DataTable>` if it would otherwise be statically generated.
 */
export interface DataTableColumn<TRow> {
  id: string;
  header: string;
  cell: (row: TRow) => React.ReactNode;
  sortable?: boolean;
  headerClassName?: string;
  cellClassName?: string;
}

/**
 * A discriminated union, not two independently-optional fields (ARCHITECTURE gate finding on
 * HO-CAT-002) — exactly one of `totalCount`/`hasNextPage` must be supplied, and TypeScript now
 * enforces that at every call site instead of only in a comment.
 *
 *  - `totalCount`: total row count across all pages (server-reported), not `rows.length`.
 *    Supply this when the endpoint behind the list reports a real total.
 *  - `hasNextPage`: for an endpoint that doesn't report a total — added for HO-CAT-002's cat/
 *    vaccine/weight lists, whose HO-CAT-001 endpoints return a bare array with no total-count
 *    header (a recorded finding against that contract, not invented here; see the doc comment
 *    on `PageOf<T>` in `lib/api/cats.ts`). The pager shows "Page N" and only enables Next while
 *    another page is known to exist — it never claims a total it wasn't told.
 */
export type DataTablePagination =
  | { totalCount: number; hasNextPage?: never; pageSize: number; paramName?: string }
  | { totalCount?: never; hasNextPage: boolean; pageSize: number; paramName?: string };

export interface DataTableProps<TRow> {
  /** The table's accessible name (rendered as a visually-hidden `<caption>`). */
  caption: string;
  columns: Array<DataTableColumn<TRow>>;
  rows: TRow[] | undefined;
  getRowId: (row: TRow) => string;
  isLoading?: boolean;
  isError?: boolean;
  onRetry?: () => void;
  errorTitle?: string;
  errorDescription?: string;
  emptyState: Omit<EmptyStateProps, 'className'>;
  /** Renders a free-text search box wired to a URL param. Omit for a table with no quick search
   *  (e.g. one already scoped by a required domain filter above it). */
  search?: {
    label: string;
    placeholder?: string;
    /** URL search-param name. Default `'q'`. */
    paramName?: string;
  };
  pagination?: DataTablePagination;
  /** URL search-param names for the active sort. Defaults `'sort'` / `'dir'`. */
  sortParamNames?: { column?: string; direction?: string };
  /** Makes each row a link (the product-list pattern — the row's primary cell links out, but the
   *  whole row is still keyboard-reachable as one stop, not one link per cell). */
  rowHref?: (row: TRow) => string;
  className?: string;
}

function useUrlParam(paramName: string, defaultValue: string | undefined = undefined) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const value = searchParams.get(paramName) ?? defaultValue;

  const setValue = React.useCallback(
    (next: string | undefined, resetParams: string[] = []) => {
      const params = new URLSearchParams(searchParams.toString());
      if (next === undefined || next === '') params.delete(paramName);
      else params.set(paramName, next);
      resetParams.forEach((p) => params.delete(p));
      const query = params.toString();
      router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false });
    },
    [paramName, pathname, router, searchParams],
  );

  return [value, setValue] as const;
}

export function DataTable<TRow>({
  caption,
  columns,
  rows,
  getRowId,
  isLoading,
  isError,
  onRetry,
  errorTitle,
  errorDescription,
  emptyState,
  search,
  pagination,
  sortParamNames,
  rowHref,
  className,
}: DataTableProps<TRow>) {
  const sortColumnParam = sortParamNames?.column ?? 'sort';
  const sortDirectionParam = sortParamNames?.direction ?? 'dir';
  const pageParam = pagination?.paramName ?? 'page';
  const searchParam = search?.paramName ?? 'q';

  const [sortColumn, setSortColumn] = useUrlParam(sortColumnParam);
  const [sortDirection, setSortDirection] = useUrlParam(sortDirectionParam, 'asc');
  const [pageValue, setPageValue] = useUrlParam(pageParam, '1');
  const [searchValue, setSearchValue] = useUrlParam(searchParam);
  const [searchDraft, setSearchDraft] = React.useState(searchValue ?? '');

  React.useEffect(() => {
    setSearchDraft(searchValue ?? '');
  }, [searchValue]);

  const page = Math.max(1, Number(pageValue) || 1);
  const knownTotalCount = pagination?.totalCount;
  const pageCount =
    pagination && knownTotalCount !== undefined
      ? Math.max(1, Math.ceil(knownTotalCount / pagination.pageSize))
      : 1;
  const hasNextPage =
    knownTotalCount !== undefined ? page < pageCount : (pagination?.hasNextPage ?? false);

  function handleSort(columnId: string) {
    if (sortColumn === columnId) {
      setSortDirection(sortDirection === 'asc' ? 'desc' : 'asc');
    } else {
      setSortColumn(columnId);
      setSortDirection('asc');
    }
    if (pagination) setPageValue('1');
  }

  function handleSearchSubmit(event: React.FormEvent) {
    event.preventDefault();
    setSearchValue(searchDraft || undefined, pagination ? [pageParam] : []);
  }

  return (
    <div className={cn('flex flex-col gap-3', className)}>
      {search && (
        <form
          role="search"
          aria-label={search.label}
          onSubmit={handleSearchSubmit}
          className="flex gap-2"
        >
          <label htmlFor={`${searchParam}-input`} className="sr-only">
            {search.label}
          </label>
          <input
            id={`${searchParam}-input`}
            type="search"
            value={searchDraft}
            placeholder={search.placeholder}
            onChange={(event) => setSearchDraft(event.target.value)}
            className="w-full max-w-xs rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          />
          <Button type="submit" variant="secondary" size="sm">
            <Search size={14} aria-hidden="true" />
            Search
          </Button>
        </form>
      )}

      {isLoading ? (
        <LoadingState
          variant="table"
          rows={pagination?.pageSize ? Math.min(pagination.pageSize, 8) : 5}
        />
      ) : isError ? (
        <ErrorState title={errorTitle} description={errorDescription} onRetry={onRetry} />
      ) : !rows || rows.length === 0 ? (
        <EmptyState {...emptyState} />
      ) : (
        <>
          {/* A wide table doesn't fit UI-1's 360px floor, and plain <table>s don't wrap — an
              `overflow-x-auto` region scrolls the TABLE, not the page, at narrow widths. `tabIndex=0`
              + `role="region"` + `aria-label` make that scroll container itself keyboard-reachable
              (axe's `scrollable-region-focusable`, WCAG 2.1.1/2.1.3) — without them, a
              keyboard-only user has no way to reach content past the visible edge; a mouse/touch
              user could scroll it, but the exact same content is then keyboard-unreachable.
              This renders a bare `<table>` (the same classes `components/ui/table.tsx`'s `Table`
              would apply) rather than that component itself — `Table` bakes in its OWN
              `overflow-x-auto` wrapper with no accessible name and no way to opt out of it, so
              using it here would nest a second, redundant `overflow-x-auto` div around this one.
              Only the INNER of two nested `overflow-x-auto` boxes is ever the one that actually
              clips/scrolls the (wider) table (the outer one just shrinks to the inner one's
              already-resolved width and never itself overflows) — so a `tabIndex`/`role` on an
              outer wrapper alone doesn't satisfy the axe rule; the real scrollable box needs it
              directly, which means not going through `Table`'s own opaque wrapper at all here. */}
          <div
            role="region"
            aria-label={`${caption} (scrolls horizontally)`}
            tabIndex={0}
            className="w-full overflow-x-auto rounded-[var(--radius-md)] border border-border"
          >
            <table className="w-full border-collapse bg-surface text-sm">
              <TableCaption>{caption}</TableCaption>
              <TableHeader>
                <TableRow>
                  {columns.map((column) => (
                    <TableHead key={column.id} className={column.headerClassName}>
                      {column.sortable ? (
                        <button
                          type="button"
                          onClick={() => handleSort(column.id)}
                          className="inline-flex items-center gap-1 text-inherit hover:text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
                          aria-label={`Sort by ${column.header}`}
                        >
                          {column.header}
                          {sortColumn === column.id ? (
                            sortDirection === 'asc' ? (
                              <ChevronUp size={12} aria-hidden="true" />
                            ) : (
                              <ChevronDown size={12} aria-hidden="true" />
                            )
                          ) : (
                            <ChevronsUpDown size={12} aria-hidden="true" className="opacity-50" />
                          )}
                        </button>
                      ) : (
                        column.header
                      )}
                    </TableHead>
                  ))}
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((row) => {
                  const href = rowHref?.(row);
                  return (
                    <TableRow key={getRowId(row)}>
                      {columns.map((column, index) => (
                        <TableCell key={column.id} className={column.cellClassName}>
                          {href && index === 0 ? (
                            <Link
                              href={href}
                              className="font-semibold text-ink no-underline hover:underline"
                            >
                              {column.cell(row)}
                            </Link>
                          ) : (
                            column.cell(row)
                          )}
                        </TableCell>
                      ))}
                    </TableRow>
                  );
                })}
              </TableBody>
            </table>
          </div>

          {pagination && (
            <nav
              aria-label="Pages"
              className="flex flex-wrap items-center justify-between gap-2 text-sm text-ink-muted"
            >
              <span>
                {knownTotalCount !== undefined
                  ? `Page ${page} of ${pageCount} · ${knownTotalCount} total`
                  : `Page ${page}`}
              </span>
              <div className="flex gap-2">
                <Button
                  variant="secondary"
                  size="sm"
                  disabled={page <= 1}
                  onClick={() => setPageValue(String(page - 1))}
                >
                  Previous
                </Button>
                <Button
                  variant="secondary"
                  size="sm"
                  disabled={!hasNextPage}
                  onClick={() => setPageValue(String(page + 1))}
                >
                  Next
                </Button>
              </div>
            </nav>
          )}
        </>
      )}
    </div>
  );
}
