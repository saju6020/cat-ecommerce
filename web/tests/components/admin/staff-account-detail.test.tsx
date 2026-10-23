import { describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { StaffAccountDetail } from '@/components/admin/StaffAccountDetail';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { useStaffRoleCatalogue } from '@/lib/hooks/useStaffAdmin';
import {
  useChangeStaffRoles,
  useUpdateStaffAccountDetails,
} from '@/lib/hooks/useStaffAdminMutations';
import type { StaffAccount } from '@/lib/api/staff-admin';

/** ARCH gate HIGH 3 (first pass) — stand-in for
 *  app/(staff)/staff/admin/staff-accounts/[staffAccountId]/page.tsx; see register-form.test.tsx's
 *  own identical-purpose wrapper comment. */
function StaffAccountDetailWithHooks({ account }: { account: StaffAccount }) {
  const catalogue = useStaffRoleCatalogue();
  const mutation = useChangeStaffRoles(account.id);
  return (
    <StaffAccountDetail
      account={account}
      catalogue={catalogue}
      onChangeRoles={(roleIds) => mutation.mutateAsync(roleIds)}
      isPending={mutation.isPending}
    />
  );
}

function renderWithClient(children: React.ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}>{children}</QueryClientProvider>);
}

const account: StaffAccount = {
  id: 'staff-1',
  fullName: 'Nadia Nurse',
  mobileNumber: null,
  emailAddress: 'nadia@example.com',
  roleIds: ['Nurse', 'Reception'],
  hasPassword: true,
};

describe('StaffAccountDetail — US-ACC-07, UI-3', () => {
  it('U-F3: removing a role confirms, naming it and the sign-out consequence, and PUT sends the whole set', async () => {
    server.use(
      http.get('*/api/staff-roles', () =>
        HttpResponse.json({
          roleIds: ['Administrator', 'Reception', 'Nurse', 'Vet'],
          capabilities: [],
          cells: [],
        }),
      ),
    );
    let capturedBody: unknown;
    server.use(
      http.put('*/api/staff-accounts/:id/roles', async ({ request }) => {
        capturedBody = await request.json();
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const user = userEvent.setup();
    renderWithClient(<StaffAccountDetailWithHooks account={account} />);

    await user.click(await screen.findByRole('checkbox', { name: 'Reception' })); // unchecking it
    await user.click(screen.getByRole('button', { name: /save roles/i }));

    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/reception/i)).toBeInTheDocument();
    expect(within(dialog).getByText(/signed out/i)).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: /^remove$/i }));

    await screen.findByRole('checkbox', { name: 'Nurse' });
    expect(capturedBody).toEqual({ roleIds: ['Nurse'] });
  });

  it('LastAdministratorCannotBeRemoved is shown as the server’s own message', async () => {
    server.use(
      http.get('*/api/staff-roles', () =>
        HttpResponse.json({ roleIds: ['Administrator', 'Reception'], capabilities: [], cells: [] }),
      ),
      http.put('*/api/staff-accounts/:id/roles', () =>
        HttpResponse.json(
          {
            isValid: false,
            errors: [
              {
                propertyName: 'RoleIds',
                errorCode: 'LastAdministratorCannotBeRemoved',
                errorMessage: 'At least one administrator must remain.',
                resourceName: '',
              },
            ],
          },
          { status: 400 },
        ),
      ),
    );
    const user = userEvent.setup();
    const adminAccount: StaffAccount = { ...account, roleIds: ['Administrator'] };
    renderWithClient(<StaffAccountDetailWithHooks account={adminAccount} />);

    await user.click(await screen.findByRole('checkbox', { name: 'Reception' })); // adding a role, not removing — save directly
    await user.click(screen.getByRole('button', { name: /save roles/i }));

    expect(await screen.findByText(/at least one administrator must remain/i)).toBeInTheDocument();
  });
});

describe('PermissionDeniedState — U-F4, ADR-FE-009', () => {
  it('names no role and no action', () => {
    renderWithClient(<PermissionDeniedState />);
    expect(screen.getByText(/you don.t have permission to do this/i)).toBeInTheDocument();
    expect(screen.getByText(/ask an administrator/i)).toBeInTheDocument();
  });
});

