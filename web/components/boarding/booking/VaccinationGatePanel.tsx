'use client';

import Link from 'next/link';
import { ShieldAlert } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { buttonVariants } from '@/components/ui/button';
import { describeGateReason } from '@/components/boarding/shared/labels';
import type { VaccinationGate } from '@/lib/api/types';
import { isSafeInternalPath } from '@/lib/routing/isSafeInternalPath';
import { cn } from '@/lib/utils';

/**
 * `components/boarding/booking/VaccinationGatePanel.tsx` — HO-BRD-002 §11.1 step 4, US-BRD-04 AC1.
 * Rendered whenever `vaccinationGate.passed === false`: per blocked cat the reason in plain
 * language (the vaccine and the date), a **Book a vaccination** link to HO-APT-002's `/book`
 * (`bookingLink`, only if it is a site-relative path), and the promise that the booking is kept —
 * **Resume** re-checks the same enquiry once the vaccination is recorded, with nothing re-entered
 * (AC2). The flow does not lose its place. Staff with the override capability get an extra action
 * through `renderOverride`.
 */
export function VaccinationGatePanel({
  gate,
  onResume,
  resuming,
  renderOverride,
  resumeNote,
}: {
  gate: VaccinationGate;
  onResume: () => void;
  resuming: boolean;
  renderOverride?: (cat: VaccinationGate['blockedCats'][number]) => React.ReactNode;
  resumeNote?: string | null;
}) {
  return (
    <section
      aria-labelledby="gate-heading"
      className="flex flex-col gap-4 rounded-[var(--radius-md)] border border-danger-ink/30 bg-danger-bg p-5"
    >
      <div className="flex items-start gap-3">
        <ShieldAlert size={22} className="mt-0.5 flex-none text-danger-ink" aria-hidden="true" />
        <div>
          <h2 id="gate-heading" className="font-display text-lg font-semibold text-danger-ink">
            A vaccination needs attention before this booking can go ahead
          </h2>
          <p className="mt-1 text-sm text-ink">
            We’ve kept your booking — book the vaccination, then come back and press{' '}
            <strong>Resume</strong>. Nothing needs to be entered again.
          </p>
        </div>
      </div>

      <ul aria-label="Cats that need a vaccination" className="flex flex-col gap-3">
        {gate.blockedCats.map((cat) => (
          <li
            key={cat.catId}
            className="rounded-[var(--radius-sm)] border border-border bg-surface p-4"
          >
            <p className="text-[15px] font-semibold text-ink">{cat.catName}</p>
            <ul className="mt-1 list-disc pl-5 text-sm text-ink">
              {cat.reasons.map((reason, index) => (
                <li key={index}>{describeGateReason(cat.catName, reason)}</li>
              ))}
            </ul>
            <div className="mt-3 flex flex-wrap gap-2">
              {isSafeInternalPath(cat.bookingLink) && (
                <Link
                  href={cat.bookingLink}
                  className={cn(buttonVariants({ variant: 'primary', size: 'md' }))}
                >
                  Book a vaccination for {cat.catName}
                </Link>
              )}
              {renderOverride?.(cat)}
            </div>
          </li>
        ))}
      </ul>

      {resumeNote && (
        <p role="status" className="text-sm font-semibold text-danger-ink">
          {resumeNote}
        </p>
      )}
      <div>
        <Button variant="secondary" onClick={onResume} disabled={resuming} aria-busy={resuming}>
          {resuming ? 'Checking…' : 'Resume'}
        </Button>
      </div>
    </section>
  );
}
