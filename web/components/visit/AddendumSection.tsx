'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { ClinicalText } from './ClinicalText';
import { formatInZone } from '@/lib/format/time';
import { describeClinicalError } from '@/lib/api/errors';
import { MAX_ADDENDUM, MAX_ADDENDUM_REASON } from '@/lib/validation/clinical';
import type { VisitAddendum } from '@/lib/api/visits';

/**
 * `AddendumList` · `AddendumForm` — HO-EMR-002 §11.1 (US-CLN-17 AC2, US-CLN-22). Corrections to a
 * finalised visit: a timestamped, attributed, append-only list (the original text above it never
 * changes), and a form that exists ONLY when the server's `allowedActions` has `addAddendum`. The
 * submit confirms with *"Addenda cannot be edited or removed."* (UI-3).
 */
export function AddendumList({ addenda, zone }: { addenda: VisitAddendum[]; zone: string }) {
  return (
    <section aria-labelledby="addenda-heading" className="flex flex-col gap-3">
      <h2 id="addenda-heading" className="text-sm font-bold uppercase tracking-wide text-ink-muted">
        Addenda
      </h2>
      {addenda.length === 0 ? (
        <p className="text-sm text-ink-muted">
          No addenda. Corrections to this record appear here.
        </p>
      ) : (
        <ol className="m-0 flex list-none flex-col gap-3 p-0">
          {addenda.map((addendum) => (
            <li
              key={addendum.id}
              className="rounded-[var(--radius-md)] border border-border bg-surface p-3"
            >
              <p className="mb-1 text-xs text-ink-muted">
                {addendum.author} · {formatInZone(addendum.addedOn, zone)}
                {addendum.reason ? ` · ${addendum.reason}` : ''}
              </p>
              <ClinicalText>{addendum.text}</ClinicalText>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}

export function AddendumForm({
  onSubmit,
}: {
  onSubmit: (text: string, reason: string) => Promise<void>;
}) {
  const [text, setText] = useState('');
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [confirmOpen, setConfirmOpen] = useState(false);

  return (
    <form
      noValidate
      className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-4"
      onSubmit={(event) => {
        event.preventDefault();
        if (!text.trim()) {
          setError('Write the addendum before adding it.');
          return;
        }
        setError(null);
        setConfirmOpen(true);
      }}
    >
      <h3 className="text-sm font-bold text-ink">Add an addendum</h3>
      <div className="flex flex-col gap-1.5">
        <label htmlFor="addendum-text" className="text-sm font-semibold text-ink">
          Addendum
        </label>
        <textarea
          id="addendum-text"
          rows={4}
          maxLength={MAX_ADDENDUM}
          value={text}
          onChange={(event) => setText(event.target.value)}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? 'addendum-error' : 'addendum-count'}
          className="w-full resize-y rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        />
        <p id="addendum-count" className="text-right text-xs text-ink-muted">
          {text.length} of {MAX_ADDENDUM} characters
        </p>
      </div>
      <div className="flex flex-col gap-1.5">
        <label htmlFor="addendum-reason" className="text-sm font-semibold text-ink">
          Reason <span className="font-normal text-ink-muted">(optional)</span>
        </label>
        <input
          id="addendum-reason"
          type="text"
          maxLength={MAX_ADDENDUM_REASON}
          value={reason}
          onChange={(event) => setReason(event.target.value)}
          className="w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        />
      </div>
      {error && (
        <p id="addendum-error" role="alert" className="text-sm font-semibold text-danger-ink">
          {error}
        </p>
      )}
      <div>
        <Button type="submit">Add addendum</Button>
      </div>
      <ConfirmDialog
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        title="Add this addendum?"
        consequence="Addenda cannot be edited or removed."
        confirmLabel="Add addendum"
        pendingLabel="Adding…"
        onConfirm={async () => {
          try {
            await onSubmit(text.trim(), reason.trim());
            setText('');
            setReason('');
            setError(null);
          } catch (caught) {
            setError(describeClinicalError(caught));
          } finally {
            setConfirmOpen(false);
          }
        }}
      />
    </form>
  );
}
