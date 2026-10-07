import Link from 'next/link';
import { Badge } from '@/components/ui/badge';
import { buttonVariants } from '@/components/ui/button';
import { DataTable } from '@/components/common/DataTable';
import { formatInstantDate } from '@/lib/format/time';
import type { ContentItem } from '@/lib/api/admin-content';

const STATUS_VARIANT = { Draft: 'neutral', Published: 'success', Unpublished: 'neutral' } as const;

/** `components/content/ContentTable.tsx` — HO-ADM-002 §11.6, US-ADM-07. Kind, title, slug,
 *  status, last published; filter by kind/status is the page's own URL-param state. */
export interface ContentTableProps {
  items: ContentItem[] | undefined;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}

export function ContentTable({ items, isLoading, isError, onRetry }: ContentTableProps) {
  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between gap-3">
        <h1 className="font-display text-xl font-semibold text-ink">Content</h1>
        <Link href="/staff/admin/content/new" className={buttonVariants({ variant: 'primary' })}>
          New content
        </Link>
      </div>

      <DataTable
        caption="Content"
        rows={items}
        getRowId={(row) => row.id}
        isLoading={isLoading}
        isError={isError}
        onRetry={onRetry}
        emptyState={{ title: 'No content yet' }}
        rowHref={(row) => `/staff/admin/content/${row.id}`}
        columns={[
          { id: 'kind', header: 'Kind', cell: (row) => row.kind },
          { id: 'title', header: 'Title', cell: (row) => row.title },
          { id: 'slug', header: 'Slug', cell: (row) => row.slug },
          {
            id: 'status',
            header: 'Status',
            cell: (row) => <Badge variant={STATUS_VARIANT[row.status]}>{row.status}</Badge>,
          },
          {
            id: 'publishedOn',
            header: 'Last published',
            cell: (row) => (row.publishedOn ? formatInstantDate(row.publishedOn) : '—'),
          },
        ]}
      />
    </div>
  );
}
