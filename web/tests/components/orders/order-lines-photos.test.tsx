import { describe, expect, it } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { OrderLines, type DisplayLine } from '@/components/orders/OrderLines';

const money = { amount: '300.00', currencyCode: 'BDT' };

function line(key: string, imageUrl?: string | null): DisplayLine {
  return {
    key,
    productName: `Product ${key}`,
    variantLabel: '400 g',
    cat: null,
    quantity: 1,
    unitPrice: money,
    lineTotal: money,
    imageUrl,
  };
}

// 2026-10-16: checkout review, order placed and my order show each line's photo, not only a drawn pack.
describe('OrderLines photos (hearth)', () => {
  it('shows the photo when the line has one, and the pack drawing when it does not', () => {
    const { container } = render(
      <OrderLines
        lines={[line('a', 'http://api.test/api/files/a/content?sig=x'), line('b', null)]}
        caption="Items in this order"
        appearance="hearth"
      />,
    );
    const items = within(screen.getByRole('list', { name: 'Items in this order' })).getAllByRole(
      'listitem',
    );
    expect(items[0]!.querySelector('img')).toHaveAttribute(
      'src',
      'http://api.test/api/files/a/content?sig=x',
    );
    expect(items[1]!.querySelector('img')).toBeNull();
    expect(container.querySelectorAll('svg').length).toBeGreaterThan(0);
  });

  it('falls back to the pack drawing when the photo fails to load', () => {
    render(
      <OrderLines
        lines={[line('a', 'http://api.test/api/files/a/content?sig=x')]}
        caption="Items in this order"
        appearance="hearth"
      />,
    );
    const item = screen.getByRole('listitem');
    fireEvent.error(item.querySelector('img')!);
    expect(item.querySelector('img')).toBeNull();
  });

  it('staff (plain) lines show no tile at all', () => {
    render(
      <OrderLines lines={[line('a', 'http://api.test/x')]} caption="Lines" appearance="plain" />,
    );
    expect(screen.getByRole('listitem').querySelector('img')).toBeNull();
  });
});
