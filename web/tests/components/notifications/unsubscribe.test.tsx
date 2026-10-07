import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { notificationFixtureIds as ids } from '@/mocks/handlers/notifications';
import UnsubscribePage from '@/app/(auth)/unsubscribe/page';

function renderPage() {
  return render(
    <QueryClientProvider
      client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}
    >
      <UnsubscribePage />
    </QueryClientProvider>,
  );
}

function countRequests() {
  const calls = { n: 0 };
  server.use(
    http.post('*/api/notifications/unsubscribe', () => {
      calls.n += 1;
      return HttpResponse.json({ channel: 'Email', wasSubscribed: true });
    }),
  );
  return calls;
}

beforeEach(() => {
  window.history.replaceState(null, '', '/');
});
afterEach(() => {
  window.history.replaceState(null, '', '/');
});

describe('UnsubscribePage — HO-NTF-002 §11.4, ADR-FE-161', () => {
  it('U-D1: nothing is sent on load, and the token is removed from the address bar', async () => {
    const calls = countRequests();
    window.history.replaceState(null, '', '/unsubscribe#token=abc');
    renderPage();

    expect(await screen.findByRole('button', { name: 'Unsubscribe' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Stop marketing messages?' })).toBeInTheDocument();
    expect(
      screen.getByText(
        /You.ll still receive important messages about your account, your cats and your orders\./,
      ),
    ).toBeInTheDocument();
    await new Promise((resolve) => setTimeout(resolve, 60));
    expect(calls.n).toBe(0);
    expect(window.location.hash).toBe('');
    expect(window.location.pathname).toBe('/unsubscribe');
    expect(window.location.href).not.toContain('abc');
  });

  it('only a click sends the token, in the request body', async () => {
    let body: unknown;
    server.use(
      http.post('*/api/notifications/unsubscribe', async ({ request }) => {
        body = await request.json();
        return HttpResponse.json({ channel: 'Email', wasSubscribed: true });
      }),
    );
    window.history.replaceState(null, '', `/unsubscribe#token=${ids.tokenEmailSubscribed}`);
    const user = userEvent.setup();
    renderPage();
    await user.click(await screen.findByRole('button', { name: 'Unsubscribe' }));
    await waitFor(() => expect(body).toEqual({ token: ids.tokenEmailSubscribed }));
  });

  it.each([
    [
      '200 wasSubscribed:true (email)',
      ids.tokenEmailSubscribed,
      "Done. You won't receive marketing emails from us any more.",
    ],
    [
      '200 wasSubscribed:true (sms)',
      ids.tokenSmsSubscribed,
      "Done. You won't receive marketing text messages from us any more.",
    ],
    [
      '200 wasSubscribed:false',
      ids.tokenAlreadyUnsubscribed,
      'You were already unsubscribed from these messages.',
    ],
  ])('U-D2: %s', async (_label, token, message) => {
    window.history.replaceState(null, '', `/unsubscribe#token=${token}`);
    const user = userEvent.setup();
    renderPage();
    await user.click(await screen.findByRole('button', { name: 'Unsubscribe' }));
    expect(await screen.findByText(message)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Manage all your preferences' })).toHaveAttribute(
      'href',
      '/sign-in?next=/portal/account/notifications',
    );
  });

  it('U-D2: 400 InvalidUnsubscribeLink shows the invalid-link message, no code, no button', async () => {
    window.history.replaceState(null, '', `/unsubscribe#token=${ids.tokenInvalid}`);
    const user = userEvent.setup();
    const { container } = renderPage();
    await user.click(await screen.findByRole('button', { name: 'Unsubscribe' }));
    expect(
      await screen.findByText(
        "This link isn't valid any more. You can manage your preferences after signing in.",
      ),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Unsubscribe' })).not.toBeInTheDocument();
    expect(container.textContent).not.toContain('InvalidUnsubscribeLink');
  });

  it('U-D2: 429 shows the wait message and lets the person try again', async () => {
    window.history.replaceState(null, '', `/unsubscribe#token=${ids.tokenRateLimited}`);
    const user = userEvent.setup();
    renderPage();
    await user.click(await screen.findByRole('button', { name: 'Unsubscribe' }));
    expect(
      await screen.findByText('Too many attempts. Please wait a minute and try again.'),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Unsubscribe' })).toBeEnabled();
  });

  it('U-D3: no token -> the invalid-link state and no Unsubscribe button', async () => {
    const calls = countRequests();
    window.history.replaceState(null, '', '/unsubscribe');
    renderPage();
    expect(
      await screen.findByText(
        "This link isn't valid any more. You can manage your preferences after signing in.",
      ),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Unsubscribe' })).not.toBeInTheDocument();
    expect(calls.n).toBe(0);
  });
});
