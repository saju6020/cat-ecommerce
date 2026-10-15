'use client';

import { useState } from 'react';
import { TriangleAlert } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ClinicalText } from './ClinicalText';
import { useVisit } from '@/lib/hooks/useVisit';
import { formatInZone } from '@/lib/format/time';
import type { NoteFormValues } from '@/lib/validation/clinical';
import type { VisitDetail } from '@/lib/api/visits';

/**
 * `SaveConflictPanel` — HO-EMR-002 §11.1 (`409` on Save note, US-CLN-12). *"Someone else saved this
 * note since you opened it. Your text is kept below — reload to see their version."* **Reload**
 * refetches the visit and shows THEIR saved note beside YOUR text, both read-only, for copying. There
 * is no automatic merge: your form is untouched, and pressing Save note again (now against the new
 * version) is the vet's own explicit decision.
 */
const SECTIONS: Array<[keyof Omit<NoteFormValues, 'problems'>, string]> = [
  ['subjective', 'Subjective'],
  ['objective', 'Objective'],
  ['assessment', 'Assessment'],
  ['plan', 'Plan'],
  ['dischargeInstructions', 'Home-care instructions'],
];

export function SaveConflictPanel({
  visit,
  mine,
  zone,
  onDismiss,
}: {
  visit: VisitDetail;
  mine: NoteFormValues;
  zone: string;
  onDismiss: () => void;
}) {
  const query = useVisit(visit.id);
  const [reloaded, setReloaded] = useState(false);

  return (
    <section
      aria-labelledby="conflict-heading"
      className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-warning-ink bg-warning-bg p-4 text-warning-ink"
    >
      <h2 id="conflict-heading" className="flex items-center gap-2 text-sm font-bold">
        <TriangleAlert size={16} aria-hidden="true" /> This note was saved by someone else
      </h2>
      <p role="alert" className="text-sm">
        Someone else saved this note since you opened it. Your text is kept below — reload to see
        their version.
      </p>
      <div className="flex flex-wrap gap-2">
        <Button
          variant="secondary"
          size="sm"
          onClick={async () => {
            await query.refetch();
            setReloaded(true);
          }}
        >
          Reload
        </Button>
        <Button variant="ghost" size="sm" onClick={onDismiss}>
          Dismiss
        </Button>
      </div>
      {reloaded && (
        <div className="grid gap-4 text-ink md:grid-cols-2">
          <div className="min-w-0 rounded-[var(--radius-sm)] bg-surface p-3">
            <h3 className="mb-2 text-sm font-bold">
              Their saved note
              {visit.note.savedOn && (
                <span className="block text-xs font-normal text-ink-muted">
                  {visit.note.savedBy} · {formatInZone(visit.note.savedOn, zone)}
                </span>
              )}
            </h3>
            {SECTIONS.map(([key, label]) => (
              <div key={key} className="mb-2">
                <h4 className="text-xs font-semibold text-ink-muted">{label}</h4>
                <ClinicalText>{visit.note[key]}</ClinicalText>
              </div>
            ))}
          </div>
          <div className="min-w-0 rounded-[var(--radius-sm)] bg-surface p-3">
            <h3 className="mb-2 text-sm font-bold">Your text (read-only copy)</h3>
            {SECTIONS.map(([key, label]) => (
              <div key={key} className="mb-2">
                <h4 className="text-xs font-semibold text-ink-muted">{label}</h4>
                <ClinicalText>{mine[key]}</ClinicalText>
              </div>
            ))}
          </div>
        </div>
      )}
    </section>
  );
}
