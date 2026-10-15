'use client';

import { useState } from 'react';
import Link from 'next/link';
import { Camera } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { LoadingState } from '@/components/common/LoadingState';
import { CatProfileHeader } from '@/components/cat/CatProfileHeader';
import { MediaRecordDialog } from '@/components/boarding/care/MediaRecordDialog';
import { IncidentForm } from '@/components/boarding/incidents/IncidentForm';
import { CatHeaderCompact } from '@/components/boarding/shared/CatHeaderCompact';
import { ReasonDialog } from '@/components/boarding/shared/ReasonDialog';
import { QueryGate } from '@/components/boarding/shared/QueryGate';
import { SeverityBadge } from '@/components/boarding/shared/StatusBadges';
import {
  INCIDENT_KIND_LABEL,
  MOOD_LABEL,
  PORTION_SPOKEN,
  WATER_LABEL,
} from '@/components/boarding/shared/labels';
import type { StaffStayDetail } from '@/lib/api/types';
import { formatBranchInstant } from '@/lib/format/boarding';
import { useCat } from '@/lib/hooks/useCat';
import {
  useHideStayMedia,
  useStayIncidents,
  useStayMedia,
  useStayObservations,
} from '@/lib/hooks/useBoardingCare';
import { STAFF_BOARDING_TASKS } from '@/lib/routing/boarding-links';

/** Full `CatProfileHeader` (UI-7) on the stay workspace: it needs the cat's profile, so each cat's
 *  header loads it and shows the compact header until it arrives (or if it cannot load). */
export function StayCatHeaders({ stay }: { stay: StaffStayDetail }) {
  return (
    <div className="flex flex-col gap-3">
      {stay.cats.map((cat) => (
        <FullHeader key={cat.catId} stay={stay} catId={cat.catId} />
      ))}
    </div>
  );
}

function FullHeader({ stay, catId }: { stay: StaffStayDetail; catId: string }) {
  const profile = useCat(catId);
  const cat = stay.cats.find((c) => c.catId === catId)!;
  if (profile.data) return <CatProfileHeader cat={profile.data} />;
  if (profile.isPending)
    return <LoadingState variant="card" rows={2} label={`Loading ${cat.catName}…`} />;
  return (
    <div className="rounded-[var(--radius-md)] border border-border bg-surface p-4">
      <CatHeaderCompact
        name={cat.catName}
        isHighRisk={cat.catIsHighRisk}
        handlingNote={cat.handlingNote}
      />
    </div>
  );
}

export function DiaryTab({ stay }: { stay: StaffStayDetail }) {
  const obs = useStayObservations(stay.id);
  const media = useStayMedia(stay.id);
  const hide = useHideStayMedia(stay.id);
  const [recording, setRecording] = useState(false);
  const [hiding, setHiding] = useState<string | null>(null);
  const zone = stay.branchTimeZoneId;
  return (
    <section aria-label="Diary" className="flex flex-col gap-4">
      <QueryGate query={obs} loadingLabel="Loading observations…">
        {(data) => (
          <>
            {data.cats
              .filter((c) => c.notEating)
              .map((c) => (
                <p
                  key={c.catId}
                  role="status"
                  className="rounded-[var(--radius-sm)] bg-danger-bg px-3 py-2 text-sm font-bold text-danger-ink"
                >
                  Not eating — flagged: {c.catName}
                </p>
              ))}
            {data.items.length === 0 ? (
              <p className="text-sm text-ink-muted">No observations yet.</p>
            ) : (
              <ul aria-label="Observations" className="flex flex-col gap-2 text-sm">
                {data.items.map((o) => (
                  <li key={o.id} className="rounded-[var(--radius-sm)] border border-border p-3">
                    <strong>{o.catName}</strong> · {formatBranchInstant(o.recordedOn, zone)} ·{' '}
                    {o.recordedByName}
                    <br />
                    {PORTION_SPOKEN[o.portionEaten]} · water{' '}
                    {WATER_LABEL[o.waterIntake].toLowerCase()} · {MOOD_LABEL[o.mood].toLowerCase()}
                    {o.notes ? ` — ${o.notes}` : ''}
                  </li>
                ))}
              </ul>
            )}
          </>
        )}
      </QueryGate>
      <div>
        <h3 className="mb-2 font-display text-base font-semibold text-ink">Photos</h3>
        {media.data && media.data.length === 0 && (
          <p className="text-sm text-ink-muted">No photo references recorded.</p>
        )}
        <ul className="flex flex-col gap-2 text-sm">
          {(media.data ?? []).map((m) => (
            <li
              key={m.id}
              className="flex flex-wrap items-center justify-between gap-2 rounded-[var(--radius-sm)] border border-dashed border-border-strong bg-surface-sunken p-3"
            >
              <span>
                <Camera size={14} className="mr-1 inline" aria-hidden="true" />
                {m.caption ?? 'Photo'} · {m.capturedOnLocalDate} · {m.status}
                {m.reference ? ` · ref ${m.reference}` : ''}
              </span>
              {m.status === 'Visible' && (
                <Button size="sm" variant="secondary" onClick={() => setHiding(m.id)}>
                  Hide…
                </Button>
              )}
            </li>
          ))}
        </ul>
        <div className="mt-2">
          <Button
            variant="secondary"
            size="sm"
            onClick={() => setRecording(true)}
            disabled={!['CheckedIn', 'CheckedOut'].includes(stay.status)}
          >
            Add a diary photo
          </Button>
        </div>
      </div>
      {recording && (
        <MediaRecordDialog
          open
          onOpenChange={(o) => !o && setRecording(false)}
          stayId={stay.id}
          catId={stay.cats[0]?.catId ?? null}
          catName={stay.cats[0]?.catName ?? 'the cat'}
          zone={zone}
        />
      )}
      {hiding && (
        <ReasonDialog
          open
          onOpenChange={(o) => !o && setHiding(null)}
          title="Hide this photo from the diary?"
          consequence="The owner will no longer see it. The record is kept."
          reasonLabel="Why is it being hidden?"
          requiredMessage="Enter a reason."
          confirmLabel="Hide it"
          onConfirm={(reason) => hide.mutateAsync({ mediaId: hiding, reason })}
        />
      )}
    </section>
  );
}

