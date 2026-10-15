'use client';

import { useState } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Banner } from '@/components/inventory/fields';
import { toast } from '@/components/ui/use-toast';
import { describeSurgeryError, surgeryViolations } from '@/lib/api/errors';
import { followUpLabel } from '@/lib/format/surgery';
import { useUpsertProtocol } from '@/lib/hooks/useSurgeryCatalogue';
import { SURGERY_LIMITS } from '@/lib/validation/surgery';
import type { ProtocolFollowUp, SurgeryProtocol } from '@/lib/api/surgery-protocols';
import type { SurgeryFollowUpKind } from '@/lib/api/surgeries';
import { TextAreaBox } from './TextAreaBox';
import { errorClass, inputClass, labelClass } from './styles';

const KINDS: SurgeryFollowUpKind[] = ['WoundCheck', 'SutureRemoval', 'Recheck'];

/** A follow-up day must sit inside its own window, and every day is 0 to 60 (HO-SRG-001 `FollowUpPlanner`). */
export function followUpRuleError(rule: ProtocolFollowUp): string | null {
  const days = [rule.dayOffset, rule.windowFromDay, rule.windowToDay];
  if (days.some((d) => !Number.isInteger(d) || d < 0 || d > 60))
    return 'Use whole days from 0 to 60.';
  if (rule.windowFromDay > rule.dayOffset || rule.dayOffset > rule.windowToDay) {
    return 'The follow-up day must sit inside its window: from ≤ day ≤ to.';
  }
  return null;
}

/**
 * `SurgeryProtocolForm` — HO-SRG-002 §11.7, US-SRG-07. One surgical service's protocol: the fasting
 * instructions and hours (hours shown as a unit), the post-op instructions, and the follow-ups (kind, day
 * offset and its window, all in days) with the window rule checked as you type. The server's refusal is
 * mapped back to its row. Values are kept across an error (NFR-USA-5).
 */
