'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { Button, buttonVariants } from '@/components/ui/button';
import { useToast } from '@/components/ui/use-toast';
import { creditNotePdfPath, type CreditNoteLine } from '@/lib/api/billing-staff';
import { describeBillingError } from '@/lib/api/errors';
import { formatDateOnly, formatInZone } from '@/lib/format/time';
import { useActiveBranchZone } from '@/lib/hooks/billing/useActiveBranchZone';
import { useCreditNote, useInvoice, useRefunds } from '@/lib/hooks/billing/useBillingQueries';
import { useCreateCorrectedDraft } from '@/lib/hooks/billing/useBillingMutations';
import { Amount } from './Amount';
import { DownloadButton } from './DownloadButton';
import { methodLabel } from './labels';
import { PageHeader, Section, Stat } from './PageHeader';
import { QueryBoundary } from './QueryBoundary';
import { ResponsiveTable, type Column } from './ResponsiveTable';

/**
 * `components/billing/CreditNoteScreens.tsx` — HO-BIL-002 §11.3 `CreditNoteView`. The credit note as
 * issued: number, invoice link, kind and reason, the lines **at the original rates** (frozen on the
 * invoice), total, the amount released to customer credit, its refunds, and **Download PDF**. It
 * offers *Corrected re-issue* until one exists.
 */
export function CreditNoteView({ creditNoteId }: { creditNoteId: string }) {
  const router = useRouter();
  const { toast } = useToast();
  const { zone } = useActiveBranchZone();
  const note = useCreditNote(creditNoteId);
  const invoice = useInvoice(note.data?.invoiceId ?? '');
  const refunds = useRefunds({});
  const corrected = useCreateCorrectedDraft(note.data?.invoiceId ?? '');

  const columns: Array<Column<CreditNoteLine>> = [
    { id: 'description', header: 'Description', primary: true, cell: (l) => l.description },
    {
      id: 'credited',
      header: 'Credited',
      numeric: true,
      cell: (l) => (l.requestedAmount ? <Amount money={l.requestedAmount} /> : (l.quantity ?? '—')),
    },
    { id: 'rate', header: 'Rate %', numeric: true, cell: (l) => `${l.taxRatePercent}%` },
    { id: 'net', header: 'Net', numeric: true, cell: (l) => <Amount money={l.net} /> },
    { id: 'tax', header: 'Tax', numeric: true, cell: (l) => <Amount money={l.tax} /> },
    { id: 'gross', header: 'Gross', numeric: true, cell: (l) => <Amount money={l.gross} /> },
  ];

  return (
    <QueryBoundary
      query={note}
      notFoundTitle="Credit note not found"
      loadingLabel="Loading the credit note…"
    >
      {(data) => {
        const mine = (refunds.data?.items ?? []).filter((r) => r.creditNoteId === data.creditNoteId);
        return (
          <div className="flex flex-col gap-5">
            <PageHeader
              title={data.number}
              back={{
                href: `/staff/billing/invoices/${data.invoiceId}`,
                label: `Invoice ${data.invoiceNumber ?? ''}`,
              }}
              description={`${data.kind} · issued ${formatDateOnly(data.issueDate)} by ${data.issuedByName}`}
              actions={
                <>
                  <DownloadButton path={creditNotePdfPath(data.creditNoteId)} fallbackName={`${data.number}.pdf`} label="Download PDF" />
                  {data.correctedByInvoiceId ? (
                    <Link
                      href={`/staff/billing/invoices/${data.correctedByInvoiceId}`}
                      className={buttonVariants({ variant: 'secondary', size: 'sm' })}
                    >
                      Corrected invoice
                    </Link>
                  ) : (
                    <Button
                      variant="secondary"
                      size="sm"
                      disabled={corrected.isPending}
                      onClick={async () => {
                        try {
                          const created = await corrected.mutateAsync(data.creditNoteId);
                          router.push(`/staff/billing/invoices/${created.invoiceId}`);
                        } catch (failure) {
                          toast({ title: describeBillingError(failure) });
                        }
                      }}
                    >
                      Corrected re-issue
                    </Button>
                  )}
                </>
              }
            />
            <p className="text-sm text-ink [overflow-wrap:anywhere]">Reason: {data.reason}</p>
            <dl className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
              <Stat label="Net">
                <Amount money={data.net} />
              </Stat>
              <Stat label="Tax">
                <Amount money={data.tax} />
              </Stat>
              <Stat label="Total credited">
                <Amount money={data.total} />
              </Stat>
              <Stat label="Released to customer credit">
                <Amount money={data.releasedToCustomerCredit} />
              </Stat>
            </dl>
            <Section title="Lines" description="At the rates printed on the original invoice.">
              <ResponsiveTable
                caption="Credit note lines"
                columns={columns}
                rows={data.lines}
                getRowId={(l) => l.invoiceLineId}
                emptyState={{ title: 'No lines.' }}
              />
            </Section>
            <Section title="Refunds">
              {mine.length === 0 ? (
                <p className="text-sm text-ink-muted">
                  No refund has been paid out against this credit note.
                </p>
              ) : (
                <ul className="flex flex-col gap-1 text-sm">
                  {mine.map((r) => (
                    <li key={r.refundId}>
                      <Amount money={r.amount} className="font-semibold" /> by {methodLabel(r.method)}{' '}
                      on {formatInZone(r.issuedOn, zone, { dateStyle: 'medium' })} — {r.reason}
                    </li>
                  ))}
                </ul>
              )}
            </Section>
            {invoice.data && (
              <p className="text-sm text-ink-muted">
                Invoice {invoice.data.number} now shows <Amount money={invoice.data.credited} /> credited.
              </p>
            )}
          </div>
        );
      }}
    </QueryBoundary>
  );
}
