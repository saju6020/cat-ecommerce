'use client';

import { useState } from 'react';
import { CheckCircle2, HeartPulse, ShieldCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Panel } from '@/components/inventory/fields';
import { toast } from '@/components/ui/use-toast';
import { describeSurgeryError } from '@/lib/api/errors';
import { formatAppointmentWhen } from '@/lib/format/appointment';
import { recoveryLabel } from '@/lib/format/surgery';
import { useSetRecoveryStatus } from '@/lib/hooks/useSurgeries';
import { SURGERY_LIMITS } from '@/lib/validation/surgery';
import { cn } from '@/lib/utils';
import type { RecoveryStatus, SurgeryDetail } from '@/lib/api/surgeries';
import { TextAreaBox } from '@/components/surgery/TextAreaBox';
import { errorClass } from '@/components/surgery/styles';

const ICONS: Record<RecoveryStatus, typeof HeartPulse> = {
  Recovering: HeartPulse,
  Stable: ShieldCheck,
  ReadyForDischarge: CheckCircle2,
};

const ORDER: RecoveryStatus[] = ['Recovering', 'Stable', 'ReadyForDischarge'];

/**
 * ⭐ `RecoveryStatusControl` — HO-SRG-002 §11.6, US-SRG-10. A segmented control (icon AND word) for
 * *Recovering / Stable / Ready for discharge* with an optional note, and the server's history of changes
 * with who and when. Setting a status is offered only while the server's `allowedTransitions` has
 * `SetRecoveryStatus` and to nurses and vets; the front desk's board reads the same status.
 */
export function RecoveryStatusControl({
  surgery,
  canSet,
}: {
  surgery: SurgeryDetail;
  canSet: boolean;
}) {
  const set = useSetRecoveryStatus(surgery.id);
  const zone = surgery.branchTimeZoneId;
  const [choice, setChoice] = useState<RecoveryStatus | ''>('');
  const [note, setNote] = useState('');
  const [error, setError] = useState<string | null>(null);
  const editable = canSet && surgery.allowedTransitions.includes('SetRecoveryStatus');
  const current = surgery.recoveryStatus;

  async function save() {
    if (!choice) {
      setError('Choose a recovery status.');
      return;
    }
    setError(null);
    try {
      await set.mutateAsync({ status: choice, note: note.trim() || undefined });
      setChoice('');
      setNote('');
      toast({ title: `Recovery status: ${recoveryLabel(choice)}` });
    } catch (failure) {
      setError(describeSurgeryError(failure));
    }
  }

  return (
    <div className="flex max-w-3xl flex-col gap-4">
      <Panel title="Recovery status" headingId="srg-recovery">
        <p className="mb-3 text-sm text-ink">
          Now: <strong>{current ? recoveryLabel(current) : 'Not set'}</strong>
        </p>
        {editable ? (
          <div className="flex flex-col gap-3">
            <fieldset>
              <legend className="mb-2 text-sm font-semibold text-ink">Set the status</legend>
              <div className="flex flex-wrap gap-2">
                {ORDER.map((status) => {
                  const Icon = ICONS[status];
                  const selected = choice === status;
                  return (
                    <label
                      key={status}
                      className={cn(
                        'flex cursor-pointer items-center gap-2 rounded-[var(--radius-sm)] border px-3 py-2 text-sm font-semibold focus-within:outline-2 focus-within:outline-offset-2 focus-within:outline-accent',
                        selected
                          ? 'border-accent bg-accent-soft text-accent-ink'
                          : 'border-border-strong bg-surface text-ink',
                      )}
                    >
                      <input
                        type="radio"
                        name="recovery-status"
                        className="sr-only"
                        checked={selected}
                        onChange={() => setChoice(status)}
                      />
                      <Icon size={16} aria-hidden="true" />
                      {recoveryLabel(status)}
                    </label>
                  );
                })}
              </div>
            </fieldset>
            <TextAreaBox
              id="recovery-note"
              label="Note (optional)"
              value={note}
              onChange={setNote}
              max={SURGERY_LIMITS.noteMax}
              rows={2}
            />
            {error && (
              <p role="alert" className={errorClass}>
                {error}
              </p>
            )}
            <div>
              <Button onClick={() => void save()} disabled={set.isPending}>
                {set.isPending ? 'Saving…' : 'Set status'}
              </Button>
            </div>
          </div>
        ) : (
          <p className="text-sm text-ink-muted">
            {surgery.status === 'Recovery'
              ? 'Nurses and vets set the recovery status.'
              : 'The recovery status can only be set while the cat is in recovery.'}
          </p>
        )}
      </Panel>

      <Panel title="History" headingId="srg-recovery-history">
        {surgery.recoveryHistory.length === 0 ? (
          <p className="text-sm text-ink-muted">No recovery status has been set yet.</p>
        ) : (
          <ol className="flex flex-col gap-2 text-sm text-ink">
            {[...surgery.recoveryHistory].reverse().map((change) => (
              <li key={`${change.changedOn}-${change.to}`}>
                <strong>{recoveryLabel(change.to)}</strong> ·{' '}
                {formatAppointmentWhen(change.changedOn, zone)}
                {change.changedByName ? ` · ${change.changedByName}` : ''}
                {change.note && <span className="block text-ink-muted">{change.note}</span>}
              </li>
            ))}
          </ol>
        )}
      </Panel>
    </div>
  );
}
