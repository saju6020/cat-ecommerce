import { Badge } from '@/components/ui/badge';
import { DataTable } from '@/components/common/DataTable';
import { CHANNEL_LABELS, sortChannels } from '@/lib/validation/templateContent';
import { formatInZone } from '@/lib/format/time';
import type { NotificationTemplateSummary } from '@/lib/api/admin-notification-templates';

/**
 * `components/templates/TemplateTable.tsx` — HO-NTF-002 §11.3, US-NTF-02. Name, key, category
 * (*Transactional* / *Marketing*), channels, *Customised* or *Default* (text, never colour alone)
 * and the last edit. The name cell links to the editor. Loading, empty and error states come from
 * `DataTable`.
 */
export function TemplateTable({
  templates,
  isLoading,
  isError,
  onRetry,
  timeZoneId,
}: {
  templates: NotificationTemplateSummary[] | undefined;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  timeZoneId: string;
}) {
  return (
    <DataTable
      caption="Message templates"
      rows={templates}
      getRowId={(row) => row.key}
      isLoading={isLoading}
      isError={isError}
      onRetry={onRetry}
      emptyState={{
        title: 'No message templates',
        description: 'Templates appear here once a feature that sends messages is installed.',
      }}
      rowHref={(row) => `/staff/admin/notification-templates/${encodeURIComponent(row.key)}`}
      columns={[
        { id: 'name', header: 'Name', cell: (row) => row.displayName },
        {
          id: 'key',
          header: 'Key',
          cell: (row) => <span className="font-mono text-xs text-ink-muted">{row.key}</span>,
        },
        { id: 'category', header: 'Category', cell: (row) => row.category },
        {
          id: 'channels',
          header: 'Channels',
          cell: (row) =>
            sortChannels(row.channels)
              .map((c) => CHANNEL_LABELS[c])
              .join(', '),
        },
        {
          id: 'status',
          header: 'Wording',
          cell: (row) => (
            <Badge variant={row.isCustomised ? 'success' : 'neutral'}>
              {row.isCustomised ? 'Customised' : 'Default'}
            </Badge>
          ),
        },
        {
          id: 'edited',
          header: 'Last edited',
          cell: (row) => (row.lastEditedOn ? formatInZone(row.lastEditedOn, timeZoneId) : '—'),
        },
      ]}
    />
  );
}
