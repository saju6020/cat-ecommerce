import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import ForgotPasswordPage from '@/app/(auth)/forgot-password/page';
import { ForgotPasswordForm } from '@/components/auth/ForgotPasswordForm';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  useSearchParams: () => new URLSearchParams(),
  usePathname: () => '/forgot-password',
}));

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

// Hearth "Welcome home" (owner-approved 2026-10-16).
describe('Hearth forgot password', () => {
  it('shows the welcome panel and, after sending, "Check your messages" with the one non-disclosing message', async () => {
    renderWithClient(<ForgotPasswordPage />);
    expect(screen.getByRole('region', { name: 'How it works' })).toHaveTextContent('It happens to');
    expect(
      screen.getByRole('heading', { level: 1, name: 'Reset your password' }),
    ).toBeInTheDocument();
    expect(screen.getByText(/Ask at the clinic reception/)).toBeInTheDocument();

    await userEvent.type(screen.getByLabelText(/Mobile number or email/), 'nobody@example.com');
    await userEvent.click(screen.getByRole('button', { name: 'Send reset link' }));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Check your messages' }),
    ).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent(
      'If an account matches, we’ve sent a link to reset the password. It expires in 15 minutes.',
    );
    expect(screen.getByRole('link', { name: 'Back to sign in' })).toHaveAttribute(
      'href',
      '/sign-in',
    );
  });

  it('staff keep the plain form: no reception line, the back link stays under the button', () => {
    renderWithClient(
      <ForgotPasswordForm
        audience="staff"
        onRequestReset={vi.fn()}
        isPending={false}
        error={null}
      />,
    );
    expect(screen.queryByText(/Ask at the clinic reception/)).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Back to sign in' })).toHaveAttribute(
      'href',
      '/staff/sign-in',
    );
  });
});
