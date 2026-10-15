import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { ReorderButton } from '@/components/portal/reorder/ReorderButton';
import { portalIds, reorderResults } from '@/mocks/handlers/portal/fixtures';
import { portalReorderFixtureHandlers, reorderCalls } from '@/mocks/handlers/portal';
import { CART } from '@/lib/routing/portal-links';
import { PORTAL_DASHBOARD_QUERY_KEY } from '@/lib/hooks/usePortalDashboard';
import { CART_QUERY_KEY } from '@/lib/hooks/useCart';
import { newClient, renderWithClient, resetPortalMock } from './helpers';

// The app's mock re-order is HO-ORD-002's; this suite answers from the portal's fixture orders.
beforeEach(() => server.use(...portalReorderFixtureHandlers));
afterEach(() => resetPortalMock());

function renderButton(orderId: string, client = newClient()) {
  return renderWithClient(
    <ReorderButton orderId={orderId} orderNumber="SO-2026-00042" canReorder />,
    client,
  );
}

const orderAgain = () => screen.getByRole('button', { name: 'Order SO-2026-00042 again' });

describe('ReorderButton — Orders’ endpoint (ADR-FE-120, ADR-FE-123)', () => {
  it('U-B1: a double click is one request; busy text; re-enabled after the answer', async () => {
    let release: () => void = () => {};
    let requests = 0;
    const gate = new Promise<void>((resolve) => (release = resolve));
    server.use(
      http.post('*/api/customers/me/orders/:orderId/reorder', async () => {
        requests += 1;
        await gate;
        return HttpResponse.json(reorderResults.allAdded);
      }),
    );
    renderButton(portalIds.orders.delivered);
    const user = userEvent.setup();

    const button = orderAgain();
    await user.dblClick(button);
    expect(await screen.findByRole('button', { name: /Adding to basket/ })).toHaveAttribute(
      'aria-disabled',
      'true',
    );
    await act(async () => release());

    expect(await screen.findByRole('dialog')).toBeVisible();
    expect(requests).toBe(1); // POR row 40: the double click reached the server once
    await user.click(screen.getByRole('button', { name: 'Close' }));
    expect(orderAgain()).not.toHaveAttribute('aria-disabled');
  });

  it('E-B3 (component): two deliberate re-orders are two requests with different Idempotency-Keys', async () => {
    const keys: Array<string | null> = [];
    server.use(
      http.post('*/api/customers/me/orders/:orderId/reorder', ({ request }) => {
        keys.push(request.headers.get('Idempotency-Key'));
        return HttpResponse.json(reorderResults.allAdded);
      }),
    );
    renderButton(portalIds.orders.delivered);
    const user = userEvent.setup();
    await user.click(orderAgain());
    await screen.findByRole('dialog');
    await user.click(screen.getByRole('button', { name: 'Close' }));
    await user.click(orderAgain());
    await screen.findByRole('dialog');
    expect(keys).toHaveLength(2);
    expect(keys[0]).toMatch(/^[0-9a-f-]{36}$/i);
    expect(keys[1]).toMatch(/^[0-9a-f-]{36}$/i);
    expect(keys[1]).not.toBe(keys[0]);
  });

  it('U-B1b: the request carries a UUID Idempotency-Key; a retry after a network error reuses it, a second order again gets a new one', async () => {
    const keys: Array<string | null> = [];
    let attempt = 0;
    server.use(
      http.post('*/api/customers/me/orders/:orderId/reorder', ({ request }) => {
        keys.push(request.headers.get('Idempotency-Key'));
        attempt += 1;
        if (attempt === 1) return HttpResponse.error();
        return HttpResponse.json(reorderResults.allAdded);
      }),
    );
    renderButton(portalIds.orders.delivered);
    const user = userEvent.setup();

    await user.click(orderAgain());
    expect(await screen.findByText('We couldn’t add these items.')).toBeVisible();
    await user.click(screen.getByRole('button', { name: 'Try again' }));
    expect(await screen.findByText('Added to your basket')).toBeVisible();
    await user.click(screen.getByRole('button', { name: 'Close' }));
    await user.click(orderAgain());
    await screen.findByText('Added to your basket');

    expect(keys).toHaveLength(3);
    expect(keys[0]).toMatch(/^[0-9a-f-]{36}$/i);
    expect(keys[1]).toBe(keys[0]); // retry of the same click: same key
    expect(keys[2]).not.toBe(keys[0]); // a deliberate second re-order: new key
  });

  it('U-B2: a mixed answer reports the count in a live region and each not-added line with its reason as text', async () => {
    renderButton(portalIds.orders.mixed);
    await userEvent.setup().click(orderAgain());

    const dialog = await screen.findByRole('dialog', { name: 'Some items were added' });
    const summary = within(dialog).getByRole('status');
    expect(summary).toHaveTextContent(
      '3 items from order SO-2026-00042 were added to your basket.',
    );
    expect(summary).toHaveTextContent('5 items could not be added');
    expect(summary).toHaveAttribute('aria-live', 'polite');

    const list = within(dialog).getByRole('region', { name: /Not added \(5\)/ });
    expect(within(list).getAllByRole('listitem')).toHaveLength(5);
    expect(within(list).getByText('Feliway Classic diffuser refill')).toBeVisible();
    expect(within(list).getByText('Out of stock right now.')).toBeVisible();
    expect(within(list).getByText('No longer sold.')).toBeVisible();
    expect(within(list).getByText(/Needs a prescription or vet authorisation/)).toBeVisible();
    expect(within(list).getByText(/already holds the most different items/)).toBeVisible();
  });

  it('U-B8: an unknown reason code gets the generic sentence and the code is never shown', async () => {
    renderButton(portalIds.orders.mixed);
    await userEvent.setup().click(orderAgain());
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText('Mystery item')).toBeVisible();
    expect(within(dialog).getByText('This could not be added to your basket.')).toBeVisible();
    expect(dialog.textContent).not.toContain('SomethingTheClientHasNeverHeardOf');
    expect(dialog.textContent).not.toContain('PrescriptionNotAuthorised');
  });

  it('U-B3: nothing added is headed "Nothing could be added" and still offers View basket', async () => {
    renderButton(portalIds.orders.nothing);
    await userEvent.setup().click(orderAgain());
    const dialog = await screen.findByRole('dialog', { name: 'Nothing could be added' });
    expect(within(dialog).getByText(/Your basket was not changed/)).toBeVisible();
    expect(within(dialog).getByRole('link', { name: 'View basket' })).toHaveAttribute('href', CART);
  });

  it.each([
    [portalIds.orders.notFound, 'We couldn’t find that order.'],
    [portalIds.orders.refused, 'This order can’t be ordered again.'],
    [portalIds.orders.conflict, 'Your basket changed while we were adding items.'],
  ])('U-B4: order %s answers with one plain message and no code', async (orderId, title) => {
    renderButton(orderId);
    await userEvent.setup().click(orderAgain());
    const dialog = await screen.findByRole('dialog', { name: title });
    expect(within(dialog).getByRole('status')).toBeVisible();
    for (const raw of [
      'OrderNotFound',
      'OrderNotReorderable',
      'StaleVersion',
      '404',
      '400',
      '409',
    ]) {
      expect(dialog.textContent).not.toContain(raw);
    }
    expect(within(dialog).queryByRole('link', { name: 'View basket' })).toBeNull();
  });

  it('U-B5: success invalidates the basket and does NOT invalidate the dashboard', async () => {
    const client = newClient();
    client.setQueryData(PORTAL_DASHBOARD_QUERY_KEY, { generatedOn: 'x' });
    client.setQueryData(CART_QUERY_KEY, { lines: [] });
    renderButton(portalIds.orders.delivered, client);
    await userEvent.setup().click(orderAgain());
    await screen.findByRole('dialog');
    await waitFor(() => expect(client.getQueryState(CART_QUERY_KEY)?.isInvalidated).toBe(true));
    expect(client.getQueryState(PORTAL_DASHBOARD_QUERY_KEY)?.isInvalidated).toBe(false);
  });

  it('U-B6: closing the dialog returns focus to the button that opened it', async () => {
    renderButton(portalIds.orders.delivered);
    const user = userEvent.setup();
    orderAgain().focus();
    await user.keyboard('{Enter}');
    await screen.findByRole('dialog');
    await user.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(orderAgain()).toHaveFocus();
  });

  it('U-X3: no browser storage and no console output while re-ordering', async () => {
    const setItem = vi.spyOn(Storage.prototype, 'setItem');
    const log = vi.spyOn(console, 'log');
    const info = vi.spyOn(console, 'info');
    const warn = vi.spyOn(console, 'warn');
    renderButton(portalIds.orders.mixed);
    await userEvent.setup().click(orderAgain());
    await screen.findByRole('dialog');
    expect(setItem).not.toHaveBeenCalled();
    expect(log).not.toHaveBeenCalled();
    expect(info).not.toHaveBeenCalled();
    expect(warn).not.toHaveBeenCalled();
    expect(reorderCalls).toHaveLength(1);
    vi.restoreAllMocks();
  });
});

