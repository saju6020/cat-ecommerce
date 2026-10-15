'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';

export type RefusalKind = 'none' | 'expired' | 'other-person';

/**
 * `components/offline/OfflineBoardRefusal.tsx` — HO-XCT-002 §11.4, U-A11. When a saved copy is
 * missing, out of date or belongs to someone else, NO data is shown: each case gets its own
 * explanation and one button that only re-checks the connection.
 */
export function OfflineBoardRefusal({
  kind,
  savedDate,
  onReconnect,
}: {
  kind: RefusalKind;
  /** Display text for the date of an out-of-date copy. */
  savedDate?: string;
  onReconnect: () => Promise<void>;
}) {
  const [checking, setChecking] = useState(false);
  const copy = {
    none: {
      title: 'No offline copy on this device.',
      body: "This device hasn't saved today's lists — either the offline copy isn't turned on here, or it was cleared when someone signed out. Nothing can be shown here until the connection returns; after that, turn the offline copy on for next time.",
    },
    expired: {
      title: `The saved copy is from ${savedDate ?? 'an earlier day'} and is out of date.`,
      body: "It isn't shown because today's appointments and tasks may be different. Once the connection returns, today's copy is saved automatically.",
    },
    'other-person': {
      title: 'This copy was saved by someone else.',
      body: "An offline copy only opens for the person who saved it, so it isn't shown here. When the connection returns, sign in as yourself — the other person's copy is removed and yours is saved.",
    },
  }[kind];

  return (
    <section
      aria-labelledby="offline-refusal-title"
      className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-6"
    >
      <h2 id="offline-refusal-title" className="text-[15px] font-semibold text-ink">
        {copy.title}
      </h2>
      <p className="text-sm text-ink-muted">{copy.body}</p>
      <div>
        <Button
          variant="secondary"
          size="sm"
          disabled={checking}
          onClick={async () => {
            setChecking(true);
            try {
              await onReconnect();
            } finally {
              setChecking(false);
            }
          }}
        >
          {checking ? 'Checking…' : 'Try to reconnect'}
        </Button>
      </div>
    </section>
  );
}
