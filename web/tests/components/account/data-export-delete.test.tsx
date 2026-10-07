import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { PersonalDataExportCard } from '@/components/account/PersonalDataExportCard';
import { DeleteAccountForm } from '@/components/account/DeleteAccountForm';
import { useDeleteAccount, useExportPersonalData } from '@/lib/hooks/useAccountMutations';
import { resetAccountsMockData } from '@/mocks/handlers/areas/accounts';

const push = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push, replace: vi.fn() }),
}));

/** ARCH gate HIGH 3 (first pass) — stand-in for app/(portal)/portal/account/data/page.tsx; see
 *  register-form.test.tsx's own identical-purpose wrapper comment. */
function PersonalDataExportCardWithHooks() {
  const mutation = useExportPersonalData();
  return (
    <PersonalDataExportCard
      onExport={() => mutation.mutateAsync()}
      isPending={mutation.isPending}
    />
  );
}

/** ARCH gate HIGH 3 (first pass) — stand-in for app/(portal)/portal/account/data/delete/page.tsx;
 *  see register-form.test.tsx's own identical-purpose wrapper comment. */
function DeleteAccountFormWithHooks() {
  const mutation = useDeleteAccount();
  const queryClient = useQueryClient();
  const router = useRouter();
  return (
    <DeleteAccountForm
      onDelete={async (currentPassword) => {
        await mutation.mutateAsync({ currentPassword });
        queryClient.clear();
        router.push('/account-deleted');
      }}
      isPending={mutation.isPending}
      error={mutation.error}
    />
  );
}

function renderWithClient(children: React.ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}>{children}</QueryClientProvider>);
}

beforeEach(() => {
  resetAccountsMockData();
  push.mockClear();
});

describe('PersonalDataExportCard — U-J1', () => {
  const createObjectURL = vi.fn(() => 'blob:mock-url');
  const revokeObjectURL = vi.fn();

  beforeEach(() => {
    // jsdom has no real Blob URL implementation.
    URL.createObjectURL = createObjectURL;
    URL.revokeObjectURL = revokeObjectURL;
    createObjectURL.mockClear();
    revokeObjectURL.mockClear();
  });

  it('downloads a Blob named purrcare-my-data.json and revokes the URL', async () => {
    // The real handler requires a session cookie, which jsdom's fetch cannot carry across
    // requests (see mocks/handlers/areas/accounts.ts's top-of-file doc comment) — overridden
    // here so this test exercises the download mechanism itself, not session plumbing that a
    // real browser (Playwright) already covers end-to-end.
    server.use(
      http.post('*/api/customers/me/personal-data-export', () =>
        HttpResponse.json({
          schemaVersion: 1,
          exportedOn: '2024-01-01T00:00:00Z',
          profile: {
            fullName: 'Sam Rahman',
            mobileNumber: '01799999999',
            emailAddress: 'sam@example.com',
            hasMobileNumber: true,
            hasEmailAddress: true,
            canEditEmail: true,
            additionalPhoneNumbers: [],
            registeredOn: '2024-01-01T00:00:00Z',
          },
          addresses: [],
          notificationPreferences: [],
          cats: [],
        }),
      ),
    );
    const user = userEvent.setup();
    renderWithClient(<PersonalDataExportCardWithHooks />);
    await user.click(screen.getByRole('button', { name: /download my data/i }));

    await waitFor(() => expect(createObjectURL).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(revokeObjectURL).toHaveBeenCalledWith('blob:mock-url'));
  });
});

describe('DeleteAccountForm — U-J2/J3/J4', () => {
  afterEach(() => {
    server.resetHandlers();
  });

  it('U-J2: lists every consequence and requires the password and the checkbox', async () => {
    const user = userEvent.setup();
    renderWithClient(<DeleteAccountFormWithHooks />);
    expect(
      screen.getByText(/remove your personal details, contact details and addresses/i),
    ).toBeInTheDocument();
    expect(screen.getByText(/sign you out/i)).toBeInTheDocument();
    expect(screen.getAllByText(/this cannot be undone/i).length).toBeGreaterThanOrEqual(1);

    await user.click(screen.getByRole('button', { name: /delete my account/i }));
    expect(await screen.findByText(/enter your current password/i)).toBeInTheDocument();
    expect(
      await screen.findByText(/confirm you understand this cannot be undone/i),
    ).toBeInTheDocument();
  });

  it('U-J3: ReauthenticationFailed shows a field error and clears the password', async () => {
    server.use(
      http.post('*/api/customers/me/deletion', () =>
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
    renderWithClient(<DeleteAccountFormWithHooks />);
    await user.type(screen.getByLabelText(/current password/i), 'wrong-password');
    await user.click(screen.getByLabelText(/understand this cannot be undone/i));
    await user.click(screen.getByRole('button', { name: /delete my account/i }));

    expect(await screen.findByText(/that current password is incorrect/i)).toBeInTheDocument();
    expect((screen.getByLabelText(/current password/i) as HTMLInputElement).value).toBe('');
  });

  it('U-J4: success clears the cache and routes to /account-deleted', async () => {
    server.use(
      http.post('*/api/customers/me/deletion', () => new HttpResponse(null, { status: 204 })),
    );
    const user = userEvent.setup();
    renderWithClient(<DeleteAccountFormWithHooks />);
    await user.type(screen.getByLabelText(/current password/i), 'Password123!');
    await user.click(screen.getByLabelText(/understand this cannot be undone/i));
    await user.click(screen.getByRole('button', { name: /delete my account/i }));

    await waitFor(() => expect(push).toHaveBeenCalledWith('/account-deleted'));
  });

  it('SEC gate MEDIUM (first pass): method="post"', () => {
    renderWithClient(<DeleteAccountFormWithHooks />);
    expect(document.querySelector('form')).toHaveAttribute('method', 'post');
  });
});
