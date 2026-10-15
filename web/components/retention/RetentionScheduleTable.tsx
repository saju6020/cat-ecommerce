import Link from 'next/link';
import { Eye, Trash2, EyeOff, UserX, Archive } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import type { RetentionActionAtEnd, RetentionSchedule } from '@/lib/api/retention';
import { ACTION_EXPLANATION, ACTION_LABEL, formatPeriod, recordClassInfo } from '@/lib/retention/labels';

const ACTION_ICON: Record<RetentionActionAtEnd, React.ReactNode> = {
  ReviewOnly: <Eye size={12} aria-hidden="true" />,
  Purge: <Trash2 size={12} aria-hidden="true" />,
  Redact: <EyeOff size={12} aria-hidden="true" />,
  AnonymiseOnRequest: <UserX size={12} aria-hidden="true" />,
  Keep: <Archive size={12} aria-hidden="true" />,
};

/**
 * `components/retention/RetentionScheduleTable.tsx` — HO-XCT-002 §11.5, U-B1. One row per record
 * class: what it covers, the period in force (from the settings, with a link to change it), and
 * what happens at the end (text plus icon). Clinical, financial, regulatory and audit classes read
 * "Review only": the screen says outright that nothing there is deleted automatically.
 */
export function RetentionScheduleTable({ schedule }: { schedule: RetentionSchedule }) {
  return (
    <div
      role="region"
      aria-label="Retention schedule (scrolls horizontally)"
      tabIndex={0}
      className="w-full overflow-x-auto rounded-[var(--radius-md)] border border-border"
    >
      <table className="w-full border-collapse bg-surface text-sm">
        <caption className="sr-only">Retention schedule by record class</caption>
        <thead>
          <tr className="border-b border-border bg-surface-sunken text-left text-ink-muted">
            <th scope="col" className="px-3 py-2">Record class</th>
            <th scope="col" className="px-3 py-2">Retention period</th>
            <th scope="col" className="px-3 py-2">At the end of the period</th>
            <th scope="col" className="px-3 py-2">Tables</th>
          </tr>
        </thead>
        <tbody>
          {schedule.classes.map((row) => {
            const info = recordClassInfo(row.recordClass);
            return (
              <tr key={row.recordClass} className="border-b border-border align-top last:border-0">
                <th scope="row" className="px-3 py-3 text-left font-normal">
                  <span className="font-semibold text-ink">{info.title}</span>
                  <span className="block text-ink-muted">{info.covers}</span>
                </th>
                <td className="px-3 py-3 text-ink">
                  {formatPeriod(row.effectivePeriod, row.periodUnit)}
                  {row.effectivePeriod !== null && (
                    <Link
                      href="/staff/admin/settings"
                      className="ml-2 text-accent-ink hover:underline"
                    >
                      Change in settings
                    </Link>
                  )}
                </td>
                <td className="px-3 py-3">
                  <Badge
                    variant={row.actionAtEnd === 'ReviewOnly' ? 'success' : 'neutral'}
                    icon={ACTION_ICON[row.actionAtEnd]}
                  >
                    {ACTION_LABEL[row.actionAtEnd]}
                  </Badge>
                  <span className="mt-1 block text-ink-muted">{ACTION_EXPLANATION[row.actionAtEnd]}</span>
                </td>
                <td className="px-3 py-3 text-ink-muted">
                  {row.tableCount}
                  {row.tables.length > 0 && (
                    <details className="mt-1">
                      <summary className="cursor-pointer text-accent-ink">Show tables</summary>
                      <ul className="mt-1 list-disc pl-5">
                        {row.tables.map((t) => (
                          <li key={`${t.schema}.${t.table}`}>
                            {t.schema}.{t.table}
                          </li>
                        ))}
                      </ul>
                    </details>
                  )}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