export function IncidentsTab({ stay }: { stay: StaffStayDetail }) {
  const incidents = useStayIncidents(stay.id);
  const [recording, setRecording] = useState(false);
  return (
    <section aria-label="Incidents" className="flex flex-col gap-3">
      <QueryGate query={incidents} loadingLabel="Loading incidents…">
        {(list) =>
          list.length === 0 ? (
            <p className="text-sm text-ink-muted">No incidents on this stay.</p>
          ) : (
            <ul className="flex flex-col gap-2 text-sm">
              {list.map((i) => (
                <li key={i.id} className="rounded-[var(--radius-sm)] border border-border p-3">
                  <div className="flex flex-wrap items-center gap-2">
                    <SeverityBadge severity={i.severity} />
                    <strong>{INCIDENT_KIND_LABEL[i.kind]}</strong> — {i.catName}
                    <span className="text-ink-muted">
                      {i.ownerAcknowledgedOn ? 'seen by owner' : 'not acknowledged'}
                    </span>
                  </div>
                  <p className="mt-1">{i.description}</p>
                </li>
              ))}
            </ul>
          )
        }
      </QueryGate>
      <div>
        <Button
          variant="secondary"
          onClick={() => setRecording(true)}
          disabled={stay.status !== 'CheckedIn'}
        >
          Record an incident
        </Button>
        {stay.status !== 'CheckedIn' && (
          <p className="mt-1 text-xs text-ink-muted">
            Incidents are recorded while the cat is in the care centre.
          </p>
        )}
      </div>
      {recording && (
        <IncidentForm
          open
          onOpenChange={(o) => !o && setRecording(false)}
          stayId={stay.id}
          cats={stay.cats.map((c) => ({ catId: c.catId, catName: c.catName }))}
        />
      )}
    </section>
  );
}

export function TasksTab() {
  return (
    <p className="text-sm text-ink">
      Care tasks are worked from the{' '}
      <Link href={STAFF_BOARDING_TASKS} className="font-semibold text-accent-ink underline">
        task board
      </Link>
      , grouped by unit.
    </p>
  );
}

export function HistoryTab({ stay }: { stay: StaffStayDetail }) {
  const zone = stay.branchTimeZoneId;
  return (
    <section aria-label="History" className="flex flex-col gap-4 text-sm">
      <div>
        <h3 className="mb-1 font-display text-base font-semibold text-ink">Status history</h3>
        <ol className="flex flex-col gap-1">
          {stay.statusHistory.map((h, i) => (
            <li key={i}>
              {formatBranchInstant(h.changedOn, zone)} — {h.from} → {h.to} by {h.changedBy}
            </li>
          ))}
        </ol>
      </div>
      <div>
        <h3 className="mb-1 font-display text-base font-semibold text-ink">Units</h3>
        <ul className="flex flex-col gap-1">
          {stay.allocations.map((a) => (
            <li key={a.id}>
              {a.unitName} · {formatBranchInstant(a.from, zone)} → {formatBranchInstant(a.to, zone)}
              {a.isActive ? '' : ' (released)'}
              {a.reason ? ` — ${a.reason}` : ''}
            </li>
          ))}
        </ul>
      </div>
    </section>
  );
}

export function HouseFoodPanel() {
  return (
    <section aria-labelledby="house-food-heading" className="flex flex-col gap-1">
      <h3 id="house-food-heading" className="font-display text-base font-semibold text-ink">
        House food issued
      </h3>
      <p className="text-sm text-ink-muted">
        Nothing is listed here yet. Food issued from stock for this stay appears here once Inventory
        records it (listed only — issuing is done in Inventory).
      </p>
    </section>
  );
}
