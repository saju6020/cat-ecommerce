'use client';

import { use } from 'react';
import { CreditNotePage } from '@/components/billing/CreditNotePage';

/** /staff/billing/invoices/[invoiceId]/credit - HO-BIL-002 section 11.3: the credit-note wizard. */
export default function InvoiceCreditPage({ params }: { params: Promise<{ invoiceId: string }> }) {
  const { invoiceId } = use(params);
  return <CreditNotePage invoiceId={invoiceId} />;
}
