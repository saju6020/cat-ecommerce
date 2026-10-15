'use client';

import { useEffect, useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { MoneyField, TextField } from '@/components/billing/Fields';
import { submitOrderingForm } from '@/lib/api/errors';
import type { RecordDeliveryInput, StaffOrderDetail } from '@/lib/api/staff-orders';
import { formatMoney } from '@/lib/format/money';
import { branchLocalToInstant, nowInBranchLocal, paymentMethodLabel } from '@/lib/format/orders';
import { useIdempotencyKey } from '@/lib/hooks/useIdempotencyKey';
import { useStaffSession } from '@/lib/hooks/useSession';
import { useRecordDelivery } from '@/lib/hooks/useStaffOrders';
import { OrderDialog } from './OrderDialog';

/**
 * `components/orders/RecordDeliveryDialog.tsx` — ⭐ HO-ORD-002 §11.8, US-ORD-20 AC2. Records that the
 * parcel arrived (or the customer collected). By payment method:
 *
 * - **Cash on delivery**: the cash collected (amount pre-filled from the order's total, editable), who
 *   collected it (the courier — by name — or the person recording) and when — all required.
 * - **Pay on collection**: the tender (cash or card in store); the amount is the balance due, read-only;
 *   a card needs the **terminal slip reference**.
 * - Anything else was paid already: only the delivered-on time.
 *
 * Money steps restate the amount before sending (UI-3). One idempotency key per attempt, kept for a
 * retry of the same payload. Times are read in the branch's zone.
 */
interface Values {
  deliveredOn: string;
  amount: string;
  collectorKind: 'Courier' | 'Staff';
  collectorName: string;
  collectedOn: string;
  tender: 'Cash' | 'InStoreCard';
  reference: string;
}

export function RecordDeliveryDialog({
  order,
  open,
  onOpenChange,
}: {
  order: StaffOrderDetail;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const record = useRecordDelivery(order.id);
  const { data: session } = useStaffSession();
  const key = useIdempotencyKey();
  const zone = order.branchTimeZoneId;
  const total = order.totals.grandTotal;
  const isCod = order.paymentMethod === 'CashOnDelivery';
  const isCollection = order.paymentMethod === 'PayOnCollection';
  const [pending, setPending] = useState<Values | null>(null);

  const defaults = (): Values => ({
    deliveredOn: nowInBranchLocal(zone),
    amount: total.amount,
    collectorKind: 'Courier',
    collectorName: order.shipment?.courierName ?? '',
    collectedOn: nowInBranchLocal(zone),
    tender: 'Cash',
    reference: '',
  });
  const {
    register,
    control,
    handleSubmit,
    setError,
    reset,
    formState: { errors },
  } = useForm<Values>({ defaultValues: defaults() });
  const kind = useWatch({ control, name: 'collectorKind' });
  const tender = useWatch({ control, name: 'tender' });

  useEffect(() => {
    if (open) {
      reset(defaults());
      setPending(null);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  function build(values: Values): RecordDeliveryInput {
    return {
      expectedVersion: order.version,
      deliveredOn: branchLocalToInstant(values.deliveredOn, zone),
      codCollection: isCod
        ? {
            amount: values.amount.trim(),
            collectorKind: values.collectorKind,
            collectorName: values.collectorKind === 'Staff' ? (session?.fullName ?? '') : values.collectorName.trim(),
            collectorStaffId: values.collectorKind === 'Staff' ? session?.staffAccountId : undefined,
            collectedOn: branchLocalToInstant(values.collectedOn, zone),
          }
        : undefined,
      collectionPayment: isCollection
        ? {
            method: values.tender,
            amount: total.amount,
            reference: values.tender === 'InStoreCard' ? values.reference.trim() : undefined,
          }
        : undefined,
    };
  }

  const review = handleSubmit((values) => {
    if (isCod && !/^\d+(\.\d{1,2})?$/.test(values.amount.trim())) {
      setError('amount', { message: 'Enter the amount as a number, like 1927.50.' });
      return;
    }
    if (isCod && values.collectorKind === 'Courier' && !values.collectorName.trim()) {
      setError('collectorName', { message: 'Enter the rider who collected the cash.' });
      return;
    }
    if (isCollection && values.tender === 'InStoreCard' && !values.reference.trim()) {
      setError('reference', { message: 'Enter the card terminal slip reference.' });
      return;
    }
    if (isCod || isCollection) setPending(values);
    else void send(values);
  });

  async function send(values: Values) {
    const request = build(values);
    const done = await submitOrderingForm(
      async () => {
        try {
          await record.mutateAsync({ request, idempotencyKey: key.keyFor(JSON.stringify(request)) });
        } catch (error) {
          key.settleFailure(error);
          throw error;
        }
        return true as const;
      },
      setError,
      {
        knownFields: ['deliveredOn', 'amount', 'collectorName', 'collectedOn', 'reference'],
        fieldMap: {
          DeliveredOn: 'deliveredOn',
          'CodCollection.Amount': 'amount',
          'CodCollection.CollectorName': 'collectorName',
          'CodCollection.CollectedOn': 'collectedOn',
          'CollectionPayment.Reference': 'reference',
          'CollectionPayment.Amount': 'reference',
        },
      },
    );
    if (done) {
      key.renew();
      onOpenChange(false);
    } else {
      setPending(null);
    }
  }

  const restated = pending
    ? isCod
      ? `Confirm ${formatMoney({ amount: pending.amount.trim(), currencyCode: total.currencyCode })} cash collected by ${
          pending.collectorKind === 'Staff' ? (session?.fullName ?? 'you') : pending.collectorName.trim()
        } for ${order.orderNumber}?`
      : `Confirm ${formatMoney(total)} taken ${pending.tender === 'Cash' ? 'in cash' : 'by card in store'} for ${order.orderNumber}?`
    : undefined;
  const label = order.fulfilmentMethod === 'Pickup' ? 'Record collection' : 'Record delivery';

  return (
    <OrderDialog
      open={open}
      onOpenChange={onOpenChange}
      title={label}
      description={`${order.orderNumber} is ${paymentMethodLabel(order.paymentMethod).toLowerCase()}. Times are in ${zone}.`}
      onSubmit={pending ? (event) => { event.preventDefault(); void send(pending); } : review}
      submitLabel={pending ? 'Confirm' : isCod || isCollection ? 'Review' : label}
      pending={record.isPending}
      rootError={errors.root?.message}
      confirmation={restated}
      onBack={() => setPending(null)}
    >
      <TextField id="delivered-on" label="Delivered on" type="datetime-local" required error={errors.deliveredOn?.message} {...register('deliveredOn', { required: 'Enter when it happened.' })} />
      {isCod && (
        <>
          <MoneyField id="cod-amount" label="Cash collected" currencyCode={total.currencyCode} required error={errors.amount?.message} {...register('amount')} />
          <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
            <legend className="mb-1 text-sm font-semibold text-ink">Collected by</legend>
            <label className="flex items-center gap-2 text-sm">
              <input type="radio" value="Courier" className="h-4 w-4 accent-[var(--accent)]" {...register('collectorKind')} />
              The courier
            </label>
            <label className="flex items-center gap-2 text-sm">
              <input type="radio" value="Staff" className="h-4 w-4 accent-[var(--accent)]" {...register('collectorKind')} />
              Me ({session?.fullName ?? 'this staff member'})
            </label>
          </fieldset>
          {kind === 'Courier' && (
            <TextField id="cod-collector" label="Rider’s name" required maxLength={200} error={errors.collectorName?.message} {...register('collectorName')} />
          )}
          <TextField id="cod-collected-on" label="Cash collected on" type="datetime-local" required error={errors.collectedOn?.message} {...register('collectedOn', { required: 'Enter when the cash was collected.' })} />
        </>
      )}
      {isCollection && (
        <>
          <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
            <legend className="mb-1 text-sm font-semibold text-ink">Paid with</legend>
            <label className="flex items-center gap-2 text-sm">
              <input type="radio" value="Cash" className="h-4 w-4 accent-[var(--accent)]" {...register('tender')} />
              Cash
            </label>
            <label className="flex items-center gap-2 text-sm">
              <input type="radio" value="InStoreCard" className="h-4 w-4 accent-[var(--accent)]" {...register('tender')} />
              Card in store
            </label>
          </fieldset>
          <p className="text-sm text-ink">
            Balance due: <strong>{formatMoney(total)}</strong>
          </p>
          {tender === 'InStoreCard' && (
            <TextField id="card-slip" label="Terminal slip reference" required maxLength={100} hint="The number printed on the card terminal’s slip — never the card number." error={errors.reference?.message} {...register('reference')} />
          )}
        </>
      )}
    </OrderDialog>
  );
}
