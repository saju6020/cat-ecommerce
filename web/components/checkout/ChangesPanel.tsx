'use client';

import { useEffect, useRef, useState } from 'react';
import type { CheckoutChange, CheckoutChangeKind } from '@/lib/api/checkout';

const GROUPS: Array<{ kinds: CheckoutChangeKind[]; heading: string }> = [
  { kinds: ['PriceIncreased', 'PriceDecreased'], heading: 'Price changes' },
  { kinds: ['QuantityReduced'], heading: 'Quantity changes' },
  { kinds: ['NowUnavailable'], heading: 'No longer available' },
  { kinds: ['PrescriptionAuthorisationExpired', 'PrescriptionCatRequired'], heading: 'Prescription food' },
];

/**
 * `components/checkout/ChangesPanel.tsx` — ⭐ HO-ORD-002 §11.3, US-ORD-02. Renders `changes[]` as
 * **sentences the server wrote, each naming the product**, grouped by kind, then one checkbox —
 * *"I've reviewed these changes."* — that the Place button depends on. The tick is **bound to the
 * quote's fingerprint** (ADR-FE-091): a new fingerprint (an option change, a refetch or a
 * `409 CheckoutQuoteChanged`) clears it and announces *"The order has changed — please review again."*
 * Nothing is conveyed by colour or position alone. With no changes the panel renders nothing.
 */
export function ChangesPanel({
  changes,
  fingerprint,
  acknowledgedFingerprint,
  onAcknowledge,
  bookHref,
  settling,
}: {
  changes: CheckoutChange[];
  fingerprint: string;
  acknowledgedFingerprint: string | null;
  onAcknowledge: (fingerprint: string | null) => void;
  bookHref: string;
  /** The quote is being refreshed for the latest choices: the tick would be cleared on arrival. */
  settling: boolean;
}) {
  const [announcement, setAnnouncement] = useState('');
  const previous = useRef(fingerprint);

  useEffect(() => {
    if (previous.current !== fingerprint) {
      if (acknowledgedFingerprint === previous.current) {
        onAcknowledge(null);
        setAnnouncement('The order has changed — please review again.');
      }
      previous.current = fingerprint;
    }
  }, [fingerprint, acknowledgedFingerprint, onAcknowledge]);

  if (changes.length === 0) return <p role="status" aria-live="polite" className="sr-only">{announcement}</p>;

  const acknowledged = acknowledgedFingerprint === fingerprint;
  return (
    <section
      role="region"
      aria-labelledby="changes-heading"
      className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-warning-ink/30 bg-warning-bg p-4 text-warning-ink"
    >
      <h3 id="changes-heading" className="font-display text-base font-semibold">
        What changed since you added these items
      </h3>
      <p role="status" aria-live="polite" className="text-sm font-semibold">
        {announcement}
      </p>
      {GROUPS.map((group) => {
        const inGroup = changes.filter((change) => group.kinds.includes(change.kind));
        if (inGroup.length === 0) return null;
        return (
          <div key={group.heading}>
            <h4 className="text-sm font-bold">{group.heading}</h4>
            <ol className="m-0 mt-1 list-decimal pl-5 text-sm">
              {inGroup.map((change) => (
                <li key={`${change.kind}-${change.lineRef}`} className="[overflow-wrap:anywhere]">
                  {change.message}
                  {(change.kind === 'PrescriptionAuthorisationExpired') && (
                    <>
                      {' '}
                      <a href={bookHref} className="font-semibold underline">
                        Book a consultation
                      </a>
                    </>
                  )}
                </li>
              ))}
            </ol>
          </div>
        );
      })}
      <label className="flex items-center gap-2 text-sm font-semibold">
        <input
          type="checkbox"
          className="h-4 w-4 accent-[var(--accent)]"
          checked={acknowledged}
          disabled={settling}
          onChange={(event) => onAcknowledge(event.target.checked ? fingerprint : null)}
        />
        I&rsquo;ve reviewed these changes.
        {settling && <span className="font-normal"> (updating…)</span>}
      </label>
    </section>
  );
}
