'use client';

import * as React from 'react';
import { Check } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ActionDialog } from '@/components/inventory/ReasonDialog';
import { TextField } from '@/components/inventory/fields';
import { CONSIGNMENT_STATUS_LABELS } from '@/components/inventory/labels';
import type { ConsignmentStatus } from '@/lib/api/procurement-consignments';
import { cn } from '@/lib/utils';

const STEPS: ConsignmentStatus[] = ['Planned', 'InTransit', 'AtCustoms', 'Cleared'];
const SLUG_TO_STATUS: Record<string, ConsignmentStatus> = {
  'in-transit': 'InTransit',
  'at-customs': 'AtCustoms',
  cleared: 'Cleared',
};

/**
 * `components/procurement/ConsignmentStatusStepper.tsx` — HO-IMP-002 §11.2.
 * `Planned → In transit → At customs → Cleared`, each advance confirmed with the date it took
 * effect. The next step comes from the server's `allowedActions` (`advance:{next}`), never from a
 * client rule. A refusal — `ClearanceDocumentsMissing` — is shown in the dialog and, by the page,
 * against the missing number field.
 */
export function ConsignmentStatusStepper({
  status,
  allowedActions,
  today,
  onAdvance,
}: {
  status: ConsignmentStatus;
  allowedActions: string[];
  today: string;
  onAdvance: (to: ConsignmentStatus, on: string) => Promise<unknown>;
}) {
  const [open, setOpen] = React.useState(false);
  const [on, setOn] = React.useState(today);
  const advance = allowedActions.find((a) => a.startsWith('advance:'));
  const next = advance ? SLUG_TO_STATUS[advance.slice('advance:'.length)] : undefined;
  const currentIndex = STEPS.indexOf(status);

  return (
    <div className="flex flex-col gap-3">
      {status === 'Cancelled' ? (
        <p className="text-sm font-semibold text-danger-ink">Cancelled</p>
      ) : (
        <ol className="flex flex-wrap gap-2" aria-label="Consignment progress">
          {STEPS.map((step, index) => {
            const done = index < currentIndex;
            const current = index === currentIndex;
            return (
              <li
                key={step}
                aria-current={current ? 'step' : undefined}
                className={cn(
                  'flex items-center gap-1.5 rounded-full border px-3 py-1 text-sm',
                  current ? 'border-accent bg-accent-soft font-semibold text-accent-ink' : 'border-border text-ink-muted',
                )}
              >
                {done && <Check size={14} aria-hidden="true" />}
                <span>
                  {index + 1}. {CONSIGNMENT_STATUS_LABELS[step]}
                </span>
                {done && <span className="sr-only">(done)</span>}
                {current && <span className="sr-only">(current)</span>}
              </li>
            );
          })}
        </ol>
      )}
      {next && (
        <div>
          <Button onClick={() => { setOn(today); setOpen(true); }}>Mark as {CONSIGNMENT_STATUS_LABELS[next].toLowerCase()}</Button>
        </div>
      )}
      {next && (
        <ActionDialog
          open={open}
          onOpenChange={setOpen}
          title={`Mark as ${CONSIGNMENT_STATUS_LABELS[next].toLowerCase()}`}
          consequence={
            next === 'Cleared'
              ? 'Clearing needs the import permit, health certificate and customs declaration numbers. Once cleared, goods can be received against this consignment.'
              : `The consignment moves from ${CONSIGNMENT_STATUS_LABELS[status].toLowerCase()} to ${CONSIGNMENT_STATUS_LABELS[next].toLowerCase()}. Steps cannot be skipped or undone.`
          }
          confirmLabel={`Mark as ${CONSIGNMENT_STATUS_LABELS[next].toLowerCase()}`}
          confirmDisabled={on === ''}
          onConfirm={() => onAdvance(next, on)}
        >
          <TextField id="cn-advance-on" label="Effective on" type="date" value={on} onChange={(e) => setOn(e.target.value)} />
        </ActionDialog>
      )}
    </div>
  );
}
