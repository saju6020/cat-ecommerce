'use client';

import { useState } from 'react';
import Link from 'next/link';
import { CheckCircle2, CircleDashed, MinusCircle } from 'lucide-react';
import { Button, buttonVariants } from '@/components/ui/button';
import { ActionDialog, ReasonDialog } from '@/components/inventory/ReasonDialog';
import { toast } from '@/components/ui/use-toast';
import { describeSurgeryError } from '@/lib/api/errors';
import { formatAppointmentWhen } from '@/lib/format/appointment';
import { checklistLabel } from '@/lib/format/surgery';
import { useStaffRoles } from '@/lib/hooks/useStaffRoles';
import { useCompleteChecklistItem, useWaiveChecklistItem } from '@/lib/hooks/useSurgeries';
import { SURGERY_LIMITS } from '@/lib/validation/surgery';
import type { ChecklistItem, SurgeryDetail } from '@/lib/api/surgeries';
import { AsaGradeControl } from './AsaGradeControl';
import { TextAreaBox } from './TextAreaBox';

const HOW = [
  { value: 'message', label: 'Sent as a message' },
  { value: 'printed', label: 'Printed and handed over' },
  { value: 'verbal', label: 'Told to the owner' },
] as const;

function StatusText({ item }: { item: ChecklistItem }) {
  if (item.status === 'Done') {
    return (
      <span className="inline-flex items-center gap-1 font-semibold text-accent-ink">
        <CheckCircle2 size={14} aria-hidden="true" /> Done
      </span>
    );
  }
  if (item.status === 'Waived') {
    return (
      <span className="inline-flex items-center gap-1 font-semibold text-warning-ink">
        <MinusCircle size={14} aria-hidden="true" /> Waived
      </span>
    );
  }
  return (
    <span className="inline-flex items-center gap-1 font-semibold text-danger-ink">
      <CircleDashed size={14} aria-hidden="true" /> Outstanding
    </span>
  );
}

/** The names the detail view lets us attach to an id (it carries ids for who completed an item). */
function knownName(surgery: SurgeryDetail, id: string | null): string | null {
  if (!id) return null;
  if (id === surgery.surgeon.id) return surgery.surgeon.name;
  if (id === surgery.assistant.id) return surgery.assistant.name;
  if (surgery.consentOverride?.overriddenBy === id) return surgery.consentOverride.overriddenByName;
  return null;
}

/**
 * ⭐ `PreOpChecklist` — HO-SRG-002 §11.3, US-SRG-02. The five items in the SERVER's order, each with its
 * status as text + icon, who/when, and its note or waiver reason. The action on a row depends on the
 * item and the role: *Fasting instructions* → Mark issued; *Blood work* → Mark done, or Waive (vets);
 * *Weight* → record it on the cat's weight page (the item completes from the server's own event);
 * *Consent* → the consent capture screen; *ASA grade* → the vet's control. A row is never completed by
 * the browser: it asks, and shows the refetched answer.
 */
