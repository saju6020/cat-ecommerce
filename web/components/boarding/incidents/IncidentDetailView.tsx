'use client';

import { useState } from 'react';
import Link from 'next/link';
import { CheckCircle2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { CatHeaderCompact } from '@/components/boarding/shared/CatHeaderCompact';
import { FormErrorAlert } from '@/components/boarding/shared/FormBits';
import { QueryGate } from '@/components/boarding/shared/QueryGate';
import { SeverityBadge } from '@/components/boarding/shared/StatusBadges';
import { INCIDENT_KIND_LABEL } from '@/components/boarding/shared/labels';
import { describeBoardingError } from '@/lib/api/errors';
import type { IncidentDetail } from '@/lib/api/types';
import { formatBranchInstant } from '@/lib/format/boarding';
import { timeOnlyInZone } from '@/lib/format/time';
import { useAcknowledgeIncident, useMyIncident } from '@/lib/hooks/useMyBoarding';
import { portalStayHref } from '@/lib/routing/boarding-links';

function Body({ incident, stayId }: { incident: IncidentDetail; stayId: string }) {
  const acknowledge = useAcknowledgeIncident(stayId, incident.id);
  const [error, setError] = useState<string | null>(null);
  const zone = incident.branchTimeZoneId;
  const seen = incident.ownerAcknowledgedOn;

  return (
    <article className="flex flex-col gap-5" aria-labelledby="incident-heading">
      <header className="flex flex-col gap-2">
        <h1 id="incident-heading" className="font-display text-2xl font-semibold text-ink">
          {INCIDENT_KIND_LABEL[incident.kind]}
        </h1>
        <div className="flex flex-wrap items-center gap-3">
          <SeverityBadge severity={incident.severity} />
          <span className="text-sm text-ink-muted">
            {formatBranchInstant(incident.occurredOn, zone)} ({zone})
          </span>
        </div>
        <CatHeaderCompact
          name={incident.catName}
          isHighRisk={incident.catIsHighRisk}
          handlingNote={incident.handlingNote}
        />
      </header>
      <dl className="grid gap-x-6 gap-y-3 text-sm sm:grid-cols-[10rem_1fr]">
        <dt className="font-semibold text-ink-muted">What happened</dt>
        <dd className="text-ink">{incident.description}</dd>
        <dt className="font-semibold text-ink-muted">What was done</dt>
        <dd className="text-ink">{incident.immediateAction}</dd>
        {incident.status === 'Resolved' && (
          <>
            <dt className="font-semibold text-ink-muted">Resolved</dt>
            <dd className="text-ink">{incident.resolution}</dd>
          </>
        )}
      </dl>

      <div aria-live="polite">
        {seen ? (
          <Badge variant="success" icon={<CheckCircle2 size={12} aria-hidden="true" />}>
            Acknowledged at {timeOnlyInZone(seen, zone)}.
          </Badge>
        ) : (
          <div className="flex flex-col gap-2">
            <FormErrorAlert message={error} />
            <div>
              <Button
                onClick={async () => {
                  setError(null);
                  try {
                    await acknowledge.mutateAsync();
                  } catch (e) {
                    setError(describeBoardingError(e));
                  }
                }}
                disabled={acknowledge.isPending}
                aria-busy={acknowledge.isPending}
              >
                {acknowledge.isPending ? 'Saving…' : 'I’ve seen this'}
              </Button>
            </div>
            <p className="text-xs text-ink-muted">
              Pressing this tells the care centre you have read this. It does not approve anything.
            </p>
          </div>
        )}
      </div>
      <Link
        href={portalStayHref(stayId)}
        className="text-sm font-semibold text-accent-ink underline"
      >
        ← Back to the stay
      </Link>
    </article>
  );
}

/** `components/boarding/incidents/IncidentDetailView.tsx` — HO-BRD-002 §11.3, US-BRD-13: the owner's
 *  incident page with **I’ve seen this**, after which it reads *“Acknowledged at 21:14.”* */
export function IncidentDetailView({ stayId, incidentId }: { stayId: string; incidentId: string }) {
  const query = useMyIncident(stayId, incidentId);
  return (
    <QueryGate query={query} loadingLabel="Loading…" notFoundTitle="We can’t find that incident.">
      {(incident) => <Body incident={incident} stayId={stayId} />}
    </QueryGate>
  );
}
