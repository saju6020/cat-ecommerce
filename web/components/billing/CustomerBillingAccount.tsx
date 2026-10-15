'use client';

import { useState } from 'react';
import Link from 'next/link';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';
import { FilePlus2, Plus } from 'lucide-react';
import { Button, buttonVariants } from '@/components/ui/button';
import { useToast } from '@/components/ui/use-toast';
import type { Charge } from '@/lib/api/billing-staff';
import { todayInZone } from '@/lib/format/time';
import {
  useCustomerBillingSummary,
  useCustomerCharges,
  useCustomerInvoices,
  useCustomerPayments,
} from '@/lib/hooks/billing/useBillingQueries';
import {
  useApplyCustomerCredit,
  useRecordManualCharge,
  useVoidCharge,
} from '@/lib/hooks/billing/useBillingMutations';
import { useActiveBranchZone } from '@/lib/hooks/billing/useActiveBranchZone';
import { useTaxClasses, useAdminServices } from '@/lib/hooks/useAdminCatalogue';
import { useStaffCustomer } from '@/lib/hooks/useStaffCustomers';
import { ApplyCreditDialog } from './ApplyCreditDialog';
import { CustomerBillingSummary } from './CustomerBillingSummary';
import { CustomerInvoicesTable, CustomerPaymentsTable } from './CustomerAccountTables';
import { ManualChargeDialog } from './ManualChargeDialog';
import { PageHeader, Section } from './PageHeader';
import { QueryBoundary } from './QueryBoundary';
import { ReasonDialog } from './ReasonDialog';
import { Tabs } from './Tabs';
import { UnbilledChargesTable } from './UnbilledChargesTable';

/**
 * `components/billing/CustomerBillingAccount.tsx` — HO-BIL-002 §11.1 (US-BIL-03, US-BIL-08): the hub
 * of the Billing section. Summary, then the tabs *Unbilled charges* / *Invoices* / *Payments*. The
 * ticked charges travel to the composer in the URL; **Void**, **Add manual charge** and **Apply
 * credit** are dialogs on `components/ui/dialog.tsx`.
 */
const TAB_IDS = ['unbilled', 'invoices', 'payments'] as const;
type TabId = (typeof TAB_IDS)[number];

