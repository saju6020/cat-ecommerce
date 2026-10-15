import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { RefundDialog } from '@/components/billing/RefundDialog';

const methods = [
  { value: 1, name: 'CashInStore', countsInTill: true, requiresReference: false, referenceLabel: 'Receipt number', requiresCollector: false, isOnline: false, allowedRefundMethods: ['CashInStore'] },
];

function Harness({ onRefund }: { onRefund: (input: { request: { amount: string }; idempotencyKey: string }) => Promise<{ refundId: string }> }) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button onClick={() => setOpen(true)}>Open refund</button>
      <RefundDialog
        open={open}
        onOpenChange={setOpen}
        paymentId="p1"
        allowedMethods={['CashInStore']}
        methods={methods}
        available={{ amount: '500.00', currencyCode: 'BDT' }}
        onRefund={onRefund as never}
      />
    </>
  );
}

describe('RefundDialog idempotency (security MEDIUM)', () => {
  it('a lost response then a different refund never reuses the first key; success names the amount and method', async () => {
    const user = userEvent.setup();
    const keys: string[] = [];
    const onRefund = vi.fn(async ({ idempotencyKey }: { request: { amount: string }; idempotencyKey: string }) => {
      keys.push(idempotencyKey);
      if (keys.length === 1) throw new Error('lost response');
      return { refundId: 'r1' };
    });
    render(<Harness onRefund={onRefund} />);
    await user.click(screen.getByRole('button', { name: 'Open refund' }));
    await user.type(screen.getByLabelText(/^Amount/), '100');
    await user.type(screen.getByRole('textbox', { name: /Reason/ }), 'first attempt');
    await user.click(screen.getByRole('button', { name: 'Refund' }));
    await screen.findByRole('alert');
    // close and reopen, then a DIFFERENT refund
    await user.click(screen.getByRole('button', { name: 'Cancel' }));
    await user.click(screen.getByRole('button', { name: 'Open refund' }));
    await user.type(screen.getByLabelText(/^Amount/), '40');
    await user.type(screen.getByRole('textbox', { name: /Reason/ }), 'second refund');
    await user.click(screen.getByRole('button', { name: 'Refund' }));
    expect(await screen.findByText('Refund recorded.')).toBeInTheDocument();
    expect(screen.getByText(/by cash in store/)).toBeInTheDocument();
    expect(screen.getAllByText(/40.00/).length).toBeGreaterThan(0);
    expect(keys).toHaveLength(2);
    expect(keys[1]).not.toBe(keys[0]);
  });

  it('a retry of the very same request keeps its key', async () => {
    const user = userEvent.setup();
    const keys: string[] = [];
    const onRefund = vi.fn(async ({ idempotencyKey }: { request: { amount: string }; idempotencyKey: string }) => {
      keys.push(idempotencyKey);
      if (keys.length === 1) throw new Error('lost');
      return { refundId: 'r1' };
    });
    render(<Harness onRefund={onRefund} />);
    await user.click(screen.getByRole('button', { name: 'Open refund' }));
    await user.type(screen.getByLabelText(/^Amount/), '100');
    await user.type(screen.getByRole('textbox', { name: /Reason/ }), 'same');
    await user.click(screen.getByRole('button', { name: 'Refund' }));
    await screen.findByRole('alert');
    await user.click(screen.getByRole('button', { name: 'Refund' }));
    await screen.findByText('Refund recorded.');
    expect(keys[1]).toBe(keys[0]);
  });
});
