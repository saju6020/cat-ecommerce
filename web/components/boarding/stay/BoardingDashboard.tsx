'use client';

import { useEffect, useRef, useState, type ReactNode } from 'react';
import Link from 'next/link';
import { Phone } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { RecordCallDialog } from '@/components/boarding/incidents/RecordCallDialog';
import { QueryGate } from '@/components/boarding/shared/QueryGate';
import { DepositStateBadge, SeverityBadge } from '@/components/boarding/shared/StatusBadges';
import {
  ATTENTION_LABEL,
  INCIDENT_KIND_LABEL,
  PORTION_LABEL,
  UNIT_TYPE_LABEL,
} from '@/components/boarding/shared/labels';
import type {
  BoardingDashboard as Dashboard,
  DashboardStayRow,
  DashboardTaskRow,
} from '@/lib/api/types';
import { formatBranchInstant, formatOverdue, formatUpdatedAt } from '@/lib/format/boarding';
import { formatMoney } from '@/lib/format/money';
import { useBoardingDashboard } from '@/lib/hooks/useStaffBoarding';
import {
  STAFF_BOARDING_INCIDENTS,
  STAFF_BOARDING_TASKS,
  staffStayHref,
} from '@/lib/routing/boarding-links';

function Card({
  title,
  count,
  href,
  linkLabel,
  empty,
  children,
}: {
  title: string;
  count: number;
  href?: string;
  linkLabel?: string;
  empty: string;
  children: ReactNode;
}) {
  const id = `dash-${title.replace(/\W+/g, '-').toLowerCase()}`;
  return (
    <section
      aria-labelledby={id}
      className="rounded-[var(--radius-md)] border border-border bg-surface p-4"
    >
      <div className="mb-2 flex items-center justify-between gap-2">
        <h2 id={id} className="text-xs font-bold uppercase tracking-wide text-ink-muted">
          {title} ({count})
        </h2>
        {href && (
          <Link href={href} className="text-xs font-semibold text-accent-ink underline">
            {linkLabel ?? 'Open'}
          </Link>
        )}
      </div>
      {count === 0 ? <p className="text-sm text-ink-muted">{empty}</p> : children}
    </section>
  );
}

function StayRows({ rows, action }: { rows: DashboardStayRow[]; action: string }) {
  return (
    <ul className="flex flex-col gap-2 text-sm">
      {rows.map((r) => (
        <li key={r.stayId} className="flex flex-wrap items-center justify-between gap-2">
          <span className="text-ink">
            <strong>{r.catNames.join(' and ')}</strong>
            {r.anyHighRisk ? ' (high risk)' : ''} · {r.unitName ?? 'No unit'}
          </span>
          <span className="flex items-center gap-2">
            {r.depositState && <DepositStateBadge state={r.depositState} stayStatus={r.status} />}
            <Link
              href={staffStayHref(r.stayId)}
              className="font-semibold text-accent-ink underline"
            >
              {action}
            </Link>
          </span>
        </li>
      ))}
    </ul>
  );
}

function TaskRows({ rows }: { rows: DashboardTaskRow[] }) {
  const byUnit = new Map<string, DashboardTaskRow[]>();
  for (const r of rows) byUnit.set(r.unitName, [...(byUnit.get(r.unitName) ?? []), r]);
  return (
    <div className="flex flex-col gap-2 text-sm">
      {[...byUnit.entries()].map(([unit, list]) => (
        <div key={unit}>
          <p className="font-semibold text-ink">{unit}</p>
          <ul className="list-disc pl-5 text-ink">
            {list.map((t) => (
              <li key={t.taskId}>
                {t.dueLocalTime} {t.title} — {t.catName}
                {t.minutesLate !== null ? ` (${formatOverdue(t.minutesLate)})` : ''}
              </li>
            ))}
          </ul>
        </div>
      ))}
    </div>
  );
}

