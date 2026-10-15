'use client';

import * as React from 'react';
import Link from 'next/link';
import { AlertTriangle, CheckCircle2, Eye } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { DataTable } from '@/components/common/DataTable';
import { ReasonDialog } from './ReasonDialog';
import { describeAlert } from './labels';
import { formatInZone } from '@/lib/format/time';
import type { InventoryAlert } from '@/lib/api/inventory-alerts';

/**
 * `components/inventory/AlertList.tsx` — HO-IMP-002 §11.6, US-IMP-10 AC1 / US-IMP-12 AC1. Each alert
 * in plain words (*"Expires in 60 days or less"*, *"Below reorder point"*, *"Shortfall recorded"*,
 * *"Storage breach"*, *"Ledger mismatch"*), what it is about, when it was raised and its state in
 * WORDS plus an icon. **Acknowledge** takes an optional note. A resolved alert is greyed and shows its
 * resolution. (The server's alert carries no unit, so the quantity at raise is not shown — the item's
 * stock page has every figure with its unit.)
 */
export function AlertList({
  rows,
  totalCount,
  pageSize,
  isLoading,
  isError,
  onRetry,
  timeZoneId,
  onAcknowledge,
}: {
  rows: InventoryAlert[] | undefined;
  totalCount: number;
  pageSize: number;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  timeZoneId: string;
  onAcknowledge: (id: string, note: string | null) => Promise<unknown>;
}) {
  const [acknowledging, setAcknowledging] = React.useState<InventoryAlert | null>(null);

  return (
    <>
      <DataTable
        caption="Stock alerts"
        rows={rows}
        getRowId={(row) => row.id}
        isLoading={isLoading}
        isError={isError}
        onRetry={onRetry}
        pagination={{ totalCount, pageSize }}
        emptyState={{ title: 'No alerts', description: 'Expiry, low-stock and storage alerts appear here.' }}
        columns={[
          {
            id: 'what',
            header: 'Alert',
            cell: (a) => (
              <span className={a.status === 'Resolved' ? 'text-ink-muted' : undefined}>
                <strong>{describeAlert(a.kind, a.thresholdDays)}</strong>
                {a.status === 'Resolved' && a.resolutionNote && (
                  <span className="block text-xs font-normal">Resolved: {a.resolutionNote}</span>
                )}
              </span>
            ),
          },
          {
            id: 'about',
            header: 'About',
            cell: (a) => (
              <span className={a.status === 'Resolved' ? 'text-ink-muted' : undefined}>
                {a.skuId ? (
                  <Link href={`/staff/inventory/stock/${a.skuId}`} className="text-accent-ink">
                    {a.displayName ?? a.skuCode}
                  </Link>
                ) : (
                  '—'
                )}
                {a.batchId && (
                  <>
                    {' · '}
                    <Link href={`/staff/inventory/batches/${a.batchId}`} className="text-accent-ink">
                      lot {a.lotNumber}
                    </Link>
                  </>
                )}
                {a.locationCode && ` · ${a.locationCode}`}
              </span>
            ),
          },
          { id: 'raised', header: 'Raised', cell: (a) => formatInZone(a.raisedOn, timeZoneId) },
          {
            id: 'status',
            header: 'Status',
            cell: (a) =>
              a.status === 'Open' ? (
                <Badge variant="warning" icon={<AlertTriangle size={12} aria-hidden="true" />}>Open</Badge>
              ) : a.status === 'Acknowledged' ? (
                <Badge variant="neutral" icon={<Eye size={12} aria-hidden="true" />}>Acknowledged</Badge>
              ) : (
                <Badge variant="neutral" icon={<CheckCircle2 size={12} aria-hidden="true" />}>Resolved</Badge>
              ),
          },
          {
            id: 'action',
            header: 'Action',
            cell: (a) =>
              a.status === 'Open' ? (
                <Button size="sm" variant="secondary" onClick={() => setAcknowledging(a)} aria-label={`Acknowledge: ${describeAlert(a.kind, a.thresholdDays)}`}>
                  Acknowledge
                </Button>
              ) : null,
          },
        ]}
      />
      {acknowledging && (
        <ReasonDialog
          open
          onOpenChange={(open) => !open && setAcknowledging(null)}
          title="Acknowledge this alert"
          consequence={`${describeAlert(acknowledging.kind, acknowledging.thresholdDays)}${acknowledging.displayName ? ` — ${acknowledging.displayName}` : ''}. Acknowledging tells your team someone has seen it; it stays on the list until the cause is resolved.`}
          reasonLabel="Note (optional)"
          required={false}
          confirmLabel="Acknowledge"
          onConfirm={(note) => onAcknowledge(acknowledging.id, note === '' ? null : note)}
        />
      )}
    </>
  );
}
