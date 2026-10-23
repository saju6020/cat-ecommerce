import { describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { OrderTrackerCard } from '@/components/orders/OrderTrackerCard';
import { orderTrackerSteps } from '@/lib/orders/tracker';
import type { CustomerOrderDetail, OrderStatus } from '@/lib/api/my-orders';

type TrackerInput = Parameters<typeof orderTrackerSteps>[0];

function order(status: OrderStatus, overrides: Partial<TrackerInput> = {}): TrackerInput {
  return {
    status,
    fulfilmentMethod: 'Delivery',
    placedOn: '2026-10-10T04:00:00Z',
    branchTimeZoneId: 'Asia/Dhaka',
    statusHistory: [
      { to: 'Confirmed', changedOn: '2026-10-10T05:00:00Z' },
      { to: 'Packing', changedOn: '2026-10-11T04:00:00Z' },
      { to: 'Dispatched', changedOn: '2026-10-12T04:00:00Z' },
    ],
    fulfilment: { slotDate: null, slotWindow: null } as CustomerOrderDetail['fulfilment'],
    ...overrides,
  };
}

const statuses = (input: TrackerInput, options?: { waitingForTransfer?: boolean }) =>
  orderTrackerSteps(input, options)!.map((step) => `${step.label}:${step.status}`);

describe('orderTrackerSteps - the server status mapped onto the five nodes', () => {
  it('Confirmed: Placed is done, Confirmed is current, the rest are ahead', () => {
    expect(statuses(order('Confirmed'))).toEqual([
      'Placed:done',
      'Confirmed:current',
      'Packing:future',
      'Dispatched:future',
      'Delivered:future',
    ]);
  });

  it('PendingPayment is still the Placed node, waiting for the transfer', () => {
    const steps = orderTrackerSteps(order('PendingPayment', { statusHistory: [] }), {
      waitingForTransfer: true,
    })!;
    expect(steps.map((s) => s.status)).toEqual(['current', 'future', 'future', 'future', 'future']);
    expect(steps[0]!.caption).toBe('Waiting for your transfer');
  });

  it('Dispatched: three done, Dispatched current, Delivered ahead - with the times the server sent', () => {
    const steps = orderTrackerSteps(order('Dispatched'))!;
    expect(steps.map((s) => s.status)).toEqual(['done', 'done', 'done', 'current', 'future']);
    expect(steps[1]!.caption).toMatch(/Sat 10 Oct, 11:00/);
    expect(steps[3]!.caption).toMatch(/Mon 12 Oct, 10:00/);
    expect(steps[4]!.caption).toBeUndefined();
  });

  it('the expected slot is shown only as the server wrote it, on the Delivered node', () => {
    const steps = orderTrackerSteps(
      order('Dispatched', {
        fulfilment: {
          slotDate: null,
          slotWindow: '10:00–13:00',
        } as CustomerOrderDetail['fulfilment'],
      }),
    )!;
    expect(steps[4]!.caption).toBe('Expected 10:00–13:00');
  });

  it.each<OrderStatus>(['Delivered', 'Returned', 'Refunded'])('%s is complete', (status) => {
    expect(orderTrackerSteps(order(status))!.every((s) => s.status === 'done')).toBe(true);
  });

  it('a cancelled order has no tracker', () => {
    expect(orderTrackerSteps(order('Cancelled'))).toBeNull();
    const { container } = render(
      <OrderTrackerCard steps={orderTrackerSteps(order('Cancelled'))} />,
    );
    expect(container).toBeEmptyDOMElement();
  });

  it('collection names the last two stages for what they are', () => {
    expect(
      orderTrackerSteps(order('Dispatched', { fulfilmentMethod: 'Pickup' }))!.map((s) => s.label),
    ).toEqual(['Placed', 'Confirmed', 'Packing', 'Ready to collect', 'Collected']);
  });
});

describe('OrderTrackerCard', () => {
  it('renders the steps with their state in words, and marks the current one', () => {
    render(<OrderTrackerCard steps={orderTrackerSteps(order('Packing'))} />);
    const list = screen.getByRole('list', { name: 'Order progress steps' });
    const items = within(list).getAllByRole('listitem');
    expect(items).toHaveLength(5);
    expect(items[2]).toHaveAttribute('aria-current', 'step');
    expect(items[2]).toHaveTextContent('Packing (current)');
    expect(items[0]).toHaveTextContent('Placed (done)');
    expect(items[4]).toHaveTextContent('Delivered (not yet)');
  });
});
