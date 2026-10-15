'use client';

import { use } from 'react';
import { MyInvoiceDetail } from '@/components/billing/MyInvoiceDetail';

/** /portal/billing/invoices/[invoiceId] - HO-BIL-002 section 11.11: the invoice, PDF and how to pay. */
export default function PortalInvoicePage({ params }: { params: Promise<{ invoiceId: string }> }) {
  const { invoiceId } = use(params);
  return <MyInvoiceDetail invoiceId={invoiceId} />;
}
