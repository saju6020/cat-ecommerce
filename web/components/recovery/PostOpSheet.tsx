'use client';

import { PrintSheet } from '@/components/print/PrintSheet';
import { formatAppointmentWhen } from '@/lib/format/appointment';
import { followUpLabel } from '@/lib/format/surgery';
import { formatDateOnly } from '@/lib/format/time';
import { useClinicBranches } from '@/lib/hooks/useBooking';
import type { SurgeryDetail } from '@/lib/api/surgeries';

/**
 * ⭐ `PostOpSheet` — HO-SRG-002 §11.6, ADR-FE-076, US-SRG-07 "produced". An HTML sheet with a print
 * stylesheet (the shared `PrintSheet`: no PDF library, no navigation reaches the paper): the cat, the
 * procedure and date, the post-op instructions as text, the follow-up dates and the clinic's contact
 * details. The instructions are plain text with their line breaks — never markup.
 */
export function PostOpSheet({ surgery }: { surgery: SurgeryDetail }) {
  const branches = useClinicBranches();
  const branch = branches.data?.find((b) => b.id === surgery.branchId);
  const zone = surgery.branchTimeZoneId;
  return (
    <PrintSheet
      title="Post-op sheet"
      description={`${surgery.catName} — ${surgery.procedure.name}`}
      back={{ href: `/staff/surgeries/${surgery.id}#discharge`, label: 'Back to the surgery' }}
      size="A4"
      label={`Post-op sheet for ${surgery.catName}`}
    >
      <div className="flex flex-col gap-4">
        <header>
          <p className="font-display text-xl font-semibold">{branch?.name ?? 'PurrCare'}</p>
          <h2 className="font-display text-lg font-semibold">
            Caring for {surgery.catName} after surgery
          </h2>
        </header>
        <dl className="grid grid-cols-[8rem_1fr] gap-x-3 gap-y-1">
          <dt className="font-semibold">Cat</dt>
          <dd>{surgery.catName}</dd>
          <dt className="font-semibold">Owner</dt>
          <dd>{surgery.customerName}</dd>
          <dt className="font-semibold">Procedure</dt>
          <dd>{surgery.procedure.name}</dd>
          <dt className="font-semibold">Date</dt>
          <dd>{formatAppointmentWhen(surgery.startsOn, zone)}</dd>
          <dt className="font-semibold">Surgeon</dt>
          <dd>{surgery.surgeon.name}</dd>
        </dl>
        <section aria-labelledby="sheet-instructions">
          <h3 id="sheet-instructions" className="font-semibold">
            What to do at home
          </h3>
          <p className="whitespace-pre-wrap break-words">{surgery.postOpInstructions}</p>
        </section>
        <section aria-labelledby="sheet-followups">
          <h3 id="sheet-followups" className="font-semibold">
            Follow-up visits
          </h3>
          {surgery.followUps.filter((f) => f.status !== 'Cancelled').length === 0 ? (
            <p>No follow-up visits are needed.</p>
          ) : (
            <ul className="list-disc pl-5">
              {surgery.followUps
                .filter((f) => f.status !== 'Cancelled')
                .map((f) => (
                  <li key={f.id}>
                    {followUpLabel(f.kind)} — {formatDateOnly(f.dueOn)}
                  </li>
                ))}
            </ul>
          )}
        </section>
        <section aria-labelledby="sheet-contact">
          <h3 id="sheet-contact" className="font-semibold">
            If you are worried
          </h3>
          <p>
            Call us on <strong>{branch?.phone ?? 'the number on your appointment message'}</strong>
            {branch?.address ? ` or visit ${branch.address}` : ''}.
          </p>
        </section>
      </div>
    </PrintSheet>
  );
}
