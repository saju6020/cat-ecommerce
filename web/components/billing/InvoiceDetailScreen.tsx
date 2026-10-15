'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useToast } from '@/components/ui/use-toast';
import { describeBillingError } from '@/lib/api/errors';
import { invoicePdfPath } from '@/lib/api/billing-staff';
import { DownloadButton } from './DownloadButton';
import { useCustomerBillingSummary, useCustomerInvoices, useInvoice } from '@/lib/hooks/billing/useBillingQueries';
import {
  useApplyCustomerCredit,
  useCreateCorrectedDraft,
  useIssueInvoice,
  useSetCorrectedLines,
} from '@/lib/hooks/billing/useBillingMutations';
import { useTaxClasses } from '@/lib/hooks/useAdminCatalogue';
import { ApplyCreditDialog } from './ApplyCreditDialog';
import { CorrectedDraftEditor } from './CorrectedDraftEditor';
import { InvoiceActions } from './InvoiceActions';
import { InvoiceComposer } from './InvoiceComposer';
import { InvoiceDocumentView } from './InvoiceDocumentView';
import { PageHeader } from './PageHeader';
import { QueryBoundary } from './QueryBoundary';

/**
 * `components/billing/InvoiceDetailScreen.tsx` — HO-BIL-002 §11.2–§11.3, `/staff/billing/invoices/[id]`.
 * A **draft** is the composer bound to the saved draft (or, for a corrected draft, the line editor);
 * anything else is the issued document with the actions the server allows. Never an edit form for an
 * issued invoice.
 */
function CorrectionLink({ invoiceId, kind }: { invoiceId: string; kind: 'corrects' | 'correctedBy' }) {
  const other = useInvoice(invoiceId);
  return (
    <>
      {kind === 'corrects' ? 'Corrects ' : 'Corrected by '}
      <Link href={`/staff/billing/invoices/${invoiceId}`} className="font-semibold text-accent-ink hover:underline">
        {other.data?.number ?? 'another invoice'}
      </Link>
    </>
  );
}

export function InvoiceDetailScreen({ invoiceId }: { invoiceId: string }) {
  const router = useRouter();
  const { toast } = useToast();
  const invoice = useInvoice(invoiceId);
  const taxClasses = useTaxClasses();
  const customerId = invoice.data?.customerId ?? '';
  const summary = useCustomerBillingSummary(customerId);
  const customerInvoices = useCustomerInvoices(customerId);
  const applyCredit = useApplyCustomerCredit(customerId);
  const createCorrected = useCreateCorrectedDraft(invoiceId);
  const setLines = useSetCorrectedLines(invoiceId);
  const issue = useIssueInvoice(invoiceId);
  const [creditOpen, setCreditOpen] = useState(false);

  return (
    <QueryBoundary
      query={invoice}
      notFoundTitle="Invoice not found"
      notFoundDescription="This invoice may belong to another branch, or it no longer exists."
      loadingLabel="Loading the invoice…"
    >
      {(data) => {
        if (data.status === 'Draft') {
          return (
            <div className="flex flex-col gap-5">
              <PageHeader
                title={data.correctsInvoiceId ? 'Corrected draft invoice' : 'Draft invoice'}
                back={{ href: `/staff/billing/customers/${data.customerId}`, label: 'Customer billing account' }}
                description={
                  data.correctsInvoiceId ? (
                    <CorrectionLink invoiceId={data.correctsInvoiceId} kind="corrects" />
                  ) : (
                    'Not issued yet — it has no number and can still change.'
                  )
                }
              />
              {data.correctsInvoiceId ? (
                <CorrectedDraftEditor
                  key={data.version}
                  invoice={data}
                  taxClasses={taxClasses.data}
                  onSaveLines={(input) => setLines.mutateAsync(input)}
                  onIssue={(input) => issue.mutateAsync(input)}
                  onReload={() => invoice.refetch()}
                />
              ) : (
                <InvoiceComposer customerId={data.customerId} draft={data} onDraftChanged={() => invoice.refetch()} />
              )}
            </div>
          );
        }
        return (
          <>
            <InvoiceDocumentView
              invoice={data}
              pdfAction={<DownloadButton path={invoicePdfPath(data.invoiceId)} fallbackName={`${data.number ?? 'invoice'}.pdf`} label="Download PDF" />}
              backHref={`/staff/billing/customers/${data.customerId}?tab=invoices`}
              backLabel="Customer billing account"
              links={{
                payment: (id) => `/staff/billing/payments/${id}`,
                creditNote: (id) => `/staff/billing/credit-notes/${id}`,
              }}
              correctionLink={(id, kind) => <CorrectionLink invoiceId={id} kind={kind} />}
              actions={
                <InvoiceActions
                  invoice={data}
                  busy={createCorrected.isPending}
                  onApplyCredit={() => setCreditOpen(true)}
                  onCorrectedReissue={async (creditNoteId) => {
                    try {
                      const created = await createCorrected.mutateAsync(creditNoteId);
                      router.push(`/staff/billing/invoices/${created.invoiceId}`);
                    } catch (failure) {
                      toast({ title: describeBillingError(failure) });
                    }
                  }}
                />
              }
            />
            <ApplyCreditDialog
              open={creditOpen}
              onOpenChange={setCreditOpen}
              invoices={(customerInvoices.data?.items ?? []).filter((i) => i.invoiceId === data.invoiceId)}
              freeCredit={summary.data?.freeCredit ?? { amount: '0.00', currencyCode: data.totals.total.currencyCode }}
              onApply={(input) => applyCredit.mutateAsync(input)}
            />
          </>
        );
      }}
    </QueryBoundary>
  );
}
