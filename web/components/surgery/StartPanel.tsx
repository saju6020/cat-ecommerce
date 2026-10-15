'use client';

import { useState } from 'react';
import { AlertTriangle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { toast } from '@/components/ui/use-toast';
import { describeSurgeryError } from '@/lib/api/errors';
import { useStaffSession } from '@/lib/hooks/useSession';
import { useStaffRoles } from '@/lib/hooks/useStaffRoles';
import {
  useAdmitSurgery,
  useStartSurgery,
  useStartWithConsentOverride,
} from '@/lib/hooks/useSurgeries';
import type { SurgeryDetail } from '@/lib/api/surgeries';
import { ConsentOverrideDialog } from './ConsentOverrideDialog';
import { errorClass } from './styles';

/** Where a blocker's fix lives: the checklist row it names. */
function blockerTarget(code: string, surgery: SurgeryDetail): string {
  if (code === 'ConsentNotRecorded') return '#checklist-ConsentSigned';
  const first = surgery.checklistSummary.outstandingItems.find((kind) => kind !== 'ConsentSigned');
  return first ? `#checklist-${first}` : '#srg-checklist';
}

/**
 * ⭐ `StartPanel` — HO-SRG-002 §11.3, ADR-FE-072, BR-5. Renders FROM THE SERVER: **Admit** only when
 * `allowedTransitions` has `Admit`; **Start surgery** only when it has `Start` AND `startBlockers` is
 * empty; otherwise the blockers are listed (in a live region) as links to the checklist rows that fix
 * them. **Start with emergency override** appears only for vets and only when consent is the sole
 * blocker. Nothing here decides consent or the checklist; a role without the action sees neither button.
 */
export function StartPanel({ surgery }: { surgery: SurgeryDetail }) {
  const roles = useStaffRoles();
  const session = useStaffSession();
  const admit = useAdmitSurgery(surgery.id);
  const start = useStartSurgery(surgery.id);
  const override = useStartWithConsentOverride(surgery.id);
  const [confirmStart, setConfirmStart] = useState(false);
  const [overriding, setOverriding] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const canAdmit =
    surgery.allowedTransitions.includes('Admit') && roles.has('Reception', 'Nurse', 'Vet');
  const canStartHere = surgery.allowedTransitions.includes('Start');
  const mayStart = roles.has('Nurse', 'Vet');
  const blockers = surgery.startBlockers;
  const startable = canStartHere && blockers.length === 0;
  const consentOnly = blockers.length === 1 && blockers[0]!.code === 'ConsentNotRecorded';
  const canOverride = canStartHere && consentOnly && roles.has('Vet');

  if (!canAdmit && !canStartHere) return null;

  async function admitNow() {
    setError(null);
    try {
      await admit.mutateAsync();
      toast({ title: `${surgery.catName} admitted` });
    } catch (failure) {
      setError(describeSurgeryError(failure));
    }
  }

  return (
    <section aria-labelledby="srg-start" className="flex flex-col gap-3">
      <h2 id="srg-start" className="font-display text-lg font-semibold text-ink">
        {canAdmit ? 'Admission' : 'Start the surgery'}
      </h2>
      {canAdmit && (
        <div className="flex flex-col gap-2">
          <p className="text-sm text-ink-muted">
            Admit {surgery.catName} when the owner brings them in on the day of surgery.
          </p>
          <div>
            <Button onClick={() => void admitNow()} disabled={admit.isPending}>
              {admit.isPending ? 'Admitting…' : 'Admit'}
            </Button>
          </div>
        </div>
      )}
      {canStartHere && (
        <>
          <ul
            aria-live="polite"
            aria-label="What is stopping the start"
            className="flex flex-col gap-1.5"
          >
            {blockers.map((blocker) => (
              <li
                key={blocker.code}
                className="flex items-start gap-2 rounded-[var(--radius-sm)] bg-warning-bg p-2 text-sm text-warning-ink"
              >
                <AlertTriangle size={16} className="mt-0.5 flex-none" aria-hidden="true" />
                <span>
                  {blocker.message}{' '}
                  <a
                    href={blockerTarget(blocker.code, surgery)}
                    className="font-semibold underline"
                  >
                    Go to the checklist
                  </a>
                </span>
              </li>
            ))}
            {blockers.length === 0 && (
              <li className="text-sm text-accent-ink">Everything is in place to start.</li>
            )}
          </ul>
          {mayStart ? (
            <div className="flex flex-wrap gap-2">
              <Button
                disabled={!startable || start.isPending}
                onClick={() => setConfirmStart(true)}
              >
                Start surgery
              </Button>
              {canOverride && (
                <Button variant="destructive" onClick={() => setOverriding(true)}>
                  Start with emergency override
                </Button>
              )}
            </div>
          ) : (
            <p className="text-sm text-ink-muted">Nurses and vets start the surgery.</p>
          )}
        </>
      )}
      {error && (
        <p role="alert" className={errorClass}>
          {error}
        </p>
      )}

      <ConfirmDialog
        open={confirmStart}
        onOpenChange={setConfirmStart}
        title={`Start ${surgery.catName}’s ${surgery.procedure.name.toLowerCase()}?`}
        consequence="The surgery moves to In theatre and the anaesthesia log starts counting from now."
        confirmLabel="Start surgery"
        pendingLabel="Starting…"
        onConfirm={async () => {
          try {
            await start.mutateAsync();
            toast({ title: 'Surgery started' });
          } catch (failure) {
            setError(describeSurgeryError(failure));
          } finally {
            setConfirmStart(false);
          }
        }}
      />
      <ConsentOverrideDialog
        open={overriding}
        onOpenChange={setOverriding}
        vetName={session.data?.fullName ?? 'Vet'}
        catName={surgery.catName}
        onConfirm={async (reason) => {
          const result = await override.mutateAsync(reason);
          // 204 = the override was recorded; 200 { overrideIgnored } = consent was on file, nothing overridden.
          toast(
            result?.overrideIgnored || result?.consentWasOverridden === false
              ? {
                  title: 'Surgery started',
                  description:
                    'Consent was already recorded, so no override was needed or recorded.',
                }
              : { title: 'Surgery started with an emergency override' },
          );
        }}
      />
    </section>
  );
}