export function CustomerBillingAccount({ customerId }: { customerId: string }) {
  const router = useRouter();
  const pathname = usePathname();
  const search = useSearchParams();
  const { toast } = useToast();
  const requestedTab = search.get('tab');
  const tab: TabId = TAB_IDS.find((id) => id === requestedTab) ?? 'unbilled';

  const { zone } = useActiveBranchZone();
  const customer = useStaffCustomer(customerId);
  const summary = useCustomerBillingSummary(customerId);
  const unbilled = useCustomerCharges(customerId, 'unbilled');
  const invoices = useCustomerInvoices(customerId);
  const payments = useCustomerPayments(customerId);
  const services = useAdminServices();
  const taxClasses = useTaxClasses();
  const voidCharge = useVoidCharge(customerId);
  const applyCredit = useApplyCustomerCredit(customerId);
  const addCharge = useRecordManualCharge();

  const [selected, setSelected] = useState<string[]>([]);
  const [voiding, setVoiding] = useState<Charge | null>(null);
  const [creditOpen, setCreditOpen] = useState(false);
  const [chargeOpen, setChargeOpen] = useState(false);

  function setTab(next: string) {
    const params = new URLSearchParams(search.toString());
    params.set('tab', next);
    router.replace(`${pathname}?${params.toString()}`, { scroll: false });
  }

  function toggle(chargeId: string) {
    setSelected((current) =>
      current.includes(chargeId) ? current.filter((id) => id !== chargeId) : [...current, chargeId],
    );
  }

  const chargeItems = unbilled.data?.items;
  const newInvoiceHref =
    `/staff/billing/invoices/new?customerId=${encodeURIComponent(customerId)}` +
    (selected.length > 0 ? `&charges=${selected.join(',')}` : '');
  const invoicesWithOutstanding = (invoices.data?.items ?? []).filter(
    (i) => i.status === 'Issued' || i.status === 'PartPaid',
  );

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={customer.data ? `${customer.data.fullName} — billing` : 'Customer billing'}
        description="Balance, unbilled charges, invoices and payments."
        back={{ href: '/staff/billing', label: 'Billing overview' }}
        actions={
          <Link href={`/staff/customers/${customerId}`} className={buttonVariants({ variant: 'secondary', size: 'sm' })}>
            Customer record
          </Link>
        }
      />
      <QueryBoundary query={summary} loadingLabel="Loading the billing summary…">
        {(data) => (
          <CustomerBillingSummary
            customerId={customerId}
            summary={data}
            onApplyCredit={() => setCreditOpen(true)}
          />
        )}
      </QueryBoundary>

      <Section title="Account activity">
        <Tabs
          label="Account activity"
          selected={tab}
          onSelect={setTab}
          idPrefix="account"
          tabs={[
            {
              id: 'unbilled',
              label: 'Unbilled charges',
              panel: (
                <div className="flex flex-col gap-3">
                  <div className="flex flex-wrap gap-2">
                    <Link href={newInvoiceHref} className={buttonVariants({ variant: 'primary', size: 'sm' })}>
                      <FilePlus2 size={14} aria-hidden="true" />
                      {selected.length > 0 ? `New invoice from ${selected.length} selected` : 'New invoice'}
                    </Link>
                    <Button
                      variant="secondary"
                      size="sm"
                      onClick={() => setSelected((chargeItems ?? []).map((c) => c.chargeId))}
                      disabled={!chargeItems || chargeItems.length === 0}
                    >
                      Select all
                    </Button>
                    <Button variant="secondary" size="sm" onClick={() => setChargeOpen(true)}>
                      <Plus size={14} aria-hidden="true" />
                      Add manual charge
                    </Button>
                  </div>
                  <UnbilledChargesTable
                    charges={chargeItems}
                    selected={selected}
                    onToggle={toggle}
                    onVoid={setVoiding}
                    isLoading={unbilled.isLoading}
                    isError={unbilled.isError}
                    onRetry={() => void unbilled.refetch()}
                  />
                </div>
              ),
            },
            {
              id: 'invoices',
              label: 'Invoices',
              panel: (
                <CustomerInvoicesTable
                  invoices={invoices.data?.items}
                  isLoading={invoices.isLoading}
                  isError={invoices.isError}
                  onRetry={() => void invoices.refetch()}
                />
              ),
            },
            {
              id: 'payments',
              label: 'Payments',
              panel: (
                <CustomerPaymentsTable
                  payments={payments.data?.items}
                  isLoading={payments.isLoading}
                  isError={payments.isError}
                  onRetry={() => void payments.refetch()}
                />
              ),
            },
          ]}
        />
      </Section>

      <ReasonDialog
        open={voiding !== null}
        onOpenChange={(open) => !open && setVoiding(null)}
        title={`Void “${voiding?.description ?? ''}”`}
        consequence="The charge is cancelled and will not be billed. It stays in the history with your reason. This cannot be undone."
        confirmLabel="Void charge"
        pendingLabel="Voiding…"
        onSubmit={async (reason) => {
          if (!voiding) return;
          await voidCharge.mutateAsync({ chargeId: voiding.chargeId, reason });
          setSelected((current) => current.filter((id) => id !== voiding.chargeId));
          toast({ title: 'Charge voided' });
        }}
      />

      <ApplyCreditDialog
        open={creditOpen}
        onOpenChange={setCreditOpen}
        invoices={invoicesWithOutstanding}
        freeCredit={summary.data?.freeCredit ?? { amount: '0.00', currencyCode: 'BDT' }}
        onApply={(input) => applyCredit.mutateAsync(input)}
      />

      <ManualChargeDialog
        open={chargeOpen}
        onOpenChange={setChargeOpen}
        customerId={customerId}
        defaultDate={todayInZone(zone)}
        services={services.data}
        taxClasses={taxClasses.data}
        taxClassesUnavailable={taxClasses.isError}
        onSubmit={async (request) => {
          const result = await addCharge.mutateAsync(request);
          setSelected((current) => [...current, result.chargeId]);
          toast({ title: 'Charge added', description: 'It is ticked, ready for the next invoice.' });
          return result;
        }}
      />
    </div>
  );
}
