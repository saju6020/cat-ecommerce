import { Phone, Mail } from 'lucide-react';

/**
 * `components/appointments/ClinicContactCard.tsx` — HO-APT-002 §11.2, **US-CLN-04 AC2**. After
 * the cut-off an appointment cannot be changed online: no Reschedule, no Cancel — the clinic's
 * contact details instead, with `tel:` and `mailto:` links, so the customer is never left at a
 * dead end.
 */
export function ClinicContactCard({
  branchName,
  phone,
  email,
  cutoffHours,
}: {
  branchName: string;
  phone: string | null;
  email: string | null;
  cutoffHours: number | null;
}) {
  return (
    <section
      aria-labelledby="contact-card-heading"
      className="flex flex-col gap-2 rounded-[var(--radius-md)] border border-border bg-warning-bg p-4 text-sm text-warning-ink"
    >
      <h2 id="contact-card-heading" className="text-[15px] font-semibold">
        This appointment can’t be changed online
      </h2>
      <p>
        {cutoffHours !== null
          ? `It’s less than ${cutoffHours} hours until your appointment, so it can’t be changed online. `
          : 'It’s too close to your appointment for it to be changed online. '}
        Please call {branchName}
        {phone ? ` on ${phone}` : ''}.
      </p>
      <ul className="m-0 flex list-none flex-wrap gap-4 p-0 font-semibold">
        {phone && (
          <li>
            <a
              href={`tel:${phone.replace(/[^+\d]/g, '')}`}
              className="inline-flex items-center gap-1.5 underline underline-offset-2"
            >
              <Phone size={15} aria-hidden="true" />
              Call {phone}
            </a>
          </li>
        )}
        {email && (
          <li>
            <a
              href={`mailto:${email}`}
              className="inline-flex items-center gap-1.5 underline underline-offset-2"
            >
              <Mail size={15} aria-hidden="true" />
              Email {email}
            </a>
          </li>
        )}
      </ul>
    </section>
  );
}
