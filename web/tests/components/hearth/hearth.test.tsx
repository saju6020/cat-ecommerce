import { describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { PillSteps } from '@/components/hearth/PillSteps';
import { OrderTracker } from '@/components/hearth/OrderTracker';
import { FreeDeliveryProgress } from '@/components/hearth/FreeDeliveryProgress';
import { HearthLogo } from '@/components/hearth/HearthLogo';

describe('PillSteps', () => {
  const steps = [
    { label: 'Delivery', status: 'done' as const },
    { label: 'Delivery time', status: 'skipped' as const },
    { label: 'Payment', status: 'current' as const },
    { label: 'Review', status: 'todo' as const },
  ];

  it('keeps the step number and state in each accessible name', () => {
    render(<PillSteps steps={steps} />);
    const list = screen.getByRole('list', { name: 'Checkout steps' });
    const items = within(list).getAllByRole('listitem');
    expect(items).toHaveLength(4);
    expect(items[0]!).toHaveTextContent('Step 1, done: Delivery');
    expect(items[1]!).toHaveTextContent('Step 2, skipped: Delivery time');
    expect(items[2]!).toHaveTextContent('Step 3, current step: Payment');
    expect(items[3]!).toHaveTextContent('Step 4, not started: Review');
  });

  it('marks only the current step with aria-current', () => {
    render(<PillSteps steps={steps} />);
    const current = screen.getAllByRole('listitem').filter((li) => li.hasAttribute('aria-current'));
    expect(current).toHaveLength(1);
    expect(current[0]).toHaveTextContent('Payment');
    expect(current[0]).toHaveTextContent('You are here');
  });

  it('shows a tick, not the number, on a done step, and hides the dot from screen readers', () => {
    render(<PillSteps steps={steps} />);
    const first = screen.getAllByRole('listitem')[0]!;
    expect(first.querySelector('[aria-hidden="true"] svg')).not.toBeNull();
  });
});

describe('OrderTracker', () => {
  const steps = [
    { label: 'Placed', status: 'done' as const },
    { label: 'Confirmed', status: 'done' as const },
    { label: 'Packing', status: 'current' as const, caption: 'Today, 10:30' },
    { label: 'Dispatched', status: 'future' as const },
    { label: 'Delivered', status: 'future' as const },
  ];

  it('renders every step from data with its state in words', () => {
    render(<OrderTracker steps={steps} />);
    const items = within(screen.getByRole('list', { name: 'Order progress' })).getAllByRole(
      'listitem',
    );
    expect(items).toHaveLength(5);
    expect(items[0]!).toHaveTextContent('Placed (done)');
    expect(items[2]!).toHaveTextContent('Packing (current)');
    expect(items[2]!).toHaveTextContent('Today, 10:30');
    expect(items[4]!).toHaveTextContent('Delivered (not yet)');
  });

  it('flags only the current node and draws future nodes dashed', () => {
    render(<OrderTracker steps={steps} />);
    const items = screen.getAllByRole('listitem');
    expect(items.filter((li) => li.hasAttribute('aria-current'))).toEqual([items[2]]);
    expect(items[3]!.querySelector('span')?.className).toContain('border-dashed');
  });
});

describe('FreeDeliveryProgress', () => {
  it('renders nothing without a threshold', () => {
    const { container } = render(<FreeDeliveryProgress />);
    expect(container).toBeEmptyDOMElement();
    const nullish = render(<FreeDeliveryProgress remaining={null} percent={50} />);
    expect(nullish.container).toBeEmptyDOMElement();
  });

  it('shows the server-given amount and an accessible progress bar', () => {
    render(<FreeDeliveryProgress remaining="৳250.00" percent={96} />);
    expect(screen.getByText('Add ৳250.00 more')).toBeInTheDocument();
    const bar = screen.getByRole('progressbar', { name: 'Progress to free delivery' });
    expect(bar).toHaveAttribute('aria-valuenow', '96');
  });

  it('draws no bar when there is no percentage', () => {
    render(<FreeDeliveryProgress remaining="৳250.00" />);
    expect(screen.getByText('Add ৳250.00 more')).toBeInTheDocument();
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
  });

  it('clamps the bar to 0-100', () => {
    render(<FreeDeliveryProgress remaining="৳1.00" percent={140} />);
    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '100');
  });
});

describe('HearthLogo', () => {
  it('reads PurrCare Shop and links to the shop by default', () => {
    render(<HearthLogo />);
    const link = screen.getByRole('link', { name: 'PurrCare Shop' });
    expect(link).toHaveAttribute('href', '/shop');
    // Logo A · Cat badge (owner, 2026-10-18): the badge is decoration, hidden from assistive tech.
    expect(link.querySelector('[aria-hidden="true"].bg-accent')).not.toBeNull();
  });

  // Owner, 2026-10-19: the shop's cat-badge logo is the one logo everywhere; the portal variant only links home.
  it('the portal variant is the same cat-badge logo, linking home', () => {
    render(<HearthLogo variant="portal" />);
    const link = screen.getByRole('link', { name: 'PurrCare Shop' });
    expect(link).toHaveAttribute('href', '/');
    expect(link.querySelector('[aria-hidden="true"].bg-accent')).not.toBeNull();
  });
});
