'use client';

import { useEffect, useMemo, useRef, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { Plus } from 'lucide-react';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/use-toast';
import {
  getInvoice,
  type BusinessLine,
  type InvoiceDetail,
} from '@/lib/api/billing-staff';
import { ApiConflictError } from '@/lib/api/client';
import { billingViolationCodes } from '@/lib/api/errors';
import { todayInZone } from '@/lib/format/time';
import { a11yMoney } from '@/lib/money';
import { useActiveBranchZone } from '@/lib/hooks/billing/useActiveBranchZone';
import { useIdempotencyKey } from '@/lib/hooks/billing/useIdempotencyKey';
import {
  useCustomerBillingSummary,
  useCustomerCharges,
  useInvoicePreview,
} from '@/lib/hooks/billing/useBillingQueries';
import {
  useApplyLoyalty,
  useCreateDraftInvoice,
  useDiscardDraftInvoice,
  useIssueInvoice,
  useRecordManualCharge,
  useRemoveLoyalty,
  useSetDraftCharges,
} from '@/lib/hooks/billing/useBillingMutations';
import { useAdminServices, useTaxClasses } from '@/lib/hooks/useAdminCatalogue';
import { useStaffCustomer } from '@/lib/hooks/useStaffCustomers';
import { ChargePicker, type PickerItem } from './ChargePicker';
import { ComposerAlert } from './ComposerAlert';
import { InvoicePreview } from './InvoicePreview';
import { LoyaltyRedemptionControl } from './LoyaltyRedemptionControl';
import { ManualChargeDialog } from './ManualChargeDialog';
import { Section } from './PageHeader';

/**
 * `components/billing/InvoiceComposer.tsx` — HO-BIL-002 §11.2 (US-BIL-02, 07, ORD-17, AC-16). Left,
 * the customer's unbilled charges grouped by business line; right, the server's preview; below,
 * **Save draft** / **Issue** (a UI-3 confirm naming the customer and total, sent with one
 * `Idempotency-Key`) / **Discard**. With a saved `draft` it is the same screen bound to
 * `PUT …/charges` with `expectedVersion`: a `409` reloads the draft ("This draft changed — reloaded.")
 * instead of overwriting it. No money is computed here.
 */
function sameSet(a: string[], b: string[]): boolean {
  return a.length === b.length && a.every((id) => b.includes(id));
}

export function InvoiceComposer({
  customerId,
  draft,
  initialSelected = [],
  onDraftChanged,
}: {
  customerId: string;
  draft?: InvoiceDetail;
  initialSelected?: string[];
  onDraftChanged?: () => Promise<unknown>;
}) {
  const router = useRouter();
  const { toast } = useToast();
  const { zone } = useActiveBranchZone();
  const customer = useStaffCustomer(customerId);
  const summary = useCustomerBillingSummary(customerId);
  const unbilled = useCustomerCharges(customerId, 'unbilled');
  const services = useAdminServices();
  const taxClasses = useTaxClasses();

  const draftChargeIds = useMemo(
    () => (draft?.lines ?? []).flatMap((l) => (l.chargeId ? [l.chargeId] : [])),
    [draft],
  );
  const [selected, setSelected] = useState<string[]>(draft ? draftChargeIds : initialSelected);
  const [taken, setTaken] = useState<string[]>([]);
  const [error, setError] = useState<unknown>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [issueOpen, setIssueOpen] = useState(false);
  const [discardOpen, setDiscardOpen] = useState(false);
  const [chargeOpen, setChargeOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [leaving, setLeaving] = useState(false);
  const [target, setTarget] = useState<string | null>(null);
  // A draft this composer created (new-invoice route) — kept so a retry never creates a second one.
  const createdDraftId = useRef<string | null>(null);
  // The charge set the server's draft had when we last read it: the baseline for "unsaved ticks".
  const baseline = useRef<string[]>(draft ? draftChargeIds : []);
  const known = useRef(new Map<string, PickerItem>());
  const issueKey = useIdempotencyKey();

  const draftId = draft?.invoiceId ?? '';
  const createDraft = useCreateDraftInvoice();
  const setCharges = useSetDraftCharges(draftId);
  const issue = useIssueInvoice(draftId);
  const discard = useDiscardDraftInvoice(draftId);
  const applyLoyalty = useApplyLoyalty(draftId);
  const removeLoyalty = useRemoveLoyalty(draftId);
  const addCharge = useRecordManualCharge();

  const items = useMemo<PickerItem[]>(() => {
    const fromServer: PickerItem[] = (unbilled.data?.items ?? []).map((c) => ({
      chargeId: c.chargeId,
      businessLine: c.businessLine,
      description: c.description,
      serviceDate: c.serviceDate,
      quantity: c.quantity,
      unitPrice: c.unitPrice,
      lineTotal: c.lineTotal,
    }));
    const fromDraft: PickerItem[] = (draft?.lines ?? [])
      .filter((l) => l.chargeId)
      .map((l) => ({
        chargeId: l.chargeId!,
        businessLine: l.businessLine,
        description: l.description,
        quantity: l.quantity,
        unitPrice: l.unitPrice,
        lineTotal: l.grossAmount,
      }));
    const merged = [...fromServer, ...fromDraft.filter((d) => !fromServer.some((s) => s.chargeId === d.chargeId))];
    merged.forEach((item) => known.current.set(item.chargeId, item));
    const vanished = taken.flatMap((id) => (merged.some((m) => m.chargeId === id) ? [] : [known.current.get(id)!]));
    return [...merged, ...vanished.filter(Boolean)];
  }, [unbilled.data, draft, taken]);

  const points = draft?.loyaltyRedemption ? String(draft.loyaltyRedemption.points) : '';
  const preview = useInvoicePreview(customerId, selected, points);
  const dirty = draft
    ? selected.length !== draftChargeIds.length || selected.some((id) => !draftChargeIds.includes(id))
    : selected.length > 0;

  useEffect(() => {
    // The server's draft changed (a reload after a 409, another user, the loyalty control). Follow it —
    // unless the user has unsaved ticks, which are kept (never wiped by a background refetch).
    if (!draft) return;
    const wasDirty = !sameSet(selected, baseline.current);
    baseline.current = draftChargeIds;
    if (!wasDirty || sameSet(selected, draftChargeIds)) setSelected(draftChargeIds);
    else setNotice('This draft changed — reloaded. Your unsaved selection is kept.');
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [draftChargeIds.join(',')]);

  function toggle(chargeId: string) {
    setSelected((current) =>
      current.includes(chargeId) ? current.filter((id) => id !== chargeId) : [...current, chargeId],
    );
  }

  function toggleGroup(line: BusinessLine, select: boolean) {
    const ids = items.filter((i) => i.businessLine === line && !taken.includes(i.chargeId)).map((i) => i.chargeId);
    setSelected((current) => (select ? [...new Set([...current, ...ids])] : current.filter((id) => !ids.includes(id))));
  }

  async function handleFailure(failure: unknown) {
    const codes = billingViolationCodes(failure);
    if (failure instanceof ApiConflictError && codes.includes('ChargeAlreadyInvoiced')) {
      const fresh = await unbilled.refetch();
      const stillFree = new Set((fresh.data?.items ?? []).map((c) => c.chargeId));
      const gone = selected.filter((id) => !stillFree.has(id) && !draftChargeIds.includes(id));
      setTaken((current) => [...new Set([...current, ...gone])]);
      setSelected((current) => current.filter((id) => !gone.includes(id)));
      setNotice('A charge you selected is already on another invoice. It is marked and has been unticked.');
      return;
    }
    if (draft && onDraftChanged && codes.includes('InvoiceNotDraft')) {
      await onDraftChanged();
      toast({ title: 'This invoice has already been issued', description: 'It was issued from another window. The issued invoice is shown.' });
      return;
    }
    if (failure instanceof ApiConflictError && draft && onDraftChanged) {
      await onDraftChanged();
      setNotice('This draft changed — reloaded.');
      return;
    }
    setError(failure);
  }

  async function run(action: () => Promise<void>) {
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      await action();
    } catch (failure) {
      await handleFailure(failure);
    } finally {
      setBusy(false);
    }
  }

  // Lets the unsaved-changes guard stand down first (it re-renders without `when`), then navigates.
  function go(path: string) {
    setLeaving(true);
    setTarget(path);
  }
  useEffect(() => {
    if (target) router.push(target);
  }, [target, router]);

  /**
   * Makes sure a draft exists whose charges are EXACTLY the current selection and returns its id and
   * REAL version (read from the server, never guessed): create it the first time, and on any later call —
   * a retry after a failed issue, or an edit of a saved draft — read it back and `setCharges` when the
   * selection differs, so a changed selection can never save or issue the old charge set.
   */
  async function syncDraft(): Promise<{ id: string; version: number; created: boolean }> {
    const existing = draft?.invoiceId ?? createdDraftId.current;
    if (!existing) {
      const created = await createDraft.mutateAsync({ customerId, chargeIds: selected });
      createdDraftId.current = created.invoiceId;
      const fresh = await getInvoice(created.invoiceId);
      baseline.current = selected;
      return { id: created.invoiceId, version: fresh.version, created: true };
    }
    const server = draft ?? (await getInvoice(existing));
    const serverCharges = server.lines.flatMap((l) => (l.chargeId ? [l.chargeId] : []));
    if (sameSet(serverCharges, selected)) return { id: existing, version: server.version, created: false };
    const saved = await setCharges.mutateAsync({ invoiceId: existing, expectedVersion: server.version, chargeIds: selected });
    baseline.current = selected;
    return { id: existing, version: saved.version, created: false };
  }

  const saveDraft = () =>
    run(async () => {
      const synced = await syncDraft();
      toast({ title: 'Draft saved' });
      if (draft) await onDraftChanged?.();
      else go(`/staff/billing/invoices/${synced.id}`);
    });

  const issueNow = () =>
    run(async () => {
      const synced = await syncDraft();
      const result = await issue.mutateAsync({ invoiceId: synced.id, expectedVersion: synced.version, idempotencyKey: issueKey.current() });
      issueKey.renew();
      toast({ title: `Issued as ${result.number}` });
      go(`/staff/billing/invoices/${result.invoiceId}`);
    });

  const customerName = customer.data?.fullName ?? 'this customer';
  const total = preview.data?.totals.total;
  const hasProblems = (preview.data?.problems.length ?? 0) > 0;
  const canAct = selected.length > 0 && !busy && !preview.isRecomputing && !hasProblems;

  return (
    <div className="flex flex-col gap-5">
      {!leaving && <UnsavedChangesGuard when={dirty} />}
      <ComposerAlert error={error} notice={notice} />
      <div className="grid min-w-0 gap-5 lg:grid-cols-2">
        <Section
          title="Charges to invoice"
          description="Pick from any business line — they combine into one invoice."
          actions={
            <Button variant="secondary" size="sm" onClick={() => setChargeOpen(true)}>
              <Plus size={14} aria-hidden="true" />
              Add manual charge
            </Button>
          }
        >
          <ChargePicker items={items} selected={selected} taken={taken} onToggle={toggle} onToggleGroup={toggleGroup} disabled={busy} />
        </Section>
        <Section title="Invoice preview">
          <InvoicePreview
            preview={preview.data}
            isRecomputing={preview.isRecomputing}
            isError={preview.isError}
            hasSelection={selected.length > 0}
            onRetry={() => void preview.refetch()}
          />
          {draft && summary.data?.loyaltyEnabled && (
            <LoyaltyRedemptionControl
              balance={summary.data.loyaltyPoints}
              redemption={draft.loyaltyRedemption}
              disabled={busy || dirty}
              onApply={async (value) => {
                await applyLoyalty.mutateAsync(value);
                await onDraftChanged?.();
              }}
              onRemove={async () => {
                await removeLoyalty.mutateAsync();
                await onDraftChanged?.();
              }}
            />
          )}
        </Section>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <Button variant="secondary" disabled={selected.length === 0 || busy} onClick={() => void saveDraft()}>
          Save draft
        </Button>
        <Button disabled={!canAct} onClick={() => setIssueOpen(true)} aria-busy={busy}>
          Issue invoice
        </Button>
        {draft && (
          <Button variant="ghost" disabled={busy} onClick={() => setDiscardOpen(true)}>
            Discard draft
          </Button>
        )}
        <Link href={`/staff/billing/customers/${customerId}`} className="text-sm font-semibold text-accent-ink hover:underline">
          Back to the account
        </Link>
      </div>

      <ConfirmDialog
        open={issueOpen}
        onOpenChange={setIssueOpen}
        title="Issue this invoice"
        consequence={`Issue invoice for ${total ? a11yMoney(total) : 'the selected charges'} to ${customerName}? It gets the next number and can no longer be changed.`}
        confirmLabel="Issue invoice"
        pendingLabel="Issuing…"
        onConfirm={async () => {
          await issueNow();
          setIssueOpen(false);
        }}
      />
      <ConfirmDialog
        open={discardOpen}
        onOpenChange={setDiscardOpen}
        title="Discard this draft"
        variant="destructive"
        consequence={`The draft is deleted. Its ${draftChargeIds.length === 1 ? '1 charge returns' : `${draftChargeIds.length} charges return`} to unbilled (${(draft?.lines ?? []).map((l) => l.description).join(', ')}).`}
        confirmLabel="Discard draft"
        pendingLabel="Discarding…"
        onConfirm={async () => {
          await run(async () => {
            await discard.mutateAsync();
            toast({ title: 'Draft discarded' });
            go(`/staff/billing/customers/${customerId}`);
          });
          setDiscardOpen(false);
        }}
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
          return result;
        }}
      />
    </div>
  );
}
