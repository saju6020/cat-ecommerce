'use client';

import { SideSheet } from '@/components/visit/SideSheet';
import { ClinicalHistoryView } from './ClinicalHistoryView';

/**
 * ⭐ `HistoryDrawer` — HO-EMR-002 §11.4, ADR-FE-049 (US-CLN-13 "without losing my draft"). A right-side
 * sheet (full screen below 1024 px) over the still-mounted workspace: opening or closing it never
 * navigates and never touches the note form. Its content is `ClinicalHistoryView`; the summary loads
 * only when the drawer first opens and each section only when its tab does (ADR-FE-056).
 */
export function HistoryDrawer({
  open,
  onOpenChange,
  catId,
  catName,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  catId: string;
  catName: string;
}) {
  return (
    <SideSheet open={open} onOpenChange={onOpenChange} title={`${catName}’s clinical history`}>
      {open && <ClinicalHistoryView catId={catId} layout="drawer" />}
    </SideSheet>
  );
}
