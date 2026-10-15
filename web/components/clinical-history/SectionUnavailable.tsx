import { Hourglass } from 'lucide-react';
import { unavailableCopy } from './historyLabels';

/**
 * `SectionUnavailable` — HO-EMR-002 §11.4 (U-F2). For a section the server reports `available: false`:
 * a plain sentence chosen from the section key. It is a STATE, not an error and not an empty list, and
 * nothing is fetched for it (the page request is never made).
 */
export function SectionUnavailable({ sectionKey }: { sectionKey: string }) {
  return (
    <p
      role="status"
      className="flex items-start gap-2 rounded-[var(--radius-md)] border border-border bg-surface-sunken p-4 text-sm text-ink-muted"
    >
      <Hourglass size={16} className="mt-0.5 flex-none" aria-hidden="true" />
      {unavailableCopy(sectionKey)}
    </p>
  );
}
