'use client';

import { useState } from 'react';
import { Download, Printer } from 'lucide-react';
import { Button, buttonVariants } from '@/components/ui/button';
import { ClinicalText } from './ClinicalText';
import { formatMeasurement, PRESCRIPTIONS_ON_LABEL } from '@/lib/format/clinical';
import { formatDateOnly, formatInZone } from '@/lib/format/time';
import type { DischargeSummary } from '@/lib/api/visits';

/**
 * ⭐ `DischargeSummaryView` — HO-EMR-002 §11.5 (US-CLN-16, FR-POR-3), used by the staff print page and the
 * owner's portal summary. Branch letterhead, cat, date, vet, diagnosis, treatments, medicines
 * administered, home-care instructions, follow-up date and corrections (addenda); empty lists are
 * written out ("None recorded"). When `prescriptionsAvailable` is false the sentence about the
 * prescription label is shown. **Print** uses a print stylesheet (this component hides the app chrome
 * with `@media print`); **Download PDF** is the SERVER'S PDF — the client never generates one
 * (ADR-FE-057). Clinical text is plain text (ADR-FE-058). No price or payment appears anywhere.
 */
function List({ items }: { items: string[] }) {
  if (items.length === 0) return <p className="text-sm text-ink-muted">None recorded</p>;
  return (
    <ul className="m-0 flex list-none flex-col gap-1 p-0 text-sm text-ink">
      {items.map((item, index) => (
        <li key={`${item}-${index}`} className="whitespace-pre-wrap break-words">
          {item}
        </li>
      ))}
    </ul>
  );
}

