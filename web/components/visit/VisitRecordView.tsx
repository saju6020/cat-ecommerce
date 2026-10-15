'use client';

import type { ReactNode } from 'react';
import { Badge } from '@/components/ui/badge';
import { ClinicalText } from './ClinicalText';
import { VitalsReadout } from './VitalsReadout';
import { PhotoThumb } from './PhotoThumb';
import { diagnosticKindLabel, diagnosticStatusLabel } from '@/lib/format/clinical';
import { formatInZone, formatDateOnly } from '@/lib/format/time';
import type { VisitDetail } from '@/lib/api/visits';

/**
 * ⭐ `VisitRecordView` — HO-EMR-002 §11.1 (ADR-FE-051). A visit as a RECORD: every section as text
 * (`pre-wrap`, never parsed — ADR-FE-058), NO inputs. Used for a finalised visit, for a role that may
 * not edit an open one (nurse, administrator — U-A2/U-A3), and nested read-only inside the history
 * drawer. *"Finalised by Dr Rahman on Mon 12 Oct 2026, 11:04 (Asia/Dhaka)"* when finalised.
 */
export function VisitRecordView({ visit, children }: { visit: VisitDetail; children?: ReactNode }) {
  const zone = visit.branchTimeZoneId;
  const finalised = visit.status === 'Finalised';
  const note = visit.note;
  const sections: Array<[string, string]> = [
    ['Subjective', note.subjective],
    ['Objective', note.objective],
    ['Assessment', note.assessment],
    ['Plan', note.plan],
    ['Home-care instructions', note.dischargeInstructions],
  ];
  return (
    <article aria-label="Consultation record" className="flex flex-col gap-5">
      <div className="flex flex-wrap items-center gap-2 text-sm text-ink-muted">
        <Badge variant={finalised ? 'success' : 'neutral'}>
          {finalised ? 'Finalised' : 'Open — in progress'}
        </Badge>
        {finalised && visit.finalisedOn && (
          <span>
            Finalised by {visit.finalisedBy} on{' '}
            {formatInZone(visit.finalisedOn, zone, {
              weekday: 'short',
              day: 'numeric',
              month: 'short',
              year: 'numeric',
              hour: '2-digit',
              minute: '2-digit',
            })}
          </span>
        )}
      </div>

      <section aria-labelledby="record-note" className="flex flex-col gap-3">
        <h2 id="record-note" className="text-sm font-bold uppercase tracking-wide text-ink-muted">
          Note
        </h2>
        {sections.map(([title, text]) => (
          <div key={title}>
            <h3 className="text-sm font-semibold text-ink">{title}</h3>
            <ClinicalText>{text}</ClinicalText>
          </div>
        ))}
        {note.savedOn && (
          <p className="text-xs text-ink-muted">
            Last saved by {note.savedBy} · {formatInZone(note.savedOn, zone)}
          </p>
        )}
      </section>

      <section aria-labelledby="record-problems" className="flex flex-col gap-2">
        <h2
          id="record-problems"
          className="text-sm font-bold uppercase tracking-wide text-ink-muted"
        >
          Problems
        </h2>
        {visit.problems.length === 0 ? (
          <p className="text-sm text-ink-muted">None recorded</p>
        ) : (
          <ul className="m-0 flex list-none flex-col gap-1 p-0 text-sm">
            {visit.problems.map((problem) => (
              <li key={problem.id}>
                <span className="font-semibold text-ink">
                  {problem.code} — {problem.name}
                </span>{' '}
                <span className="text-ink-muted">· {problem.certainty}</span>
                {problem.note && (
                  <span className="whitespace-pre-wrap text-ink-muted"> · {problem.note}</span>
                )}
              </li>
            ))}
          </ul>
        )}
      </section>

      <section aria-labelledby="record-vitals" className="flex flex-col gap-3">
        <h2 id="record-vitals" className="text-sm font-bold uppercase tracking-wide text-ink-muted">
          Vitals
        </h2>
        {visit.vitals.length === 0 ? (
          <p className="text-sm text-ink-muted">None recorded</p>
        ) : (
          [...visit.vitals]
            .sort((a, b) => b.measuredOn.localeCompare(a.measuredOn))
            .map((entry) => <VitalsReadout key={entry.id} entry={entry} zone={zone} />)
        )}
      </section>

      <section aria-labelledby="record-diagnostics" className="flex flex-col gap-2">
        <h2
          id="record-diagnostics"
          className="text-sm font-bold uppercase tracking-wide text-ink-muted"
        >
          Diagnostics
        </h2>
        {visit.diagnostics.length === 0 ? (
          <p className="text-sm text-ink-muted">None ordered</p>
        ) : (
          <ul className="m-0 flex list-none flex-col gap-1 p-0 text-sm text-ink">
            {visit.diagnostics.map((order) => (
              <li key={order.id}>
                {diagnosticKindLabel(order.kind)} · {diagnosticStatusLabel(order.status)}
                {order.hasAbnormal ? ' · has abnormal results' : ''}
              </li>
            ))}
          </ul>
        )}
      </section>

      {visit.photos.length > 0 && (
        <section aria-labelledby="record-photos" className="flex flex-col gap-2">
          <h2
            id="record-photos"
            className="text-sm font-bold uppercase tracking-wide text-ink-muted"
          >
            Photos
          </h2>
          <ul className="m-0 flex list-none flex-wrap gap-3 p-0">
            {visit.photos.map((photo) => (
              <li key={photo.id} className="w-28">
                <PhotoThumb photo={photo} />
              </li>
            ))}
          </ul>
        </section>
      )}

      <section aria-labelledby="record-followup" className="flex flex-col gap-1">
        <h2
          id="record-followup"
          className="text-sm font-bold uppercase tracking-wide text-ink-muted"
        >
          Follow-up
        </h2>
        <p className="text-sm text-ink">
          {visit.followUp
            ? `${visit.followUp.kind === 'Appointment' ? 'Appointment booked for' : 'Reminder on'} ${formatDateOnly(visit.followUp.dueOn)}`
            : 'No follow-up set'}
        </p>
      </section>
      {children}
    </article>
  );
}
