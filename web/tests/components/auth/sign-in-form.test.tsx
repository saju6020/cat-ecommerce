import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { SignInForm, type SignInFormProps } from '@/components/auth/SignInForm';
import { useSignInCustomer } from '@/lib/hooks/useAuthMutations';
import { accountsFixtureIds, resetAccountsMockData } from '@/mocks/handlers/areas/accounts';

/** ARCH gate HIGH 3 (first pass) — stand-in for `app/(auth)/sign-in/page.tsx`, which is what
 *  actually calls `useSignInCustomer()` in the real app; see register-form.test.tsx's own
 *  identical-purpose wrapper for why this is preferable to moving every test here to a page test. */
function CustomerSignInFormWithHooks({ onSuccess }: Pick<SignInFormProps, 'onSuccess'>) {
  const mutation = useSignInCustomer();
  return (
    <SignInForm
      audience="customer"
      onSignIn={(values) => mutation.mutateAsync(values)}
      isPending={mutation.isPending}
      error={mutation.error}
      onSuccess={onSuccess}
    />
  );
}

function renderWithClient(children: React.ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}>{children}</QueryClientProvider>);
}

beforeEach(() => {
  resetAccountsMockData();
});

describe('SignInForm — US-ACC-04/14, ADR-FE-007', () => {
  it('U-C1: an unknown identifier and a wrong password give an identical message, clear the password, keep the identifier', async () => {
    const user = userEvent.setup();
    renderWithClient(<CustomerSignInFormWithHooks onSuccess={vi.fn()} />);

    await user.type(screen.getByLabelText(/mobile number or email/i), 'nobody@example.com');
    await user.type(screen.getByLabelText(/^password/i), 'whatever12345');
    await user.click(screen.getByRole('button', { name: /^sign in$/i }));
    const unknownMessage = await screen.findByRole('alert');

    await user.clear(screen.getByLabelText(/mobile number or email/i));
    await user.type(
      screen.getByLabelText(/mobile number or email/i),
      accountsFixtureIds.customerEmail,
    );
    await user.type(screen.getByLabelText(/^password/i), 'the-wrong-password');
    await user.click(screen.getByRole('button', { name: /^sign in$/i }));
    const wrongPasswordMessage = await screen.findByRole('alert');

    expect(wrongPasswordMessage.textContent).toBe(unknownMessage.textContent);
    expect((screen.getByLabelText(/^password/i) as HTMLInputElement).value).toBe('');
    expect((screen.getByLabelText(/mobile number or email/i) as HTMLInputElement).value).toBe(
      accountsFixtureIds.customerEmail,
    );
  });

  it('U-B11-equivalent: a 429 reads minutes, never raw seconds', async () => {
    const user = userEvent.setup();
    renderWithClient(<CustomerSignInFormWithHooks onSuccess={vi.fn()} />);
    await user.type(
      screen.getByLabelText(/mobile number or email/i),
      accountsFixtureIds.trigger429,
    );
    await user.type(screen.getByLabelText(/^password/i), 'whatever12345');
    await user.click(screen.getByRole('button', { name: /^sign in$/i }));

    const notice = await screen.findByText(/wait 1 minute/i);
    expect(notice.textContent).not.toMatch(/\b60\b/);
  });

  it('a successful sign-in calls onSuccess once with the session result', async () => {
    const user = userEvent.setup();
    const onSuccess = vi.fn();
    renderWithClient(<CustomerSignInFormWithHooks onSuccess={onSuccess} />);
    await user.type(
      screen.getByLabelText(/mobile number or email/i),
      accountsFixtureIds.customerEmail,
    );
    await user.type(screen.getByLabelText(/^password/i), accountsFixtureIds.customerPassword);
    await user.click(screen.getByRole('button', { name: /^sign in$/i }));

    await waitFor(() => expect(onSuccess).toHaveBeenCalledTimes(1));
    expect(onSuccess).toHaveBeenCalledWith(
      expect.objectContaining({ customerId: accountsFixtureIds.customerId }),
    );
  });

  it('U-X2: errorCode never appears in the DOM on a 400', async () => {
    server.use(
      http.post('*/api/auth/sign-in', () =>
        HttpResponse.json({ error: 'invalid_credentials', message: 'nope' }, { status: 401 }),
      ),
    );
    const user = userEvent.setup();
    renderWithClient(<CustomerSignInFormWithHooks onSuccess={vi.fn()} />);
    await user.type(screen.getByLabelText(/mobile number or email/i), 'x@example.com');
    await user.type(screen.getByLabelText(/^password/i), 'whatever12345');
    await user.click(screen.getByRole('button', { name: /^sign in$/i }));
    await screen.findByRole('alert');

    expect(document.body.textContent).not.toMatch(/invalid_credentials/);
  });

  it('SEC gate MEDIUM (first pass): method="post"', () => {
    renderWithClient(<CustomerSignInFormWithHooks onSuccess={vi.fn()} />);
    expect(document.querySelector('form')).toHaveAttribute('method', 'post');
  });

  it('2026-10-16: shows a spinner while signing in, and stays busy after success while the next page loads', async () => {
    let release: () => void = () => {};
    const onSuccess = vi.fn();
    renderWithClient(
      <SignInForm
        audience="customer"
        onSignIn={() =>
          new Promise((resolve) => {
            release = () => resolve({} as never);
          })
        }
        isPending={false}
        error={null}
        onSuccess={onSuccess}
      />,
    );
    const user = userEvent.setup();
    await user.type(screen.getByLabelText(/mobile number or email/i), 'a@example.com');
    await user.type(screen.getByLabelText(/^password/i), 'whatever12345');
    await user.click(screen.getByRole('button', { name: /^sign in$/i }));

    const busy = await screen.findByRole('button', { name: /signing in/i });
    expect(busy).toBeDisabled();
    expect(busy).toHaveAttribute('aria-busy', 'true');
    expect(busy.querySelector('svg.animate-spin')).not.toBeNull();

    release();
    await waitFor(() => expect(onSuccess).toHaveBeenCalled());
    // Navigation is the caller's job and is still under way: the button must not go back to "Sign in".
    expect(screen.getByRole('button', { name: /signing in/i })).toBeDisabled();
    expect(screen.queryByRole('button', { name: /^sign in$/i })).toBeNull();
  });

  it('after a failed sign-in the button is usable again', async () => {
    const user = userEvent.setup();
    renderWithClient(<CustomerSignInFormWithHooks onSuccess={vi.fn()} />);
    await user.type(screen.getByLabelText(/mobile number or email/i), 'nobody@example.com');
    await user.type(screen.getByLabelText(/^password/i), 'whatever12345');
    await user.click(screen.getByRole('button', { name: /^sign in$/i }));
    await screen.findByRole('alert');
    expect(screen.getByRole('button', { name: /^sign in$/i })).toBeEnabled();
  });
});
