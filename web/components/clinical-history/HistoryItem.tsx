'use client';

import { TriangleAlert } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { FileThumb } from '@/components/files/FileThumb';
import {
  diagnosticKindLabel,
  diagnosticStatusLabel,
  summariseFlagsField,
} from '@/lib/format/clinical';
import { formatDateOnly, formatInZone } from '@/lib/format/time';
import type { HistoryItem as Item } from '@/lib/api/clinical-history';

/**
 * `HistoryItem` — HO-EMR-002 §11.4. One item of a section, rendered by its `kind` (visit, diagnosis, lab
 * result, photo, vaccination, parasite treatment, weight) — the small per-type components the handover
 * names, kept in one file because each is a few lines. Everything is plain text (ADR-FE-058); items
 * known only by a date (`photo`, `vaccination`, `parasite-treatment`, `weight`) show a date with no
 * zone, instants show the branch zone. A visit or lab order opens nested in the drawer, never a
 * navigation (ADR-FE-049).
 */
const DATE_ONLY_KINDS = new Set(['photo', 'vaccination', 'parasite-treatment', 'weight']);

export function HistoryItemRow({
  item,
  zone,
  onOpenVisit,
  onOpenOrder,
}: {
  item: Item;
  zone: string;
  onOpenVisit?: (visitId: string) => void;
  onOpenOrder?: (orderId: string) => void;
}) {
  const when = DATE_ONLY_KINDS.has(item.kind)
    ? formatDateOnly(item.occurredOn.slice(0, 10))
    : formatInZone(item.occurredOn, zone, { dateStyle: 'medium' });
  const f = item.fields;

  return (
    <li className="flex flex-col gap-1 border-b border-border py-3 last:border-0">
      <div className="flex flex-wrap items-baseline justify-between gap-x-3">
        <p className="text-sm font-semibold text-ink">
          {item.kind === 'lab-result' ? diagnosticKindLabel(item.title) : item.title}
        </p>
        <p className="text-xs text-ink-muted">{when}</p>
      </div>
      {item.kind === 'visit' && (
        <>
          {item.detail && <p className="text-sm text-ink">{item.detail}</p>}
          <p className="text-xs text-ink-muted">
            {f.status} · {f.vet}
            {f.addendumCount && f.addendumCount !== '0' ? ` · ${f.addendumCount} addenda` : ''}
          </p>
          {onOpenVisit && (
            <div>
              <Button
                variant="secondary"
                size="sm"
                onClick={() => onOpenVisit(item.id)}
                aria-label={`Open the ${item.title} visit from ${when}`}
              >
                Open visit
              </Button>
            </div>
          )}
        </>
      )}
      {item.kind === 'diagnosis' && (
        <p className="text-sm text-ink-muted">
          {f.code} · {f.certainty} · seen {f.occurrences} {f.occurrences === '1' ? 'time' : 'times'}
          , first {f.firstSeen ? formatInZone(f.firstSeen, zone, { dateStyle: 'medium' }) : ''}
        </p>
      )}
      {item.kind === 'lab-result' && (
        <>
          <p className="text-sm text-ink-muted">
            {diagnosticStatusLabel(item.detail ?? '')}
            {f.reportedOn ? ` · reported ${formatDateOnly(f.reportedOn)}` : ''}
            {summariseFlagsField(f.flags) ? ` · ${summariseFlagsField(f.flags)}` : ''}
          </p>
          {f.hasAbnormal === 'true' && (
            <p className="inline-flex items-center gap-1 text-xs font-semibold text-warning-ink">
              <TriangleAlert size={12} aria-hidden="true" /> Has abnormal results
            </p>
          )}
          {onOpenOrder && (
            <div>
              <Button
                variant="secondary"
                size="sm"
                onClick={() => onOpenOrder(item.id)}
                aria-label={`Open the ${diagnosticKindLabel(item.title)} order from ${when}`}
              >
                Open order
              </Button>
            </div>
          )}
        </>
      )}
      {item.kind === 'photo' && (
        <div className="flex items-start gap-3">
          {item.file && (
            <FileThumb
              file={item.file}
              alt={`Photo taken ${when}${item.detail ? `: ${item.detail}` : ''}`}
              width={96}
              height={96}
            />
          )}
          {item.detail && (
            <p className="whitespace-pre-wrap break-words text-sm text-ink">{item.detail}</p>
          )}
        </div>
      )}
      {item.kind === 'vaccination' && (
        <p className="text-sm text-ink-muted">
          {f.nextDueOn ? `Next due ${formatDateOnly(f.nextDueOn)}` : ''}
          {f.batch ? ` · batch ${f.batch}` : ''}
        </p>
      )}
      {item.kind === 'parasite-treatment' && (
        <p className="text-sm text-ink-muted">
          {item.detail}
          {f.nextDueOn ? ` · next due ${formatDateOnly(f.nextDueOn)}` : ''}
        </p>
      )}
    </li>
  );
}
