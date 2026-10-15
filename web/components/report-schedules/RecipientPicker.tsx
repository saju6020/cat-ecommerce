import { CircleAlert } from 'lucide-react';
import type { EligibleRecipient, ReportScheduleRecipient } from '@/lib/api/report-schedules';

/**
 * `components/report-schedules/RecipientPicker.tsx` — HO-RPT-002 §11.5, U-E2. Checkbox list fed by
 * `…/recipients`, each with roles. Someone with no email is unselectable, with the reason as text.
 * `kept` are already-saved recipients the server no longer lists (no longer allowed to open the
 * report): shown ticked so they can be removed, with the reason.
 */
export function RecipientPicker({
  eligible,
  kept,
  selected,
  onChange,
  error,
}: {
  eligible: EligibleRecipient[];
  kept: ReportScheduleRecipient[];
  selected: string[];
  onChange: (ids: string[]) => void;
  error?: string;
}) {
  const eligibleIds = new Set(eligible.map((person) => person.id));
  const stale = kept.filter(
    (person) => !eligibleIds.has(person.id) && selected.includes(person.id),
  );

  function toggle(id: string, checked: boolean) {
    onChange(checked ? [...selected, id] : selected.filter((current) => current !== id));
  }

  return (
    <fieldset
      className="m-0 min-w-0 border-0 p-0"
      aria-describedby={error ? 'recipients-error' : undefined}
    >
      <legend className="mb-1 text-sm font-semibold text-ink">
        Recipients <span className="font-normal text-ink-muted">· {selected.length} chosen</span>
      </legend>
      <div className="flex max-h-72 flex-col gap-1 overflow-y-auto rounded-[var(--radius-sm)] border border-border p-2">
        {eligible.length === 0 && stale.length === 0 && (
          <p className="m-0 text-sm text-ink-muted">
            No staff can receive this report for this branch.
          </p>
        )}
        {eligible.map((person) => {
          const noteId = `recipient-${person.id}-note`;
          return (
            <label key={person.id} className="flex items-start gap-2 py-1 text-sm">
              <input
                type="checkbox"
                className="mt-1"
                checked={selected.includes(person.id)}
                disabled={!person.emailPresent}
                aria-describedby={person.emailPresent ? undefined : noteId}
                onChange={(event) => toggle(person.id, event.target.checked)}
              />
              <span className="min-w-0">
                <span className="font-semibold text-ink">{person.name}</span>
                <span className="text-ink-muted"> · {person.roles.join(', ')}</span>
                {!person.emailPresent && (
                  <span
                    id={noteId}
                    className="mt-0.5 flex items-center gap-1 text-xs text-ink-muted"
                  >
                    <CircleAlert size={12} aria-hidden="true" />
                    Can&rsquo;t be chosen: no email address on their staff account.
                  </span>
                )}
              </span>
            </label>
          );
        })}
        {stale.map((person) => (
          <label key={person.id} className="flex items-start gap-2 py-1 text-sm">
            <input
              type="checkbox"
              className="mt-1"
              checked
              onChange={() => toggle(person.id, false)}
            />
            <span className="min-w-0">
              <span className="font-semibold text-ink">{person.name}</span>
              <span className="flex items-center gap-1 text-xs text-warning-ink">
                <CircleAlert size={12} aria-hidden="true" />
                Can&rsquo;t open this report any more. Untick to remove.
              </span>
            </span>
          </label>
        ))}
      </div>
      <p className="mt-1 text-xs text-ink-muted">Only staff who can open this report are listed.</p>
      {error && (
        <p
          id="recipients-error"
          role="alert"
          className="mt-1 text-xs font-semibold text-danger-ink"
        >
          {error}
        </p>
      )}
    </fieldset>
  );
}