export function SurgeryProtocolForm({
  protocol,
  onClose,
}: {
  protocol: SurgeryProtocol;
  onClose: () => void;
}) {
  const save = useUpsertProtocol(protocol.serviceId);
  const [fasting, setFasting] = useState(protocol.fastingInstructions);
  const [hours, setHours] = useState(String(protocol.fastingHours));
  const [postOp, setPostOp] = useState(protocol.postOpInstructions);
  const [rules, setRules] = useState<ProtocolFollowUp[]>(protocol.followUps);
  const [showErrors, setShowErrors] = useState(false);
  const [serverErrors, setServerErrors] = useState<string[]>([]);

  const hoursNumber = Number(hours);
  const hoursError =
    showErrors && (!Number.isInteger(hoursNumber) || hoursNumber < 0 || hoursNumber > 24)
      ? 'Use whole hours from 0 to 24.'
      : undefined;

  function update(index: number, change: Partial<ProtocolFollowUp>) {
    setRules((all) => all.map((rule, i) => (i === index ? { ...rule, ...change } : rule)));
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setShowErrors(true);
    if (
      !fasting.trim() ||
      !postOp.trim() ||
      hoursError !== undefined ||
      !Number.isInteger(hoursNumber) ||
      hoursNumber < 0 ||
      hoursNumber > 24 ||
      rules.some((rule) => followUpRuleError(rule))
    ) {
      return;
    }
    setServerErrors([]);
    try {
      await save.mutateAsync({
        fastingInstructions: fasting.trim(),
        fastingHours: hoursNumber,
        postOpInstructions: postOp.trim(),
        followUps: rules,
      });
      toast({ title: `${protocol.serviceName} protocol saved` });
      onClose();
    } catch (error) {
      const messages = surgeryViolations(error).map((v) => v.message);
      setServerErrors(messages.length > 0 ? messages : [describeSurgeryError(error)]);
    }
  }

  return (
    <Dialog open onOpenChange={(open) => !open && !save.isPending && onClose()}>
      <DialogContent className="max-h-[90vh] max-w-2xl overflow-y-auto">
        <form noValidate onSubmit={(event) => void submit(event)} className="flex flex-col gap-4">
          <DialogHeader>
            <DialogTitle>{protocol.serviceName} protocol</DialogTitle>
            <DialogDescription>
              {protocol.isDefault
                ? 'This procedure is using the default protocol. Saving creates its own.'
                : 'Changes apply to surgeries booked from now on.'}
            </DialogDescription>
          </DialogHeader>
          {serverErrors.length > 0 && (
            <Banner tone="danger">
              <ul className="list-disc pl-5">
                {serverErrors.map((m) => (
                  <li key={m}>{m}</li>
                ))}
              </ul>
            </Banner>
          )}
          <TextAreaBox
            id="protocol-fasting"
            label="Fasting instructions"
            value={fasting}
            onChange={setFasting}
            max={SURGERY_LIMITS.fastingInstructionsMax}
            error={showErrors && !fasting.trim() ? 'Write the fasting instructions.' : undefined}
          />
          <div className="flex flex-col gap-1.5">
            <label htmlFor="protocol-hours" className={labelClass}>
              Fasting hours
            </label>
            <div className="flex max-w-40 items-stretch">
              <input
                id="protocol-hours"
                inputMode="numeric"
                className={`${inputClass} rounded-r-none`}
                value={hours}
                aria-invalid={hoursError ? true : undefined}
                onChange={(event) => setHours(event.target.value)}
              />
              <span className="flex items-center rounded-r-[var(--radius-sm)] border border-l-0 border-border-strong bg-surface-sunken px-3 text-sm font-semibold text-ink">
                hours
              </span>
            </div>
            {hoursError && (
              <p role="alert" className={errorClass}>
                {hoursError}
              </p>
            )}
          </div>
          <TextAreaBox
            id="protocol-postop"
            label="Post-op instructions"
            value={postOp}
            onChange={setPostOp}
            max={SURGERY_LIMITS.postOpInstructionsMax}
            rows={6}
            error={showErrors && !postOp.trim() ? 'Write the post-op instructions.' : undefined}
          />
          <fieldset className="flex flex-col gap-3">
            <legend className="text-sm font-semibold text-ink">Follow-ups</legend>
            {rules.length === 0 && <p className="text-sm text-ink-muted">No follow-ups.</p>}
            {rules.map((rule, index) => {
              const error = showErrors ? followUpRuleError(rule) : null;
              const id = `protocol-fu-${index}`;
              return (
                <div
                  key={index}
                  className="flex flex-col gap-2 rounded-[var(--radius-sm)] border border-border p-3"
                >
                  <div className="flex flex-wrap items-end gap-3">
                    <div className="flex flex-col gap-1.5">
                      <label htmlFor={`${id}-kind`} className={labelClass}>
                        Kind
                      </label>
                      <select
                        id={`${id}-kind`}
                        className={inputClass}
                        value={rule.kind}
                        onChange={(event) =>
                          update(index, { kind: event.target.value as SurgeryFollowUpKind })
                        }
                      >
                        {KINDS.map((kind) => (
                          <option key={kind} value={kind}>
                            {followUpLabel(kind)}
                          </option>
                        ))}
                      </select>
                    </div>
                    {(
                      [
                        ['dayOffset', 'Due on day'],
                        ['windowFromDay', 'Window from day'],
                        ['windowToDay', 'Window to day'],
                      ] as const
                    ).map(([field, label]) => (
                      <div key={field} className="flex w-32 flex-col gap-1.5">
                        <label htmlFor={`${id}-${field}`} className={labelClass}>
                          {label}
                        </label>
                        <div className="flex items-stretch">
                          <input
                            id={`${id}-${field}`}
                            inputMode="numeric"
                            className={`${inputClass} min-w-0 rounded-r-none`}
                            value={String(rule[field])}
                            aria-invalid={error ? true : undefined}
                            onChange={(event) =>
                              update(index, { [field]: Number(event.target.value) })
                            }
                          />
                          <span className="flex items-center rounded-r-[var(--radius-sm)] border border-l-0 border-border-strong bg-surface-sunken px-2 text-sm font-semibold text-ink">
                            days
                          </span>
                        </div>
                      </div>
                    ))}
                    <Button
                      variant="secondary"
                      size="sm"
                      aria-label={`Remove follow-up ${index + 1}`}
                      onClick={() => setRules((all) => all.filter((_, i) => i !== index))}
                    >
                      <Trash2 size={14} aria-hidden="true" /> Remove
                    </Button>
                  </div>
                  {error && (
                    <p role="alert" className={errorClass}>
                      {error}
                    </p>
                  )}
                </div>
              );
            })}
            {rules.length < SURGERY_LIMITS.maxFollowUps && (
              <div>
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={() =>
                    setRules((all) => [
                      ...all,
                      { kind: 'WoundCheck', dayOffset: 3, windowFromDay: 2, windowToDay: 5 },
                    ])
                  }
                >
                  <Plus size={14} aria-hidden="true" /> Add a follow-up
                </Button>
              </div>
            )}
          </fieldset>
          <DialogFooter>
            <Button variant="secondary" onClick={onClose} disabled={save.isPending}>
              Cancel
            </Button>
            <Button type="submit" disabled={save.isPending}>
              {save.isPending ? 'Saving…' : 'Save protocol'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
