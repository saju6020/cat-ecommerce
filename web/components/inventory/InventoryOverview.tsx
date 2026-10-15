'use client';

import * as React from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { AlertTriangle, Clock, ShieldAlert } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Panel, TextField } from './fields';
import { ALERT_KIND_OPTIONS } from './labels';
import { formatMoney } from '@/lib/format/money';
import type { AlertSummary } from '@/lib/api/inventory-alerts';
import type { NearExpiryReport } from '@/lib/api/inventory-reports';

function Tile({ icon, title, href, linkLabel, children }: { icon: React.ReactNode; title: string; href: string; linkLabel: string; children: React.ReactNode }) {
  return (
    <section aria-label={title} className="flex min-w-0 flex-col gap-2 rounded-[var(--radius-md)] border border-border bg-surface p-4">
      <h2 className="flex items-center gap-2 text-sm font-bold uppercase tracking-wide text-ink-muted">
        {icon}
        {title}
      </h2>
      <div className="text-sm text-ink">{children}</div>
      <Link href={href} className="mt-auto text-sm font-semibold text-accent-ink">
        {linkLabel}
      </Link>
    </section>
  );
}

/**
 * `components/inventory/InventoryOverview.tsx` — HO-IMP-002 §8.1. The landing page of Inventory:
 * the open alerts, a near-expiry teaser and the active recalls — each only when the server let the
 * caller read it (a `403` simply leaves its tile out) — over a search that opens the stock list.
 */
export function InventoryOverview({
  alerts,
  nearExpiry,
  activeRecalls,
}: {
  alerts: AlertSummary | undefined;
  nearExpiry: NearExpiryReport | undefined;
  activeRecalls: number | undefined;
}) {
  const router = useRouter();
  const [term, setTerm] = React.useState('');
  const kindLabel = (kind: string) => ALERT_KIND_OPTIONS.find((o) => o.value === kind)?.label ?? kind;
  return (
    <div className="flex flex-col gap-5">
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {alerts && (
          <Tile icon={<AlertTriangle size={14} aria-hidden="true" />} title="Alerts" href="/staff/inventory/alerts" linkLabel="See all alerts">
            <p className="font-semibold">{alerts.open.total} open</p>
            <ul className="mt-1 text-ink-muted">
              {Object.entries(alerts.open.byKind).map(([kind, count]) => (
                <li key={kind}>
                  {count} · {kindLabel(kind).toLowerCase()}
                </li>
              ))}
            </ul>
          </Tile>
        )}
        {nearExpiry && (
          <Tile icon={<Clock size={14} aria-hidden="true" />} title="Near expiry" href="/staff/inventory/reports/near-expiry" linkLabel="Open the near-expiry report">
            <p className="font-semibold">
              {nearExpiry.rows.length} {nearExpiry.rows.length === 1 ? 'batch' : 'batches'} within {nearExpiry.withinDays} days
            </p>
            <p className="text-ink-muted">Value at risk {formatMoney(nearExpiry.totalValueAtRisk)}</p>
          </Tile>
        )}
        {activeRecalls !== undefined && (
          <Tile icon={<ShieldAlert size={14} aria-hidden="true" />} title="Recalls" href="/staff/inventory/recalls" linkLabel="Open the recall console">
            <p className="font-semibold">{activeRecalls} active</p>
            <p className="text-ink-muted">Recalled stock is held everywhere and cannot be used.</p>
          </Tile>
        )}
      </div>
      <Panel title="Find stock">
        <form
          role="search"
          onSubmit={(event) => {
            event.preventDefault();
            router.push(`/staff/inventory/stock${term.trim() ? `?search=${encodeURIComponent(term.trim())}` : ''}`);
          }}
          className="flex flex-wrap items-end gap-2"
        >
          <TextField id="overview-search" label="Search by name or code" value={term} onChange={(e) => setTerm(e.target.value)} wrapperClassName="min-w-56 flex-1" />
          <Button type="submit">Search stock</Button>
        </form>
      </Panel>
    </div>
  );
}
