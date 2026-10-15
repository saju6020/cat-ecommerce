'use client';

import { useState } from 'react';
import { History, FileText } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { formatInZone } from '@/lib/format/time';

/** `TemplatePrefillNotice` — HO-EMR-002 §11.1 (US-CLN-20). The note stays fully editable. */
export function TemplatePrefillNotice({ templateName }: { templateName: string }) {
  return (
    <div
      role="status"
      className="flex items-start gap-2 rounded-[var(--radius-md)] border border-border bg-accent-soft p-3 text-sm text-accent-ink"
    >
      <FileText size={16} className="mt-0.5 flex-none" aria-hidden="true" />
      <p>Pre-filled from the {templateName} template. Edit freely.</p>
    </div>
  );
}

/**
 * `RestoredDraftNotice` — HO-EMR-002 §11.1 (US-CLN-12 AC2). Says WHERE the text came from — the server's
 * working copy, never the browser — and offers *Revert to saved note*, confirmed (UI-3) with what is
 * discarded.
 */
export function RestoredDraftNotice({
  savedOn,
  zone,
  hasSavedNote,
  onRevert,
}: {
  savedOn: string;
  zone: string;
  hasSavedNote: boolean;
  onRevert: () => void;
}) {
  const [confirmOpen, setConfirmOpen] = useState(false);
  const when = formatInZone(savedOn, zone, { timeStyle: 'short' });
  return (
    <div
      role="status"
      className="flex flex-wrap items-center justify-between gap-3 rounded-[var(--radius-md)] border border-border bg-accent-soft p-3 text-sm text-accent-ink"
    >
      <p className="flex items-start gap-2">
        <History size={16} className="mt-0.5 flex-none" aria-hidden="true" />
        <span>Unsaved changes from {when} were restored from your working copy on the server.</span>
      </p>
      <Button variant="secondary" size="sm" onClick={() => setConfirmOpen(true)}>
        Revert to saved note
      </Button>
      <ConfirmDialog
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        title="Revert to the saved note?"
        consequence={`Everything you typed since the note was last saved — the restored changes from ${when} — will be discarded.${hasSavedNote ? ' The note and problem list go back to the last saved version.' : ' The note goes back to empty.'}`}
        confirmLabel="Revert to saved note"
        variant="destructive"
        onConfirm={() => {
          onRevert();
          setConfirmOpen(false);
        }}
      />
    </div>
  );
}