function Body({ data }: { data: Dashboard }) {
  const [calling, setCalling] = useState<Dashboard['callOwner'][number] | null>(null);
  const [announcement, setAnnouncement] = useState('');
  const seenHigh = useRef<Set<string> | null>(null);
  const zone = data.branchTimeZoneId;

  useEffect(() => {
    const high = new Set(
      data.openIncidents.filter((i) => i.severity === 'High').map((i) => i.incidentId),
    );
    if (seenHigh.current) {
      const fresh = data.openIncidents.find(
        (i) => i.severity === 'High' && !seenHigh.current!.has(i.incidentId),
      );
      if (fresh)
        setAnnouncement(
          `New high-severity incident: ${fresh.kind ? INCIDENT_KIND_LABEL[fresh.kind] : 'Incident'} — ${fresh.catName}.`,
        );
    }
    seenHigh.current = high;
  }, [data.openIncidents]);

  const occ = data.occupancy;
  return (
    <div className="flex flex-col gap-4">
      <p role="status" aria-live="polite" className="sr-only">
        {announcement}
      </p>
      <p className="text-xs text-ink-muted">
        Updated {formatUpdatedAt(data.generatedOn, zone)} ({zone}) · refreshes every 30 seconds
      </p>
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        <Card title="Occupancy today" count={occ.unitsOccupied} empty="No unit is occupied today.">
          <p className="text-sm text-ink">
            <strong>{occ.unitsOccupied}</strong> of {occ.unitsActive} units occupied ·{' '}
            <strong>{occ.catsInHouse}</strong> cats in house
          </p>
          <ul className="mt-1 text-sm text-ink-muted">
            {occ.byType.map((t) => (
              <li key={t.unitType}>
                {UNIT_TYPE_LABEL[t.unitType]}: {t.occupied} of {t.active}
              </li>
            ))}
          </ul>
        </Card>
        <Card
          title="Arrivals"
          count={data.arrivals.length}
          href="/staff/boarding/stays"
          linkLabel="All stays"
          empty="No arrivals today."
        >
          <StayRows rows={data.arrivals} action="Check in" />
        </Card>
        <Card
          title="Departures"
          count={data.departures.length}
          href="/staff/boarding/stays"
          linkLabel="All stays"
          empty="No departures today."
        >
          <StayRows rows={data.departures} action="Check out" />
        </Card>
        <Card title="Cats not eating" count={data.notEating.length} empty="Every cat is eating.">
          <ul className="flex flex-col gap-1 text-sm">
            {data.notEating.map((c) => (
              <li key={c.catId}>
                <Link
                  href={staffStayHref(c.stayId, 'care')}
                  className="font-semibold text-accent-ink underline"
                >
                  {c.catName}
                </Link>{' '}
                · {c.unitName} · last two:{' '}
                {c.lastEntries.map((e) => PORTION_LABEL[e.portionEaten]).join(', ')}
              </li>
            ))}
          </ul>
        </Card>
        <Card
          title="Overdue tasks"
          count={data.overdueTasks.length}
          href={STAFF_BOARDING_TASKS}
          linkLabel="Task board"
          empty="Nothing is overdue."
        >
          <TaskRows rows={data.overdueTasks} />
        </Card>
        <Card
          title="Unassigned tasks"
          count={data.unassignedTasks.length}
          href={STAFF_BOARDING_TASKS}
          linkLabel="Task board"
          empty="Every task has someone on it."
        >
          <TaskRows rows={data.unassignedTasks} />
        </Card>
        <Card
          title="Open incidents"
          count={data.openIncidents.length}
          href={STAFF_BOARDING_INCIDENTS}
          linkLabel="Incident queue"
          empty="No open incidents."
        >
          <ul className="flex flex-col gap-1 text-sm">
            {data.openIncidents.map((i) => (
              <li key={i.incidentId} className="flex flex-wrap items-center gap-2">
                <SeverityBadge severity={i.severity} />
                {i.kind ? INCIDENT_KIND_LABEL[i.kind] : 'Incident'} — {i.catName}
                <span className="text-ink-muted">
                  {i.acknowledged ? 'seen by owner' : 'not acknowledged'}
                  {i.escalated ? ' · escalated' : ''}
                </span>
              </li>
            ))}
          </ul>
        </Card>
        <Card
          title="Call owner"
          count={data.callOwner.length}
          href={STAFF_BOARDING_INCIDENTS}
          linkLabel="Incident queue"
          empty="No owner needs a call."
        >
          <ul className="flex flex-col gap-2 text-sm">
            {data.callOwner.map((c) => (
              <li key={c.incidentId} className="flex flex-wrap items-center justify-between gap-2">
                <span>
                  {c.catName}
                  {c.customerName ? ` — ${c.customerName}` : ''}{' '}
                  <Link
                    href={staffStayHref(c.stayId)}
                    className="font-semibold text-accent-ink underline"
                  >
                    Open the stay for the number
                  </Link>
                  {c.customerPhone && (
                    <a href={`tel:${c.customerPhone}`} className="ml-1 underline">
                      <Phone size={12} className="inline" aria-hidden="true" /> {c.customerPhone}
                    </a>
                  )}
                </span>
                <Button size="sm" onClick={() => setCalling(c)}>
                  Record call
                </Button>
              </li>
            ))}
          </ul>
        </Card>
        <Card title="Unpaid balances" count={data.unpaidBalances.length} empty="Nothing is unpaid.">
          <ul className="flex flex-col gap-1 text-sm">
            {data.unpaidBalances.map((u) => (
              <li key={u.stayId}>
                <Link
                  href={staffStayHref(u.stayId)}
                  className="font-semibold text-accent-ink underline"
                >
                  {u.catNames.join(' and ')}
                </Link>{' '}
                {u.customerName ? ` · ${u.customerName}` : ''} · {formatMoney(u.amount)}{' '}
                {u.kind === 'DepositAwaiting'
                  ? `deposit${u.depositDueBy ? `, pay by ${formatBranchInstant(u.depositDueBy, zone)}` : ''}`
                  : 'balance'}
              </li>
            ))}
          </ul>
        </Card>
        <Card title="Refund due" count={data.refundDue.length} empty="No refunds due.">
          <ul className="text-sm">
            {data.refundDue.map((r) => (
              <li key={r.stayId}>
                {r.customerName ? `${r.customerName} · ` : ''}
                <Link
                  href={staffStayHref(r.stayId)}
                  className="font-semibold text-accent-ink underline"
                >
                  Stay
                </Link>{' '}
                · {formatMoney(r.amount)} — {r.reason}
              </li>
            ))}
          </ul>
        </Card>
        <Card
          title="Needs attention"
          count={data.attention.length}
          empty="Nothing needs attention."
        >
          <ul className="text-sm">
            {data.attention.map((a) => (
              <li key={a.stayId}>
                <Link
                  href={staffStayHref(a.stayId)}
                  className="font-semibold text-accent-ink underline"
                >
                  {a.catNames.join(' and ')}
                </Link>{' '}
                — {a.reason ? ATTENTION_LABEL[a.reason] : 'needs attention'}
              </li>
            ))}
          </ul>
        </Card>
      </div>
      {calling && (
        <RecordCallDialog
          open
          onOpenChange={(o) => !o && setCalling(null)}
          incidentId={calling.incidentId}
          customerName={calling.customerName ?? 'the owner'}
        />
      )}
    </div>
  );
}

/** `components/boarding/stay/BoardingDashboard.tsx` — ⭐ HO-BRD-002 §11.5, US-BRD-19: one card per
 *  section of the server's dashboard — each with its own empty state, its count in the heading and a
 *  link into the screen that deals with it. Polls every 30 s; an open **Record call** dialog keeps
 *  what was typed (ADR-FE-101). */
export function BoardingDashboard() {
  const query = useBoardingDashboard();
  return (
    <QueryGate query={query} loadingLabel="Loading the dashboard…" loadingRows={6}>
      {(data) => <Body data={data} />}
    </QueryGate>
  );
}
