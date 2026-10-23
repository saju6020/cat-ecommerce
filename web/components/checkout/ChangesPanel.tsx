'use client';

import { useEffect, useRef, useState } from 'react';
import { TriangleAlert } from 'lucide-react';
import { IconTile } from '@/components/hearth/IconTile';
import type { CheckoutChange, CheckoutChangeKind } from '@/lib/api/checkout';

const GROUPS: Array<{ kinds: CheckoutChangeKind[]; heading: string }> = [
  { kinds: ['PriceIncreased', 'PriceDecreased'], heading: 'Price changes' },
  { kinds: ['QuantityReduced'], heading: 'Quantity changes' },
  { kinds: ['NowUnavailable'], heading: 'No longer available' },
  {
    kinds: ['PrescriptionAuthorisationExpired', 'PrescriptionCatRequired'],
    heading: 'Prescription food',
  },
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

  if (changes.length === 0)
    return (
      <p role="status" aria-live="polite" className="sr-only">
        {announcement}
      </p>
    );

  const acknowledged = acknowledgedFingerprint === fingerprint;
  return (
    <section
      role="region"
      aria-labelledby="changes-heading"
      className="flex gap-3.5 rounded-card border border-warning-ink/40 bg-surface p-5 ring-1 ring-warning-ink/15 sm:px-6"
    >
      <IconTile shape="tile" size="md" className="bg-warning-bg text-warning-ink">
        <TriangleAlert size={20} aria-hidden="true" />
      </IconTile>
      <div className="flex min-w-0 flex-1 flex-col gap-2">
        <h3 id="changes-heading" className="m-0 mt-2.5 text-base font-bold text-ink">
          What changed since you added these items
        </h3>
        <p role="status" aria-live="polite" className="m-0 text-sm font-semibold text-warning-ink">
          {announcement}
        </p>
        {GROUPS.map((group) => {
          const inGroup = changes.filter((change) => group.kinds.includes(change.kind));
          if (inGroup.length === 0) return null;
          return (
            <div key={group.heading}>
              <h4 className="m-0 mb-1 text-[11px] font-bold uppercase tracking-[0.06em] text-ink-muted">
                {group.heading}
              </h4>
              <ul className="m-0 list-disc pl-5 text-sm text-ink">
                {inGroup.map((change) => (
                  <li
                    key={`${change.kind}-${change.lineRef}`}
                    className="mb-1.5 [overflow-wrap:anywhere]"
                  >
                    {change.message}
                    {change.kind === 'PrescriptionAuthorisationExpired' && (
                      <>
                        {' '}
                        <a href={bookHref} className="font-semibold text-accent-ink underline">
                          Book a consultation
                        </a>
                      </>
                    )}
                  </li>
                ))}
              </ul>
            </div>
          );
        })}
        <label className="mt-1 flex min-h-12 cursor-pointer items-center gap-3 rounded-2xl border border-border-strong bg-surface px-4 py-2.5 text-sm font-semibold">
          <input
            type="checkbox"
            className="size-5 flex-none accent-[var(--accent)]"
            checked={acknowledged}
            disabled={settling}
            onChange={(event) => onAcknowledge(event.target.checked ? fingerprint : null)}
          />
          <span>
            I&rsquo;ve reviewed these changes.
            {settling && <span className="font-normal"> (updating…)</span>}
          </span>
        </label>
      </div>
    </section>
  );
}
