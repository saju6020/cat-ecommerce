import { Info } from 'lucide-react';
import type { ReportNote } from '@/lib/api/reports';

/**
 * `components/reports/ReportNotes.tsx` — HO-RPT-002 §11.3. Each server `note` as a short paragraph
 * above the table, **verbatim** — this is where the margin coverage and card-terminal caveats
 * appear (HO-RPT-001 §8.6). The client never rewords, shortens or recomputes a note, and the
 * server already states the margin coverage as a note (`MarginCoverage`), so the page does not
 * compose a second sentence from the `coverage` object.
 */
export function ReportNotes({ notes }: { notes: ReportNote[] }) {
  if (notes.length === 0) return null;
  return (
    <div className="flex flex-col gap-1.5 rounded-[var(--radius-md)] border border-border bg-surface-sunken p-3">
      {notes.map((note) => (
        <p key={note.code} className="m-0 flex items-start gap-2 text-sm text-ink">
          <Info size={14} className="mt-0.5 flex-none text-ink-muted" aria-hidden="true" />
          <span>{note.text}</span>
        </p>
      ))}
    </div>
  );
}
