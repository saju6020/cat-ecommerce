import { describe, expect, it, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { ResetPasswordForm } from '@/components/auth/ResetPasswordForm';
import { ForgotPasswordForm } from '@/components/auth/ForgotPasswordForm';
import {
  useConfirmPasswordReset,
  useRequestCustomerPasswordReset,
} from '@/lib/hooks/useAuthMutations';
import { accountsFixtureIds, resetAccountsMockData } from '@/mocks/handlers/areas/accounts';

/** ARCH gate HIGH 3 (first pass) — stand-ins for the real pages; see register-form.test.tsx's
 *  own identical-purpose wrapper comment. */
function ForgotPasswordFormWithHooks({ audience }: { audience: 'customer' }) {
  const mutation = useRequestCustomerPasswordReset();
  return (
    <ForgotPasswordForm
      audience={audience}
      onRequestReset={(identifier) => mutation.mutateAsync({ identifier })}
      isPending={mutation.isPending}
      error={mutation.error}
    />
  );
}

function ResetPasswordFormWithHooks() {
  const mutation = useConfirmPasswordReset();
  return (
    <ResetPasswordForm
      onConfirm={(request) => mutation.mutateAsync(request)}
      isPending={mutation.isPending}
    />
  );
}

function renderWithClient(children: React.ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}>{children}</QueryClientProvider>);
}

beforeEach(() => {
  resetAccountsMockData();
  window.history.replaceState(null, '', '/reset-password');
});

describe('ForgotPasswordForm — U-D1', () => {
  it('renders the fixed client confirmation on any 200, never the response body', async () => {
    const user = userEvent.setup();
    renderWithClient(<ForgotPasswordFormWithHooks audience="customer" />);
    await user.type(screen.getByLabelText(/mobile number or email/i), 'anyone@example.com');
    await user.click(screen.getByRole('button', { name: /send reset link/i }));

    expect(
      await screen.findByText(/if an account matches, we.ve sent a link to reset the password/i),
    ).toBeInTheDocument();
  });

  it('SEC gate MEDIUM (first pass): method="post"', () => {
    renderWithClient(<ForgotPasswordFormWithHooks audience="customer" />);
    expect(document.querySelector('form')).toHaveAttribute('method', 'post');
  });
});

describe('ResetPasswordForm — U-D2/D3/D4/D6, ADR-FE-007', () => {
  it('U-D3: no token in the fragment shows the invalid-link state', async () => {
    window.history.replaceState(null, '', '/reset-password');
    renderWithClient(<ResetPasswordFormWithHooks />);
    expect(await screen.findByText(/this link is invalid or has expired/i)).toBeInTheDocument();
  });

  it('U-D2: reads the token from the hash, then strips it from the address bar', async () => {
    window.location.hash = '#token=abc123';
    renderWithClient(<ResetPasswordFormWithHooks />);
    await screen.findByLabelText(/new password/i);
    await waitFor(() =>
      expect(window.location.pathname + window.location.hash).toBe('/reset-password'),
    );
  });

  it('U-D4: InvalidPasswordResetToken renders one invalid/expired state with a "request another" link', async () => {
    window.location.hash = '#token=expired-token';
    const user = userEvent.setup();
    renderWithClient(<ResetPasswordFormWithHooks />);
    await user.type(await screen.findByLabelText(/new password/i), 'a-brand-new-password');
    await user.click(screen.getByRole('button', { name: /set new password/i }));

    expect(await screen.findByText(/this link is invalid or has expired/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /request another link/i })).toBeInTheDocument();
  });

  it('U-D6: after success, links to both sign-in pages with no account-kind hint', async () => {
    window.location.hash = '#token=valid-customer-token';
    const user = userEvent.setup();
    renderWithClient(<ResetPasswordFormWithHooks />);
    await user.type(await screen.findByLabelText(/new password/i), 'a-brand-new-password');
    await user.click(screen.getByRole('button', { name: /set new password/i }));

    expect(await screen.findByText(/your password has been set/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /sign in as a customer/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /sign in as staff/i })).toBeInTheDocument();
  });

  it('SEC gate MEDIUM (first pass): both forms use method="post"', async () => {
    window.location.hash = '#token=valid-customer-token';
    renderWithClient(<ResetPasswordFormWithHooks />);
    await screen.findByLabelText(/new password/i);
    expect(document.querySelector('form')).toHaveAttribute('method', 'post');
  });

  it('U-D5: BreachedPassword shows a field error, and a corrected second attempt reuses the same token', async () => {
    window.location.hash = '#token=valid-customer-token';
    const capturedTokens: string[] = [];
    server.use(
      http.post('*/api/auth/password-reset/confirm', async ({ request }) => {
        const body = (await request.json()) as { token: string; newPassword: string };
        capturedTokens.push(body.token);
        if (body.newPassword === accountsFixtureIds.triggerBreachedPassword) {
          return HttpResponse.json(
            {
              isValid: false,
              errors: [
                {
                  propertyName: 'NewPassword',
                  errorCode: 'BreachedPassword',
                  errorMessage:
                    'This password has appeared in a data breach. Choose a different one.',
                  resourceName: '',
                },
              ],
            },
            { status: 400 },
          );
        }
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const user = userEvent.setup();
    renderWithClient(<ResetPasswordFormWithHooks />);
    const passwordField = await screen.findByLabelText(/new password/i);

    await user.type(passwordField, accountsFixtureIds.triggerBreachedPassword);
    await user.click(screen.getByRole('button', { name: /set new password/i }));
    expect(await screen.findByText(/appeared in a data breach/i)).toBeInTheDocument();

    await user.clear(passwordField);
    await user.type(passwordField, 'a-brand-new-password');
    await user.click(screen.getByRole('button', { name: /set new password/i }));
    expect(await screen.findByText(/your password has been set/i)).toBeInTheDocument();

    expect(capturedTokens).toEqual(['valid-customer-token', 'valid-customer-token']);
  });
});
