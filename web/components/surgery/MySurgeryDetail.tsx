'use client';

import Link from 'next/link';
import { CalendarPlus } from 'lucide-react';
import { PrintSheet } from '@/components/print/PrintSheet';
import { Banner, Panel } from '@/components/inventory/fields';
import { buttonVariants } from '@/components/ui/button';
import { formatAppointmentWhen } from '@/lib/format/appointment';
import { followUpLabel } from '@/lib/format/surgery';
import { formatDateOnly } from '@/lib/format/time';
import { useClinicBranches } from '@/lib/hooks/useBooking';
import { BOOK_APPOINTMENT } from '@/lib/routing/portal-links';
import type { MySurgeryDetail as Detail } from '@/lib/api/my-surgeries';
import { SurgeryStatusBadge } from './SurgeryStatusBadge';

/** The server's `bookFollowUpLink` is absolute (`{web}/book?catId=…`); the portal links to its own `/book`. */
export function followUpBookingPath(link: string, catId: string): string {
  try {
    const url = new URL(link, 'https://portal.invalid');
    if (url.pathname === BOOK_APPOINTMENT) return `${BOOK_APPOINTMENT}${url.search}`;
  } catch {
    /* fall through to the cat's own link */
  }
  return `${BOOK_APPOINTMENT}?catId=${encodeURIComponent(catId)}&category=FollowUp`;
}

/**
 * ⭐ `MySurgeryDetail` — HO-SRG-002 §11.9, OQ-3, US-SRG-07. Before the surgery: the date, **admit by**
 * time, fasting instructions (text) and where the clinic is. While the cat is in care: *"{Cat} is in our
 * care. Call {branch} on {phone} for an update."* After discharge: the post-op instructions, the
 * follow-ups with their dates and **Book follow-up**, and **Print**. No payment control exists on this
 * page, and no recovery progress or clinical note is ever shown to the owner.
 */
export function MySurgeryDetail({ surgery }: { surgery: Detail }) {
  const branches = useClinicBranches();
  const branch = branches.data?.find((b) => b.name === surgery.branch);
  const zone = surgery.branchTimeZoneId;
  const contact = (
    <p className="text-sm text-ink">
      {surgery.branch}
      {branch?.address ? `, ${branch.address}` : ''}
      {branch?.phone ? (
        <>
          {' · '}
          <a href={`tel:${branch.phone}`} className="font-semibold text-accent-ink underline">
            {branch.phone}
          </a>
        </>
      ) : null}
    </p>
  );

  if (surgery.status === 'Discharged') {
    const followUps = surgery.followUps.filter((f) => f.status !== 'Cancelled');
    return (
      <div className="flex flex-col gap-5">
        <PrintSheet
          title={`${surgery.catName} — ${surgery.procedure}`}
          description={`Surgery on ${formatAppointmentWhen(surgery.startsOn, zone)}`}
          back={{ href: '/portal/surgeries', label: 'Back to my surgeries' }}
          size="A4"
          label={`Care instructions for ${surgery.catName}`}
        >
          <div className="flex flex-col gap-4">
            <h2 className="font-display text-lg font-semibold">
              Caring for {surgery.catName} after {surgery.procedure.toLowerCase()}
            </h2>
            <p className="whitespace-pre-wrap break-words">
              {surgery.postOpInstructions || 'The clinic has not added instructions.'}
            </p>
            <section aria-labelledby="my-followups">
              <h3 id="my-followups" className="font-semibold">
                Follow-up visits
              </h3>
              {followUps.length === 0 ? (
                <p>No follow-up visits are needed.</p>
              ) : (
                <ul className="list-disc pl-5">
                  {followUps.map((f) => (
                    <li key={`${f.kind}-${f.dueOn}`}>
                      {followUpLabel(f.kind)} — {formatDateOnly(f.dueOn)} ({f.status})
                    </li>
                  ))}
                </ul>
              )}
            </section>
            <section aria-labelledby="my-contact">
              <h3 id="my-contact" className="font-semibold">
                If you are worried
              </h3>
              {contact}
            </section>
          </div>
        </PrintSheet>
        {followUps.some((f) => f.status === 'Pending') && (
          <div>
            <Link
              href={followUpBookingPath(surgery.bookFollowUpLink, surgery.catId)}
              className={buttonVariants({ variant: 'primary' })}
            >
              <CalendarPlus size={16} aria-hidden="true" />
              Book follow-up
            </Link>
          </div>
        )}
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-col gap-2">
        <Link
          href="/portal/surgeries"
          className="w-fit text-sm font-semibold text-accent-ink underline"
        >
          ← My surgeries
        </Link>
        <h1 className="font-display text-2xl font-semibold text-ink">
          {surgery.catName} — {surgery.procedure}
        </h1>
        <p className="flex flex-wrap items-center gap-3 text-sm text-ink-muted">
          <SurgeryStatusBadge status={surgery.status} />
          {formatAppointmentWhen(surgery.startsOn, zone)}
        </p>
      </div>

      {surgery.status === 'InCare' && (
        <Banner tone="info" role="status">
          {surgery.catName} is in our care. Call {surgery.branch}
          {branch?.phone ? (
            <>
              {' on '}
              <a href={`tel:${branch.phone}`} className="font-semibold underline">
                {branch.phone}
              </a>
            </>
          ) : null}{' '}
          for an update.
        </Banner>
      )}
      {surgery.status === 'Cancelled' && (
        <Banner tone="neutral">
          This surgery was cancelled. The clinic will be in touch to rebook.
        </Banner>
      )}
      {surgery.status === 'Scheduled' && (
        <>
          <Panel title={`Bringing ${surgery.catName}`} headingId="my-bring">
            <p className="text-sm text-ink">
              Please bring {surgery.catName} to {surgery.branch} by{' '}
              <strong>{formatAppointmentWhen(surgery.admitBy, zone)}</strong>. The surgery is at{' '}
              {formatAppointmentWhen(surgery.startsOn, zone)}.
            </p>
            <div className="mt-2">{contact}</div>
          </Panel>
          <Panel title="Fasting instructions" headingId="my-fasting">
            <p className="whitespace-pre-wrap break-words text-sm text-ink">
              {surgery.fastingInstructions}
            </p>
          </Panel>
        </>
      )}
      <p className="text-sm text-ink-muted">
        There is nothing to pay online. The price is agreed and paid at the clinic.
      </p>
    </div>
  );
}
