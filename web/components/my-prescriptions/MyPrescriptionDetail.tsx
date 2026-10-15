'use client';

import * as React from 'react';
import Link from 'next/link';
import { CalendarPlus, CheckCircle2, Info } from 'lucide-react';
import { Button, buttonVariants } from '@/components/ui/button';
import { ClinicalText, Quantity } from '@/components/common/Quantity';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { DefinitionList, Panel } from '@/components/inventory/fields';
import type { MyPrescriptionDetail as DetailData } from '@/lib/api/my-prescriptions';
import { describePharmacyError } from '@/lib/api/errors';
import { formatDay, formatInstantInZone, formLabel } from '@/lib/format/pharmacy';
import { formatRemaining } from '@/lib/format/units';
import { RepeatRequestDialog } from './RepeatRequestDialog';

/**
 * ⭐ `MyPrescriptionDetail` — HO-PHM-002 §11.8 (US-PHM-06, 07; ADR-FE-066). The directions and
 * instructions, the collection history (date, branch, quantity — **never a batch number**) and the
 * repeat area, which is drawn ONLY from the server's `repeat` block:
 *  - `canRequest` → **Request a repeat** (a dialog: branch, optional note, confirm);
 *  - otherwise `cannotRequestReason` is shown VERBATIM, with **Book a consultation** when the reason is
 *    that the validity ended or no repeats are left (US-PHM-06 AC2) — the owner is told why, not just
 *    shown a missing button;
 *  - an open request shows **Withdraw request**.
 * `autoOpen` (the refill reminder's `?action=repeat`) opens the dialog on load when it is allowed.
 */
const OFFER_CONSULTATION = new Set(['PrescriptionValidityEnded', 'NoRepeatsRemaining']);

export function MyPrescriptionDetail({
  detail,
  zone,
  branches,
  autoOpen,
  onRequest,
  onWithdraw,
}: {
  detail: DetailData;
  zone: string;
  branches: Array<{ id: string; name: string }>;
  autoOpen: boolean;
  onRequest: (input: { preferredBranchId: string; note: string | null }) => Promise<unknown>;
  onWithdraw: (requestId: string) => Promise<unknown>;
}) {
  const { repeat } = detail;
  const [dialogOpen, setDialogOpen] = React.useState(autoOpen && repeat.canRequest);
  const [withdrawOpen, setWithdrawOpen] = React.useState(false);
  const [requested, setRequested] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const bookHref = `/book?catId=${detail.catId}`;

  return (
    <article
      aria-label={`${detail.medicine.name} for ${detail.catName}`}
      className="flex flex-col gap-4"
    >
      <header>
        <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">
          {detail.catName}
        </p>
        <h1 className="font-display text-2xl font-semibold text-ink">{detail.medicine.name}</h1>
        <p className="text-sm text-ink-muted">
          <ClinicalText>{detail.medicine.strength}</ClinicalText>, {formLabel(detail.medicine.form)}
        </p>
      </header>

      <Panel title="How to give it" headingId="mp-how">
        <DefinitionList
          items={[
            {
              label: 'Dose',
              value: (
                <>
                  <Quantity value={detail.dose} /> {detail.frequency}
                </>
              ),
            },
            {
              label: 'Left',
              value: detail.isActive ? formatRemaining(detail.remainingDays) : 'Finished',
            },
            {
              label: 'Prescribed by',
              value: `${detail.issuingVetName}, ${formatDay(detail.issuedOn)}`,
            },
          ]}
        />
        <h2 className="mb-1 mt-3 text-xs font-bold uppercase tracking-wide text-ink-muted">
          Instructions
        </h2>
        <p className="whitespace-pre-wrap break-words text-sm text-ink">{detail.instructions}</p>
      </Panel>

      <Panel title="Repeats" headingId="mp-repeat">
        {requested && (
          <p
            role="status"
            className="mb-3 flex items-center gap-2 text-sm font-semibold text-accent-ink"
          >
            <CheckCircle2 size={16} aria-hidden="true" />
            Requested. We’ll message you when it’s ready to collect.
          </p>
        )}
        {repeat.openRequest ? (
          <div className="flex flex-col gap-3 text-sm">
            <p className="text-ink">
              You asked for a repeat on{' '}
              {formatInstantInZone(repeat.openRequest.requestedOn, zone).split(',')[0]}. We’ll
              message you when it’s ready to collect.
            </p>
            <div>
              <Button variant="secondary" onClick={() => setWithdrawOpen(true)}>
                Withdraw request
              </Button>
            </div>
          </div>
        ) : repeat.canRequest ? (
          <div className="flex flex-col gap-2 text-sm">
            <p className="text-ink">
              {repeat.suppliesLeft} {repeat.suppliesLeft === 1 ? 'repeat' : 'repeats'} left, until{' '}
              {formatDay(repeat.validUntil)}. You pay at collection.
            </p>
            <div>
              <Button onClick={() => setDialogOpen(true)}>
                <CalendarPlus size={14} aria-hidden="true" />
                Request a repeat
              </Button>
            </div>
          </div>
        ) : (
          <div className="flex flex-col gap-3 text-sm">
            <p className="flex items-start gap-2 text-ink">
              <Info size={16} className="mt-0.5 flex-none text-ink-muted" aria-hidden="true" />
              <span className="whitespace-pre-wrap break-words">
                {repeat.cannotRequestReason ??
                  'A repeat cannot be requested for this prescription.'}
              </span>
            </p>
            {repeat.cannotRequestCode && OFFER_CONSULTATION.has(repeat.cannotRequestCode) && (
              <div>
                <Link
                  href={bookHref}
                  className={buttonVariants({ variant: 'primary', size: 'md' })}
                >
                  Book a consultation
                </Link>
              </div>
            )}
          </div>
        )}
        {error && (
          <p role="alert" className="mt-2 text-sm font-semibold text-danger-ink">
            {error}
          </p>
        )}
      </Panel>

      <Panel title="Collected so far" headingId="mp-history">
        {detail.dispensings.length === 0 ? (
          <p className="text-sm text-ink-muted">Not collected yet.</p>
        ) : (
          <ul className="m-0 flex list-none flex-col gap-1 p-0 text-sm">
            {detail.dispensings.map((dispensing, index) => (
              <li
                key={`${dispensing.dispensedOn}-${index}`}
                className="flex flex-wrap items-center gap-x-3"
              >
                <span>{formatDay(dispensing.dispensedOn)}</span>
                <span>{dispensing.branchName}</span>
                <Quantity value={dispensing.quantity} />
              </li>
            ))}
          </ul>
        )}
      </Panel>

      <RepeatRequestDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        medicineName={detail.medicine.name}
        branches={branches}
        onRequest={async (input) => {
          await onRequest(input);
          setRequested(true);
        }}
      />
      <ConfirmDialog
        open={withdrawOpen}
        onOpenChange={setWithdrawOpen}
        title="Withdraw your repeat request?"
        consequence="We will stop preparing it. You can ask again later if you still need it."
        confirmLabel="Withdraw request"
        variant="destructive"
        onConfirm={async () => {
          try {
            await onWithdraw((repeat.openRequest as NonNullable<typeof repeat.openRequest>).id);
            setWithdrawOpen(false);
          } catch (failure) {
            setWithdrawOpen(false);
            setError(describePharmacyError(failure));
          }
        }}
      />
    </article>
  );
}
