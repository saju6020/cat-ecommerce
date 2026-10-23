import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import RegisterPage from '@/app/(auth)/register/page';
import { resetAccountsMockData } from '@/mocks/handlers/areas/accounts';

const push = vi.fn();
let search = new URLSearchParams();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push, replace: vi.fn() }),
  usePathname: () => '/register',
  useSearchParams: () => search,
}));

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <RegisterPage />
    </QueryClientProvider>,
  );
}

async function register(user: ReturnType<typeof userEvent.setup>) {
  await user.type(await screen.findByLabelText(/full name/i), 'Jamie Owner');
  await user.type(screen.getByLabelText(/mobile number/i), '01712345678');
  await user.type(screen.getByLabelText(/^password/i), 'a-very-long-password');
  await screen.findByAltText(/security check/i);
  await user.type(screen.getByLabelText(/security check/i), 'ABCDE');
  await user.click(screen.getByRole('button', { name: 'Create account' }));
}

beforeEach(() => {
  resetAccountsMockData();
  push.mockClear();
  search = new URLSearchParams();
});

// Owner, 2026-10-19: a customer who registers is signed in straight away and sent where they were going.
describe('Register page — signed in after registering', () => {
  it('a visitor sent from Checkout lands on /checkout, signed in with what they just typed', async () => {
    search = new URLSearchParams('next=/checkout');
    const signIns: unknown[] = [];
    server.use(
      http.post('*/api/auth/sign-in', async ({ request }) => {
        signIns.push(await request.json());
        return HttpResponse.json({
          customerId: 'c-1',
          fullName: 'Jamie Owner',
          expiresOn: '2026-10-26T00:00:00Z',
          recordStatus: 'Complete',
        });
      }),
    );
    const user = userEvent.setup();
    renderPage();

    await register(user);

    await waitFor(() => expect(push).toHaveBeenCalledWith('/checkout'));
    expect(signIns).toEqual([{ identifier: '01712345678', password: 'a-very-long-password' }]);
    expect(
      screen.queryByRole('heading', { name: 'Your account is ready' }),
    ).not.toBeInTheDocument();
  });

  it('with no destination, lands on My orders', async () => {
    server.use(
      http.post('*/api/auth/sign-in', () =>
        HttpResponse.json({
          customerId: 'c-1',
          fullName: 'Jamie Owner',
          expiresOn: '2026-10-26T00:00:00Z',
          recordStatus: 'Complete',
        }),
      ),
    );
    const user = userEvent.setup();
    renderPage();

    await register(user);

    await waitFor(() => expect(push).toHaveBeenCalledWith('/portal/orders'));
  });

  it('a destination outside the customer area is ignored', async () => {
    search = new URLSearchParams('next=https://evil.example/steal');
    server.use(
      http.post('*/api/auth/sign-in', () =>
        HttpResponse.json({
          customerId: 'c-1',
          fullName: 'Jamie Owner',
          expiresOn: '2026-10-26T00:00:00Z',
          recordStatus: 'Complete',
        }),
      ),
    );
    const user = userEvent.setup();
    renderPage();

    await register(user);

    await waitFor(() => expect(push).toHaveBeenCalledWith('/portal/orders'));
  });

  it('if that sign-in fails, the account still exists and "Your account is ready" offers Sign in, keeping the destination', async () => {
    search = new URLSearchParams('next=/checkout');
    server.use(http.post('*/api/auth/sign-in', () => HttpResponse.json({}, { status: 503 })));
    const user = userEvent.setup();
    renderPage();

    await register(user);

    expect(
      await screen.findByRole('heading', { name: 'Your account is ready' }),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Sign in' })).toHaveAttribute(
      'href',
      '/sign-in?next=%2Fcheckout',
    );
    expect(push).not.toHaveBeenCalled();
  });
});