describe('Idempotency-Key renewal on failure (CR: ORD-002 shouldRenewIdempotencyKey rule)', () => {
  const violation = (errorCode: string) => ({
    isValid: false,
    errors: [{ propertyName: 'Request', errorCode, errorMessage: '', resourceName: 'Order' }],
  });

  async function keysAfterFailureThenRetry(failure: () => Response) {
    const keys: Array<string | null> = [];
    let attempt = 0;
    server.use(
      http.post('*/api/customers/me/orders/:orderId/reorder', ({ request }) => {
        keys.push(request.headers.get('Idempotency-Key'));
        attempt += 1;
        return attempt === 1 ? failure() : HttpResponse.json(reorderResults.allAdded);
      }),
    );
    renderButton(portalIds.orders.delivered);
    const user = userEvent.setup();
    await user.click(orderAgain());
    await screen.findByRole('dialog');
    await user.click(screen.getByRole('button', { name: 'Try again' }));
    await screen.findByText('Added to your basket');
    expect(keys).toHaveLength(2);
    return keys;
  }

  it.each([
    ['400', () => HttpResponse.json(violation('OrderNotReorderable'), { status: 400 })],
    ['404', () => new HttpResponse(null, { status: 404 })],
    ['409 StaleVersion', () => HttpResponse.json(violation('StaleVersion'), { status: 409 })],
  ])('a definitive %s renews the key', async (_label, failure) => {
    const keys = await keysAfterFailureThenRetry(failure);
    expect(keys[1]).not.toBe(keys[0]);
  });

  it('a 5xx keeps the key', async () => {
    const keys = await keysAfterFailureThenRetry(() => new HttpResponse(null, { status: 503 }));
    expect(keys[1]).toBe(keys[0]);
  });

  it('409 RequestInProgress keeps the key and says the first request is still processing', async () => {
    server.use(
      http.post('*/api/customers/me/orders/:orderId/reorder', () =>
        HttpResponse.json(violation('RequestInProgress'), { status: 409 }),
      ),
    );
    renderButton(portalIds.orders.delivered);
    await userEvent.setup().click(orderAgain());
    const dialog = await screen.findByRole('dialog', { name: 'We’re still adding your items.' });
    expect(within(dialog).getByText(/still being processed/)).toBeVisible();
    expect(dialog.textContent).not.toContain('RequestInProgress');
    expect(dialog.textContent).not.toContain('basket changed');
  });

  it('409 RequestInProgress keeps the key on the retry', async () => {
    const keys = await keysAfterFailureThenRetry(() =>
      HttpResponse.json(violation('RequestInProgress'), { status: 409 }),
    );
    expect(keys[1]).toBe(keys[0]);
  });

  it('a network error keeps the key', async () => {
    const keys = await keysAfterFailureThenRetry(() => HttpResponse.error());
    expect(keys[1]).toBe(keys[0]);
  });

  it('a 400 IdempotencyKeyRequired reads as a send problem, not "can’t be ordered again"', async () => {
    server.use(
      http.post('*/api/customers/me/orders/:orderId/reorder', () =>
        HttpResponse.json(violation('IdempotencyKeyRequired'), { status: 400 }),
      ),
    );
    renderButton(portalIds.orders.delivered);
    await userEvent.setup().click(orderAgain());
    expect(
      await screen.findByRole('dialog', { name: 'We couldn’t send that request.' }),
    ).toBeVisible();
  });
});
