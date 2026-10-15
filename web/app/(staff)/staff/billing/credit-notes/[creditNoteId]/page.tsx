'use client';

import { use } from 'react';
import { CreditNoteView } from '@/components/billing/CreditNoteScreens';

/** /staff/billing/credit-notes/[creditNoteId] - HO-BIL-002 section 11.3: the credit note as issued. */
export default function CreditNoteDetailPage({ params }: { params: Promise<{ creditNoteId: string }> }) {
  const { creditNoteId } = use(params);
  return <CreditNoteView creditNoteId={creditNoteId} />;
}
