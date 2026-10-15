import { describe, expect, it, vi } from 'vitest';
import { render, screen, within, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { PolicyAcceptancePrompt } from '@/components/content/PolicyAcceptancePrompt';

// SignOutButton (rendered inside the dialog) calls useRouter() — no App Router is mounted in
// vitest's jsdom environment, matching tests/unit/data-table.test.tsx's own precedent.
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
}));

function renderWithClient(children: React.ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}>{children}</QueryClientProvider>);
}

// The real API's wire shape (`policyKey`), Issues.md #8.
const PENDING = [
  { policyKey: 'terms', title: 'Terms of service', versionNumber: 2 },
  { policyKey: 'privacy', title: 'Privacy policy', versionNumber: 3 },
];

describe('PolicyAcceptancePrompt — ⭐ HO-ADM-002 §11.6, OQ-2/OQ-3', () => {
  it('U-F3: shows both pending policies, nothing pre-ticked, accepting both closes the dialog, sign-out stays reachable', async () => {
    let accepted: string[] = [];
    server.use(
      http.get('*/api/customers/me/policy-acceptances/pending', () =>
        HttpResponse.json(PENDING.filter((p) => !accepted.includes(p.policyKey))),
      ),
      http.post('*/api/customers/me/policy-acceptances', async ({ request }) => {
        const body = (await request.json()) as { policyKey: string };
        accepted = [...accepted, body.policyKey];
        return new HttpResponse(null, { status: 204 });
      }),
      http.post('*/api/auth/sign-out', () => new HttpResponse(null, { status: 204 })),
    );

    const user = userEvent.setup();
    renderWithClient(<PolicyAcceptancePrompt />);

    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText('Terms of service')).toBeInTheDocument();
    expect(within(dialog).getByText('Privacy policy')).toBeInTheDocument();

    // Never pre-ticked — there is no checkbox at all, only an explicit Accept action per policy.
    expect(within(dialog).queryAllByRole('checkbox')).toHaveLength(0);
    expect(within(dialog).getByRole('button', { name: /sign out/i })).toBeInTheDocument();

    const firstAcceptButtons = within(dialog).getAllByRole('button', { name: /^accept$/i });
    expect(firstAcceptButtons).toHaveLength(2);
    await user.click(firstAcceptButtons[0]!);

    await waitFor(() => {
      expect(within(dialog).getAllByRole('button', { name: /^accept$/i })).toHaveLength(1);
    });
    const remainingAcceptButtons = within(dialog).getAllByRole('button', { name: /^accept$/i });
    await user.click(remainingAcceptButtons[0]!);

    // Once both are accepted the pending list is empty, so the dialog renders nothing at all.
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('renders nothing when there is no pending acceptance', async () => {
    server.use(
      http.get('*/api/customers/me/policy-acceptances/pending', () => HttpResponse.json([])),
    );
    renderWithClient(<PolicyAcceptancePrompt />);
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('ADM 14: a failed pending-acceptances request fails closed — a blocking retry dialog, and Try again recovers', async () => {
    let fail = true;
    server.use(
      http.get('*/api/customers/me/policy-acceptances/pending', () =>
        fail
          ? HttpResponse.json({ errorCode: 'Boom' }, { status: 500 })
          : HttpResponse.json([PENDING[0]]),
      ),
    );
    const user = userEvent.setup();
    renderWithClient(<PolicyAcceptancePrompt />);

    const dialog = await screen.findByRole('dialog');
    expect(
      within(dialog).getByRole('heading', { name: /couldn.t check your policies/i }),
    ).toBeVisible();
    expect(within(dialog).getByRole('button', { name: /sign out/i })).toBeInTheDocument();

    fail = false;
    await user.click(within(dialog).getByRole('button', { name: /try again/i }));
    expect(await screen.findByText('Terms of service')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^accept$/i })).toBeInTheDocument();
  });

  it('Issues.md #8: each policy shows its title and "Read the full text" opens that policy', async () => {
    server.use(
      http.get('*/api/customers/me/policy-acceptances/pending', () => HttpResponse.json(PENDING)),
    );
    renderWithClient(<PolicyAcceptancePrompt />);
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText('Terms of service')).toBeInTheDocument();
    const links = within(dialog).getAllByRole('link', { name: /read the full text/i });
    expect(links.map((a) => a.getAttribute('href'))).toEqual([
      '/policies/terms',
      '/policies/privacy',
    ]);
  });
});
