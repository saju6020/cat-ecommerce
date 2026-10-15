'use client';

import { Check, History, LoaderCircle, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { formatInZone, timeOnlyInZone } from '@/lib/format/time';
import { cn } from '@/lib/utils';
import type { AutosaveStatus } from '@/lib/hooks/useConsultationAutosave';
import type { VisitDetail } from '@/lib/api/visits';

/**
 * ⭐ `VisitHeaderBar` — HO-EMR-002 §11.1. The page heading, the service / appointment time (branch
 * zone) / opened-by line with the status as TEXT, the **History** button (drawer — ADR-FE-049), the
 * auto-save status, **Save note** and **Finalise**. Save and Finalise exist only when the server's
 * `allowedActions` has `editNote` / `finalise` (ADR-FE-051). The auto-save status is announced
 * ONLY on failure (*"Couldn't save your draft — your text is still here"*, `aria-live="polite"`); the
 * other states are plain text so a typist is never interrupted. Below 1024 px the actions sit in a
 * bottom bar (UI-1).
 */
export function VisitHeaderBar({
  visit,
  autosave,
  saving,
  onSave,
  onFinalise,
  onOpenHistory,
}: {
  visit: VisitDetail;
  autosave?: { status: AutosaveStatus; savedAt: Date | null };
  saving?: boolean;
  onSave?: () => void;
  onFinalise?: () => void;
  onOpenHistory: () => void;
}) {
  const zone = visit.branchTimeZoneId;
  const canSave = visit.allowedActions.includes('editNote');
  const canFinalise = visit.allowedActions.includes('finalise');
  const failed = autosave?.status === 'failed';

  let statusText = '';
  if (autosave?.status === 'saving') statusText = 'Saving draft…';
  else if (autosave?.status === 'saved' && autosave.savedAt)
    statusText = `Draft saved ${timeOnlyInZone(autosave.savedAt.toISOString(), zone)} (${zone})`;

  return (
    <header className="flex flex-col gap-3">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h1 className="font-display text-2xl font-semibold text-ink">
            Consultation — {visit.catName}
          </h1>
          <p className="mt-1 text-sm text-ink-muted">
            {visit.service.name}
            {visit.appointment.startsOn &&
              ` · Appointment ${formatInZone(visit.appointment.startsOn, zone)}`}{' '}
            · Opened by {visit.openedBy}{' '}
            {formatInZone(visit.openedOn, zone, { timeStyle: 'short' })} · Status:{' '}
            <strong className="text-ink">
              {visit.status === 'Finalised' ? 'Finalised' : 'Open'}
            </strong>
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {canSave && (
            <>
              {statusText && (
                <span className="inline-flex items-center gap-1.5 text-sm text-ink-muted">
                  {autosave?.status === 'saving' ? (
                    <LoaderCircle size={14} className="animate-spin" aria-hidden="true" />
                  ) : (
                    <Check size={14} aria-hidden="true" />
                  )}
                  {statusText}
                </span>
              )}
              <span
                role="status"
                aria-live="polite"
                className={cn(failed ? 'text-sm font-semibold text-danger-ink' : 'sr-only')}
              >
                {failed ? 'Couldn’t save your draft — your text is still here' : ''}
              </span>
            </>
          )}
          <Button variant="secondary" onClick={onOpenHistory}>
            <History size={16} aria-hidden="true" /> History
          </Button>
        </div>
      </div>
      {(canSave || canFinalise) && (
        <div className="fixed inset-x-0 bottom-0 z-30 flex flex-wrap justify-end gap-2 border-t border-border bg-surface p-3 lg:static lg:z-auto lg:justify-start lg:border-0 lg:bg-transparent lg:p-0">
          {canSave && (
            <Button variant="secondary" onClick={onSave} disabled={saving} aria-busy={saving}>
              <Save size={16} aria-hidden="true" /> {saving ? 'Saving…' : 'Save note'}
            </Button>
          )}
          {canFinalise && <Button onClick={onFinalise}>Finalise</Button>}
        </div>
      )}
    </header>
  );
}