export function PreOpChecklist({ surgery }: { surgery: SurgeryDetail }) {
  const roles = useStaffRoles();
  const open = surgery.status === 'Scheduled' || surgery.status === 'PreOp';
  const canManage = open && roles.has('Nurse', 'Vet');
  const isVet = open && roles.has('Vet');
  const [issuing, setIssuing] = useState(false);
  const [how, setHow] = useState<(typeof HOW)[number]['value']>('message');
  const [marking, setMarking] = useState(false);
  const [waiving, setWaiving] = useState(false);
  const [doneNote, setDoneNote] = useState('');
  const complete = useCompleteChecklistItem(surgery.id);
  const waive = useWaiveChecklistItem(surgery.id);
  const zone = surgery.branchTimeZoneId;
  const consentLink = (kind: 'Surgery' | 'Anaesthesia') =>
    `/staff/consents/new?catId=${surgery.catId}&appointmentId=${surgery.appointmentId}&kind=${kind}`;

  function actions(item: ChecklistItem) {
    if (item.status !== 'Outstanding' && item.kind !== 'AsaGradeAssessed') return null;
    switch (item.kind) {
      case 'FastingInstructionsIssued':
        return canManage ? (
          <Button variant="secondary" size="sm" onClick={() => setIssuing(true)}>
            Mark issued
          </Button>
        ) : null;
      case 'PreAnaestheticBloodWork':
        return (
          <>
            {canManage && (
              <Button variant="secondary" size="sm" onClick={() => setMarking(true)}>
                Mark done
              </Button>
            )}
            {isVet && (
              <Button variant="secondary" size="sm" onClick={() => setWaiving(true)}>
                Waive
              </Button>
            )}
          </>
        );
      case 'WeightRecorded':
        return canManage ? (
          <Link
            href={`/staff/cats/${surgery.catId}/weights`}
            className={buttonVariants({ variant: 'secondary', size: 'sm' })}
          >
            Record weight
          </Link>
        ) : null;
      case 'ConsentSigned':
        return roles.has('Reception', 'Nurse', 'Vet') && open ? (
          <>
            <Link
              href={consentLink('Surgery')}
              className={buttonVariants({ variant: 'secondary', size: 'sm' })}
            >
              Record surgery consent
            </Link>
            <Link
              href={consentLink('Anaesthesia')}
              className={buttonVariants({ variant: 'secondary', size: 'sm' })}
            >
              Record anaesthesia consent
            </Link>
          </>
        ) : null;
      case 'AsaGradeAssessed':
        return isVet ? <AsaGradeControl surgery={surgery} /> : null;
      default:
        return null;
    }
  }

  return (
    <section aria-labelledby="srg-checklist" className="flex flex-col gap-3">
      <h2 id="srg-checklist" className="font-display text-lg font-semibold text-ink">
        Pre-op checklist
      </h2>
      <ul className="flex flex-col divide-y divide-border rounded-[var(--radius-md)] border border-border bg-surface">
        {surgery.checklist.map((item) => {
          const by = knownName(surgery, item.completedBy);
          return (
            <li
              key={item.kind}
              id={`checklist-${item.kind}`}
              className="flex flex-col gap-2 p-3 sm:flex-row sm:items-start sm:justify-between"
            >
              <div className="flex min-w-0 flex-col gap-1">
                <p className="text-sm font-semibold text-ink">{checklistLabel(item.kind)}</p>
                <p className="text-sm">
                  <StatusText item={item} />
                  {item.completedOn && (
                    <span className="text-ink-muted">
                      {' '}
                      · {formatAppointmentWhen(item.completedOn, zone)}
                      {by ? ` · ${by}` : ''}
                    </span>
                  )}
                </p>
                {item.kind === 'AsaGradeAssessed' && surgery.asaGrade && (
                  <p className="text-sm text-ink">
                    Grade {surgery.asaGrade}
                    {surgery.asaIsEmergency ? ' (Emergency)' : ''}
                  </p>
                )}
                {item.note && <p className="text-sm text-ink-muted">{item.note}</p>}
                {item.waiverReason && (
                  <p className="text-sm text-ink-muted">Waived because: {item.waiverReason}</p>
                )}
              </div>
              <div className="flex flex-wrap items-start gap-2">{actions(item)}</div>
            </li>
          );
        })}
      </ul>

      <ActionDialog
        open={issuing}
        onOpenChange={setIssuing}
        title="Fasting instructions issued"
        consequence="Record that the owner has the fasting instructions."
        confirmLabel="Mark issued"
        describeError={describeSurgeryError}
        onConfirm={async () => {
          await complete.mutateAsync({
            kind: 'FastingInstructionsIssued',
            note: HOW.find((h) => h.value === how)?.label,
          });
          setIssuing(false);
          toast({ title: 'Fasting instructions marked as issued' });
        }}
      >
        <fieldset className="flex flex-col gap-2">
          <legend className="text-sm font-semibold text-ink">How were they given?</legend>
          {HOW.map((option) => (
            <label key={option.value} className="flex items-center gap-2 text-sm text-ink">
              <input
                type="radio"
                name="fasting-how"
                checked={how === option.value}
                onChange={() => setHow(option.value)}
              />
              {option.label}
            </label>
          ))}
        </fieldset>
      </ActionDialog>

      <ActionDialog
        open={marking}
        onOpenChange={setMarking}
        title="Blood work done"
        consequence="Record that the pre-anaesthetic blood work is done."
        confirmLabel="Mark done"
        describeError={describeSurgeryError}
        onConfirm={async () => {
          await complete.mutateAsync({
            kind: 'PreAnaestheticBloodWork',
            note: doneNote.trim() || undefined,
          });
          setMarking(false);
          setDoneNote('');
          toast({ title: 'Blood work marked as done' });
        }}
      >
        <TextAreaBox
          id="bloodwork-note"
          label="Note (optional)"
          value={doneNote}
          onChange={setDoneNote}
          max={SURGERY_LIMITS.noteMax}
        />
      </ActionDialog>

      <ReasonDialog
        open={waiving}
        onOpenChange={setWaiving}
        title="Waive the blood work?"
        consequence="The surgery can start without pre-anaesthetic blood work. The waiver and your reason are kept on the record."
        reasonLabel="Reason for waiving"
        confirmLabel="Waive blood work"
        describeError={describeSurgeryError}
        onConfirm={async (reason) => {
          await waive.mutateAsync({ kind: 'PreAnaestheticBloodWork', reason });
          setWaiving(false);
          toast({ title: 'Blood work waived' });
        }}
      />
    </section>
  );
}
