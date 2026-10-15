'use client';

import { AlertTriangle, CheckCircle2, CircleDashed, ShieldAlert } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { ResponsiveTable, type Column } from '@/components/billing/ResponsiveTable';
import { timeOnlyInZone } from '@/lib/format/time';
import { describeChecklist } from '@/lib/format/surgery';
import type { SurgeryDayItem } from '@/lib/api/surgeries';
import { SurgeryStatusBadge } from './SurgeryStatusBadge';

/** The checklist as TEXT with an icon — *"Complete"* or *"3 outstanding: consent, weight, ASA grade"* (US-SRG-02 AC2). */
export function ChecklistText({ item }: { item: Pick<SurgeryDayItem, 'checklist'> }) {
  const complete = item.checklist.isComplete;
  const Icon = complete ? CheckCircle2 : CircleDashed;
  return (
    <span className={complete ? 'text-accent-ink' : 'text-warning-ink'}>
      <Icon size={14} className="mr-1 inline align-text-bottom" aria-hidden="true" />
      {describeChecklist(item.checklist)}
    </span>
  );
}

function CatCell({ item }: { item: SurgeryDayItem }) {
  return (
    <span className="flex flex-col items-start gap-1">
      <span>{item.cat.name}</span>
      {item.cat.isHighRisk && (
        <Badge variant="danger" icon={<ShieldAlert size={12} aria-hidden="true" />}>
          High risk
        </Badge>
      )}
      {item.cat.isDeceased && <Badge variant="neutral">Deceased</Badge>}
    </span>
  );
}

/**
 * ⭐ `SurgeryDayList` — HO-SRG-002 §11.2, US-SRG-02. The day's surgeries grouped by theatre: time, cat
 * (high-risk and deceased flags as text), procedure, surgeon and assistant, customer and phone, status,
 * the checklist and a *"Needs rescheduling"* badge. Cards from a phone width up to the table (UI-1).
 * Everything shown is what the server sent.
 */
export function SurgeryDayList({
  surgeries,
  zone,
  isLoading,
  isError,
  onRetry,
}: {
  surgeries: SurgeryDayItem[] | undefined;
  zone: string;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}) {
  const columns: Array<Column<SurgeryDayItem>> = [
    {
      id: 'time',
      header: 'Time',
      cell: (item) => (
        <span className="whitespace-nowrap font-semibold">
          {timeOnlyInZone(item.startsOn, zone)}–{timeOnlyInZone(item.endsOn, zone)}
        </span>
      ),
    },
    { id: 'cat', header: 'Cat', primary: true, cell: (item) => <CatCell item={item} /> },
    { id: 'procedure', header: 'Procedure', cell: (item) => item.procedure },
    {
      id: 'team',
      header: 'Surgeon',
      cell: (item) => (
        <>
          {item.surgeon.name}
          <span className="block text-xs text-ink-muted">with {item.assistant.name}</span>
        </>
      ),
    },
    {
      id: 'customer',
      header: 'Customer',
      cell: (item) => (
        <>
          {item.customer.name}
          {item.customer.phone && (
            <span className="block text-xs text-ink-muted">{item.customer.phone}</span>
          )}
        </>
      ),
    },
    {
      id: 'status',
      header: 'Status',
      cell: (item) => (
        <span className="flex flex-col items-start gap-1">
          <SurgeryStatusBadge status={item.status} />
          {item.needsRescheduling && (
            <Badge variant="warning" icon={<AlertTriangle size={12} aria-hidden="true" />}>
              Needs rescheduling
            </Badge>
          )}
        </span>
      ),
    },
    { id: 'checklist', header: 'Pre-op checklist', cell: (item) => <ChecklistText item={item} /> },
  ];

  const theatres = new Map<string, { name: string; items: SurgeryDayItem[] }>();
  for (const item of surgeries ?? []) {
    const group = theatres.get(item.theatre.id) ?? { name: item.theatre.name, items: [] };
    group.items.push(item);
    theatres.set(item.theatre.id, group);
  }

  if (isLoading || isError || !surgeries || surgeries.length === 0) {
    return (
      <ResponsiveTable
        caption="Surgeries"
        columns={columns}
        rows={surgeries}
        getRowId={(item) => item.surgeryId}
        isLoading={isLoading}
        isError={isError}
        onRetry={onRetry}
        emptyState={{
          title: 'No surgeries on this day.',
          description: 'Book a surgery, or choose another day.',
        }}
      />
    );
  }

  return (
    <div className="flex flex-col gap-6">
      {[...theatres.entries()].map(([theatreId, { name: theatre, items }]) => (
        <section key={theatreId} aria-label={theatre} className="flex flex-col gap-2">
          <h2 className="font-display text-base font-semibold text-ink">
            {theatre}{' '}
            <span className="font-sans text-sm font-normal text-ink-muted">
              {items.length} {items.length === 1 ? 'surgery' : 'surgeries'}
            </span>
          </h2>
          <ResponsiveTable
            caption={`${theatre} surgeries`}
            columns={columns}
            rows={items}
            getRowId={(item) => item.surgeryId}
            rowHref={(item) => `/staff/surgeries/${item.surgeryId}`}
            emptyState={{ title: 'No surgeries.' }}
          />
        </section>
      ))}
    </div>
  );
}
