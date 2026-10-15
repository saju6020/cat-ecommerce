'use client';

import { use } from 'react';
import { InvoiceDetailScreen } from '@/components/billing/InvoiceDetailScreen';

/** `/staff/billing/invoices/[invoiceId]` — HO-BIL-002 §11.2–§11.3: a draft is the composer, anything
 *  else the issued document. */
export default function InvoicePage({ params }: { params: Promise<{ invoiceId: string }> }) {
  const { invoiceId } = use(params);
  return <InvoiceDetailScreen invoiceId={invoiceId} />;
}