/** Owner, 2026-10-20: both sign-in identifiers are shown, and the administrator edits them. */
function StaffAccountDetailWithEdit({ account }: { account: StaffAccount }) {
  const catalogue = useStaffRoleCatalogue();
  const roles = useChangeStaffRoles(account.id);
  const details = useUpdateStaffAccountDetails(account.id);
  return (
    <StaffAccountDetail
      account={account}
      catalogue={catalogue}
      onChangeRoles={(roleIds) => roles.mutateAsync(roleIds)}
      isPending={roles.isPending}
      onUpdateDetails={(request) => details.mutateAsync(request)}
      isUpdatingDetails={details.isPending}
    />
  );
}

describe('StaffAccountDetail — sign-in details (owner, 2026-10-20)', () => {
  it('shows the mobile number and the email, labelled, with "Not set" for an empty one', () => {
    renderWithClient(<StaffAccountDetailWithHooks account={account} />);
    const list = screen.getByText('Signs in with').nextElementSibling as HTMLElement;
    expect(list.tagName).toBe('DL');
    expect(within(list).getByText('Mobile number')).toBeInTheDocument();
    expect(within(list).getByText('nadia@example.com')).toBeInTheDocument();
    expect(within(list).getByText('Not set')).toBeInTheDocument();
  });

  it('edits name, mobile and email and sends them with PUT', async () => {
    let capturedBody: unknown;
    server.use(
      http.put('*/api/staff-accounts/:id', async ({ request }) => {
        capturedBody = await request.json();
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const user = userEvent.setup();
    renderWithClient(<StaffAccountDetailWithEdit account={account} />);

    await user.click(screen.getByRole('button', { name: 'Edit details' }));
    const form = screen.getByRole('form', { name: 'Edit details' });
    const name = within(form).getByLabelText(/full name/i);
    await user.clear(name);
    await user.type(name, 'Nadia Rahman');
    await user.type(within(form).getByLabelText(/mobile number/i), '01712345678');
    await user.click(within(form).getByRole('button', { name: 'Save details' }));

    expect(await screen.findByText('Details saved.')).toBeInTheDocument();
    expect(capturedBody).toEqual({
      fullName: 'Nadia Rahman',
      mobileNumber: '01712345678',
      emailAddress: 'nadia@example.com',
    });
  });

  it('refuses to clear both identifiers before sending', async () => {
    let called = false;
    server.use(
      http.put('*/api/staff-accounts/:id', () => {
        called = true;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const user = userEvent.setup();
    renderWithClient(<StaffAccountDetailWithEdit account={account} />);

    await user.click(screen.getByRole('button', { name: 'Edit details' }));
    const form = screen.getByRole('form', { name: 'Edit details' });
    await user.clear(within(form).getByLabelText(/email/i));
    await user.click(within(form).getByRole('button', { name: 'Save details' }));

    expect(
      await within(form).findByText(/give a mobile number, an email, or both/i),
    ).toBeInTheDocument();
    expect(called).toBe(false);
  });

  it("shows the server's duplicate message on the email field", async () => {
    server.use(
      http.put('*/api/staff-accounts/:id', () =>
        HttpResponse.json(
          {
            isValid: false,
            errors: [
              {
                propertyName: 'EmailAddress',
                errorCode: 'EmailAddressAlreadyRegistered',
                errorMessage: 'This email address is already registered to a staff account.',
                resourceName: '',
              },
            ],
          },
          { status: 400 },
        ),
      ),
    );
    const user = userEvent.setup();
    renderWithClient(<StaffAccountDetailWithEdit account={account} />);

    await user.click(screen.getByRole('button', { name: 'Edit details' }));
    await user.click(screen.getByRole('button', { name: 'Save details' }));

    expect(await screen.findByText(/already registered to a staff account/i)).toBeInTheDocument();
    expect(screen.getByRole('form', { name: 'Edit details' })).toBeInTheDocument();
  });
});
