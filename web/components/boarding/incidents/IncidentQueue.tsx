'use client';

import { useEffect, useRef, useState } from 'react';
import Link from 'next/link';
import { Phone } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/common/EmptyState';
import { CatHeaderCompact } from '@/components/boarding/shared/CatHeaderCompact';
import { QueryGate } from '@/components/boarding/shared/QueryGate';
import { ReasonDialog } from '@/components/boarding/shared/ReasonDialog';
import { SeverityBadge } from '@/components/boarding/shared/StatusBadges';
import { INCIDENT_KIND_LABEL } from '@/components/boarding/shared/labels';
import type { IncidentDetail } from '@/lib/api/types';
import { formatBranchInstant } from '@/lib/format/boarding';
import {
  useIncidentQueue,
  useRecordIncidentAcknowledgement,
  useResolveIncident,
} from '@/lib/hooks/useBoardingCare';
import { staffStayHref } from '@/lib/routing/boarding-links';
import { RecordCallDialog } from './RecordCallDialog';

/** `CallOwnerCard` (HO-BRD-002 §11.9): *“No acknowledgement after 30 minutes — call {customer} on
 *  {phone}”* with a `tel:` link and **Record call**. Shown only when the server escalated it. */
function CallOwnerCard({ incident, onRecord }: { incident: IncidentDetail; onRecord: () => void }) {
  return (
    <div className="mt-3 flex flex-col gap-2 rounded-[var(--radius-sm)] bg-danger-bg p-3 text-sm text-danger-ink">
      <p className="font-semibold">
        No acknowledgement yet — call {incident.customerName ?? 'the owner'}
        {incident.customerPhone ? (
          <>
            {' '}
            on{' '}
            <a href={`tel:${incident.customerPhone}`} className="underline">
              <Phone size={12} className="inline" aria-hidden="true" /> {incident.customerPhone}
            </a>
          </>
        ) : null}
        .
      </p>
      <div>
        <Button size="sm" onClick={onRecord}>
          Record call
        </Button>
      </div>
    </div>
  );
}

function Row({
  incident,
  onCall,
  onResolve,
}: {
  incident: IncidentDetail;
  onCall: () => void;
  onResolve: () => void;
}) {
  const acknowledge = useRecordIncidentAcknowledgement();
  const zone = incident.branchTimeZoneId;
  const open = incident.status === 'Open';
  return (
    <li className="rounded-[var(--radius-md)] border border-border bg-surface p-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex flex-wrap items-center gap-2">
          <SeverityBadge severity={incident.severity} />
          <span className="text-[15px] font-semibold text-ink">
            {INCIDENT_KIND_LABEL[incident.kind]}
          </span>
          {incident.escalatedOn && <Badge variant="danger">Escalated</Badge>}
          <Badge variant={incident.ownerAcknowledgedOn ? 'success' : 'neutral'}>
            {incident.ownerAcknowledgedOn ? 'Owner has seen it' : 'Not acknowledged'}
          </Badge>
          {!open && <Badge variant="neutral">Resolved</Badge>}
        </div>
        <span className="text-xs text-ink-muted">
          {formatBranchInstant(incident.reportedOn, zone)} ({zone})
        </span>
      </div>
      <div className="mt-2">
        <CatHeaderCompact
          name={incident.catName}
          isHighRisk={incident.catIsHighRisk}
          handlingNote={incident.handlingNote}
        />
        <p className="text-xs text-ink-muted">
          {incident.unitName ?? 'No unit'}
          {incident.customerName ? ` · ${incident.customerName}` : ''}
        </p>
      </div>
      <p className="mt-2 text-sm text-ink">{incident.description}</p>
      {incident.phoneCallOn && (
        <p className="mt-1 text-sm text-ink-muted">Call recorded: {incident.phoneCallOutcome}</p>
      )}
      {open && incident.escalatedOn && !incident.phoneCallOn && !incident.ownerAcknowledgedOn && (
        <CallOwnerCard incident={incident} onRecord={onCall} />
      )}
      <div className="mt-3 flex flex-wrap gap-2">
        <Link
          href={staffStayHref(incident.stayId, 'incidents')}
          className="inline-flex min-h-9 items-center text-sm font-semibold text-accent-ink underline"
        >
          Open the stay
        </Link>
        {open && !incident.ownerAcknowledgedOn && (
          <Button
            size="sm"
            variant="secondary"
            onClick={() => void acknowledge.mutateAsync(incident.id)}
          >
            Owner told us they’ve seen it
          </Button>
        )}
        {open && (
          <Button size="sm" variant="secondary" onClick={onResolve}>
            Resolve…
          </Button>
        )}
      </div>
    </li>
  );
}

/**
 * `components/boarding/incidents/IncidentQueue.tsx` — HO-BRD-002 §11.9, US-BRD-13: open first, by
 * severity then time; the acknowledged state; the **escalated** chip; **Call owner** (`CallOwnerCard`)
 * when the server escalated an unacknowledged high-severity incident; `ResolveIncidentDialog`
 * (resolution required). Refreshes every 30 s; a newly arrived incident is announced in a polite
 * live region rather than re-ordering focus (ADR-FE-101).
 */
export function IncidentQueue() {
  const query = useIncidentQueue();
  const resolve = useResolveIncident();
  const [calling, setCalling] = useState<IncidentDetail | null>(null);
  const [resolving, setResolving] = useState<IncidentDetail | null>(null);
  const [announcement, setAnnouncement] = useState('');
  const seen = useRef<Set<string> | null>(null);

  useEffect(() => {
    if (!query.data) return;
    const ids = new Set(query.data.map((i) => i.id));
    if (seen.current) {
      const fresh = query.data.filter((i) => !seen.current!.has(i.id) && i.status === 'Open');
      if (fresh.length > 0) {
        setAnnouncement(
          `New ${fresh[0]!.severity.toLowerCase()}-severity incident: ${fresh[0]!.catName}.`,
        );
      }
    }
    seen.current = ids;
  }, [query.data]);

  return (
    <div className="flex flex-col gap-4">
      <p role="status" aria-live="polite" className="sr-only">
        {announcement}
      </p>
      <QueryGate query={query} loadingLabel="Loading incidents…">
        {(incidents) =>
          incidents.length === 0 ? (
            <EmptyState
              title="No incidents."
              description="Nothing has been recorded for the cats in the care centre."
            />
          ) : (
            <ul aria-label="Incidents" className="flex flex-col gap-3">
              {incidents.map((i) => (
                <Row
                  key={i.id}
                  incident={i}
                  onCall={() => setCalling(i)}
                  onResolve={() => setResolving(i)}
                />
              ))}
            </ul>
          )
        }
      </QueryGate>
      {calling && (
        <RecordCallDialog
          open
          onOpenChange={(o) => !o && setCalling(null)}
          incidentId={calling.id}
          customerName={calling.customerName ?? 'the owner'}
        />
      )}
      {resolving && (
        <ReasonDialog
          open
          onOpenChange={(o) => !o && setResolving(null)}
          title={`Resolve the incident for ${resolving.catName}?`}
          consequence="It moves out of the open queue and the resolution is kept on the record."
          reasonLabel="How was it resolved?"
          requiredMessage="Enter how this was resolved."
          confirmLabel="Resolve"
          onConfirm={(resolution) => resolve.mutateAsync({ incidentId: resolving.id, resolution })}
        />
      )}
    </div>
  );
}