export function DischargeSummaryView({
  summary,
  pdfUrl,
  loadPdf,
  showPrint,
}: {
  summary: DischargeSummary;
  /** A plain link (the owner's portal, whose endpoint needs no branch). */
  pdfUrl?: string;
  /** Staff: fetch the PDF as a blob through `fetchStaffBlob` (sends `X-Branch-Id`). */
  loadPdf?: () => Promise<Blob>;
  showPrint: boolean;
}) {
  const [pdfError, setPdfError] = useState<string | null>(null);
  const [pdfBusy, setPdfBusy] = useState(false);
  async function downloadPdf() {
    if (!loadPdf) return;
    setPdfBusy(true);
    setPdfError(null);
    try {
      const url = URL.createObjectURL(await loadPdf());
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = 'visit-summary.pdf';
      anchor.click();
      setTimeout(() => URL.revokeObjectURL(url), 1_000);
    } catch {
      setPdfError('The PDF couldn’t be downloaded just now. Try again, or print this page.');
    } finally {
      setPdfBusy(false);
    }
  }
  const zone = summary.branchTimeZoneId;
  const heading = 'mb-1 text-sm font-bold uppercase tracking-wide text-ink-muted';
  return (
    <article
      aria-label={`Discharge summary for ${summary.catName}`}
      className="flex flex-col gap-5"
    >
      <style>{`@media print { aside, body header, nav, .no-print { display: none !important; } main { padding: 0 !important; } a { color: inherit; text-decoration: none; } }`}</style>
      {pdfError && (
        <p role="alert" className="no-print text-sm font-semibold text-danger-ink">
          {pdfError}
        </p>
      )}
      <div className="no-print flex flex-wrap gap-2">
        {showPrint && (
          <Button variant="secondary" onClick={() => window.print()}>
            <Printer size={16} aria-hidden="true" /> Print
          </Button>
        )}
        {loadPdf ? (
          <Button
            variant="secondary"
            onClick={() => void downloadPdf()}
            disabled={pdfBusy}
            aria-busy={pdfBusy}
          >
            <Download size={16} aria-hidden="true" /> Download PDF
          </Button>
        ) : (
          <a href={pdfUrl} download className={buttonVariants({ variant: 'secondary' })}>
            <Download size={16} aria-hidden="true" /> Download PDF
          </a>
        )}
      </div>

      <div className="rounded-[var(--radius-md)] border border-border bg-surface p-5 print:border-0 print:p-0">
        <div className="mb-4 border-b border-border pb-3">
          <p className="font-display text-xl font-semibold text-ink">{summary.branchName}</p>
          <p className="text-sm text-ink-muted">
            {summary.branchAddress}
            {summary.branchPhone ? ` · ${summary.branchPhone}` : ''}
          </p>
        </div>
        <h1 className="mb-3 font-display text-2xl font-semibold text-ink">
          Visit summary for {summary.catName}
        </h1>
        <dl className="mb-5 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
          <dt className="font-semibold text-ink">Date</dt>
          <dd className="m-0 text-ink-muted">{formatDateOnly(summary.visitDate)}</dd>
          <dt className="font-semibold text-ink">Seen by</dt>
          <dd className="m-0 text-ink-muted">{summary.attendingVet}</dd>
          <dt className="font-semibold text-ink">Cat</dt>
          <dd className="m-0 text-ink-muted">
            {summary.catName} · {summary.catSex} · {summary.catAge}
          </dd>
          <dt className="font-semibold text-ink">Weight at visit</dt>
          <dd className="m-0 text-ink-muted">{formatMeasurement(summary.weightAtVisit)}</dd>
        </dl>

        <div className="flex flex-col gap-4">
          <section aria-labelledby="ds-diagnosis">
            <h2 id="ds-diagnosis" className={heading}>
              Diagnosis
            </h2>
            {summary.problems.length === 0 ? (
              <p className="text-sm text-ink-muted">None recorded</p>
            ) : (
              <ul className="m-0 flex list-none flex-col gap-1 p-0 text-sm text-ink">
                {summary.problems.map((p) => (
                  <li key={`${p.code}-${p.name}`}>
                    {p.name} <span className="text-ink-muted">({p.certainty})</span>
                  </li>
                ))}
              </ul>
            )}
          </section>
          <section aria-labelledby="ds-treatments">
            <h2 id="ds-treatments" className={heading}>
              Treatments
            </h2>
            <List items={summary.treatments} />
          </section>
          <section aria-labelledby="ds-medicines">
            <h2 id="ds-medicines" className={heading}>
              Medicines given at the clinic
            </h2>
            <List items={summary.medicinesAdministered} />
            {!summary.prescriptionsAvailable && (
              <p className="mt-2 text-sm italic text-ink-muted">{PRESCRIPTIONS_ON_LABEL}</p>
            )}
          </section>
          <section aria-labelledby="ds-home">
            <h2 id="ds-home" className={heading}>
              Home-care instructions
            </h2>
            <ClinicalText>{summary.homeCareInstructions}</ClinicalText>
          </section>
          <section aria-labelledby="ds-follow">
            <h2 id="ds-follow" className={heading}>
              Follow-up
            </h2>
            <p className="text-sm text-ink">
              {summary.followUp
                ? summary.followUp.kind === 'Appointment' && summary.followUp.appointmentStartsOn
                  ? `Appointment on ${formatInZone(summary.followUp.appointmentStartsOn, zone)}`
                  : `${summary.followUp.kind === 'Appointment' ? 'Appointment' : 'Reminder'} around ${formatDateOnly(summary.followUp.dueOn)}`
                : 'No follow-up planned'}
            </p>
          </section>
          {summary.extraSections.map((section) => (
            <section key={section.title}>
              <h2 className={heading}>{section.title}</h2>
              <List items={section.lines} />
            </section>
          ))}
          {summary.corrections.length > 0 && (
            <section aria-labelledby="ds-corrections">
              <h2 id="ds-corrections" className={heading}>
                Corrections
              </h2>
              <ul className="m-0 flex list-none flex-col gap-2 p-0">
                {summary.corrections.map((c, index) => (
                  <li key={`${c.addedOn}-${index}`} className="text-sm">
                    <span className="text-xs text-ink-muted">
                      {formatInZone(c.addedOn, zone)}
                      {c.reason ? ` · ${c.reason}` : ''}
                    </span>
                    <ClinicalText>{c.text}</ClinicalText>
                  </li>
                ))}
              </ul>
            </section>
          )}
        </div>
      </div>
    </article>
  );
}
