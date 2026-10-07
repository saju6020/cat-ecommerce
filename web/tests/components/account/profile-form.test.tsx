import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { ProfileForm } from '@/components/account/ProfileForm';
import { useUpdateCustomerProfile } from '@/lib/hooks/useAccountMutations';
import { ApiRateLimitedError } from '@/lib/api/client';
import type { CustomerProfile } from '@/lib/api/account';

// UnsavedChangesGuard (rendered by ProfileForm whenever the form is dirty) calls useRouter() —
// ARCH gate MEDIUM (first pass) fix, replacing window.confirm with the shared ConfirmDialog.
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

/** ARCH gate HIGH 3 (first pass) — stand-in for app/(portal)/portal/account/profile/page.tsx; see
 *  register-form.test.tsx's own identical-purpose wrapper comment. */
function ProfileFormWithHooks({ profile }: { profile: CustomerProfile }) {
  const mutation = useUpdateCustomerProfile();
  return (
    <ProfileForm
      profile={profile}
      onSave={(request) => mutation.mutateAsync(request)}
      isPending={mutation.isPending}
      rateLimitError={mutation.error instanceof ApiRateLimitedError ? mutation.error : null}
    />
  );
}

function renderWithClient(children: React.ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}>{children}</QueryClientProvider>);
}

const baseProfile: CustomerProfile = {
  fullName: 'Sam Rahman',
  mobileNumber: '01799999999',
  emailAddress: 'sam@example.com',
  hasMobileNumber: true,
  hasEmailAddress: true,
  canEditEmail: true,
  additionalPhoneNumbers: [],
  registeredOn: '2024-01-01T00:00:00Z',
};

describe('ProfileForm — US-ACC-06', () => {
  it('U-E1: canEditEmail false renders email read-only with an explanation; mobile is always read-only', () => {
    renderWithClient(<ProfileFormWithHooks profile={{ ...baseProfile, canEditEmail: false }} />);
    expect(screen.getByLabelText(/^email$/i)).toHaveAttribute('readonly');
    expect(screen.getByText(/your email is how you sign in/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/primary mobile number/i)).toHaveAttribute('readonly');
    expect(screen.getByText(/your mobile number is how you sign in/i)).toBeInTheDocument();
  });

  it('U-E2: four loaded additional numbers show the limit explanation, not the add button', () => {
    renderWithClient(
      <ProfileFormWithHooks
        profile={{
          ...baseProfile,
          additionalPhoneNumbers: ['01711111111', '01711111112', '01711111113', '01711111114'],
        }}
      />,
    );
    expect(screen.getByText(/up to 3 additional numbers/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /add a number/i })).not.toBeInTheDocument();
  });

  it('shows and requires "Current password" only while the email is actually being changed', async () => {
    const user = userEvent.setup();
    renderWithClient(<ProfileFormWithHooks profile={baseProfile} />);
    expect(screen.queryByLabelText(/current password/i)).not.toBeInTheDocument();

    const emailInput = screen.getByLabelText(/^email$/i);
    await user.clear(emailInput);
    await user.type(emailInput, 'new-email@example.com');
    expect(await screen.findByLabelText(/current password/i)).toBeInTheDocument();

    await user.clear(emailInput);
    await user.type(emailInput, baseProfile.emailAddress!);
    await waitFor(() =>
      expect(screen.queryByLabelText(/current password/i)).not.toBeInTheDocument(),
    );
  });

  it('ReauthenticationFailed on an email change surfaces as a field error on Current password', async () => {
    server.use(
      http.put('*/api/customers/me', () =>
        HttpResponse.json(
          {
            isValid: false,
            errors: [
              {
                propertyName: 'CurrentPassword',
                errorCode: 'ReauthenticationFailed',
                errorMessage: 'That current password is incorrect.',
                resourceName: '',
              },
            ],
          },
          { status: 400 },
        ),
      ),
    );
    const user = userEvent.setup();
    renderWithClient(<ProfileFormWithHooks profile={baseProfile} />);
    const emailInput = screen.getByLabelText(/^email$/i);
    await user.clear(emailInput);
    await user.type(emailInput, 'new-email@example.com');
    await user.type(await screen.findByLabelText(/current password/i), 'wrong-password');
    await user.click(screen.getByRole('button', { name: /^save$/i }));

    expect(await screen.findByText(/that current password is incorrect/i)).toBeInTheDocument();
    // CODE_REVIEW LOW (first pass) — cleared on failure, matching every other password-bearing
    // form in this slice.
    expect((screen.getByLabelText(/current password/i) as HTMLInputElement).value).toBe('');
  });

  it('CODE_REVIEW LOW (first pass): changing the email with a blank current password is blocked client-side, never reaching the server', async () => {
    let putCalled = false;
    server.use(
      http.put('*/api/customers/me', () => {
        putCalled = true;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const user = userEvent.setup();
    renderWithClient(<ProfileFormWithHooks profile={baseProfile} />);
    const emailInput = screen.getByLabelText(/^email$/i);
    await user.clear(emailInput);
    await user.type(emailInput, 'new-email@example.com');
    await screen.findByLabelText(/current password/i); // leave it blank
    await user.click(screen.getByRole('button', { name: /^save$/i }));

    expect(
      await screen.findByText(/enter your current password to change your email/i),
    ).toBeInTheDocument();
    expect(putCalled).toBe(false);
  });

  it('SEC gate MEDIUM (first pass): method="post"', () => {
    renderWithClient(<ProfileFormWithHooks profile={baseProfile} />);
    expect(document.querySelector('form')).toHaveAttribute('method', 'post');
  });
});
