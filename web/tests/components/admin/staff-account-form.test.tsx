import { describe, expect, it } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { StaffAccountForm } from '@/components/admin/StaffAccountForm';
import { useStaffRoleCatalogue } from '@/lib/hooks/useStaffAdmin';
import { useCreateStaffAccount } from '@/lib/hooks/useStaffAdminMutations';

/** ARCH gate HIGH 3 (first pass) — stand-in for
 *  app/(staff)/staff/admin/staff-accounts/new/page.tsx; see register-form.test.tsx's own
 *  identical-purpose wrapper comment. */
function StaffAccountFormWithHooks() {
  const catalogue = useStaffRoleCatalogue();
  const mutation = useCreateStaffAccount();
  return (
    <StaffAccountForm
      catalogue={catalogue}
      onCreate={(request) => mutation.mutateAsync(request)}
      isPending={mutation.isPending}
    />
  );
}

function renderWithClient(children: React.ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}>{children}</QueryClientProvider>);
}

const CATALOGUE = {
  roleIds: ['Administrator', 'Reception', 'Nurse', 'Vet'],
  capabilities: ['Register customers'],
  cells: [
    { capability: 'Register customers', roleId: 'Administrator', access: 'Full', qualifier: null },
    { capability: 'Register customers', roleId: 'Reception', access: 'Full', qualifier: null },
    { capability: 'Register customers', roleId: 'Nurse', access: 'None', qualifier: null },
    { capability: 'Register customers', roleId: 'Vet', access: 'None', qualifier: null },
  ],
};

function stubCatalogue() {
  server.use(http.get('*/api/staff-roles', () => HttpResponse.json(CATALOGUE)));
}

describe('StaffAccountForm — US-ACC-07', () => {
  it('U-F1: role options come from the catalogue and at least one is required', async () => {
    stubCatalogue();
    const user = userEvent.setup();
    renderWithClient(<StaffAccountFormWithHooks />);

    for (const role of CATALOGUE.roleIds) {
      expect(await screen.findByRole('checkbox', { name: role })).toBeInTheDocument();
    }

    await user.type(screen.getByLabelText(/full name/i), 'New Person');
    await user.type(screen.getByLabelText(/mobile number/i), '01712345678');
    await user.click(screen.getByRole('button', { name: /create account/i }));

    expect(await screen.findByText(/select at least one role/i)).toBeInTheDocument();
  });

  it('U-F2: the capability preview shows the union of selected roles, captioned "Indicative"', async () => {
    stubCatalogue();
    const user = userEvent.setup();
    renderWithClient(<StaffAccountFormWithHooks />);

    await user.click(await screen.findByRole('checkbox', { name: 'Nurse' }));
    expect(screen.getByText(/indicative/i)).toBeInTheDocument();
    // Nurse alone has no access to "Register customers".
    expect(screen.getAllByText('None').length).toBeGreaterThan(0);

    await user.click(screen.getByRole('checkbox', { name: 'Reception' }));
    await waitFor(() => expect(screen.getByText('Full')).toBeInTheDocument());
  });

  // Owner, 2026-10-19: the administrator sets the starting password and shares it with the new member.
  it('sends the password the administrator typed, and success says to share it', async () => {
    stubCatalogue();
    const posted: unknown[] = [];
    server.use(
      http.post('*/api/staff-accounts', async ({ request }) => {
        posted.push(await request.json());
        return HttpResponse.json({ staffAccountId: 'new-id' }, { status: 201 });
      }),
    );
    const user = userEvent.setup();
    renderWithClient(<StaffAccountFormWithHooks />);

    await user.type(screen.getByLabelText(/full name/i), 'New Person');
    await user.type(screen.getByLabelText(/mobile number/i), '01712345678');
    await user.type(screen.getByLabelText(/^password/i), 'a-long-shared-password');
    await user.click(await screen.findByRole('checkbox', { name: 'Reception' }));
    await user.click(screen.getByRole('button', { name: /create account/i }));

    const status = await screen.findByRole('status');
    expect(status).toHaveTextContent(/share the password with new person/i);
    // Owner, 2026-10-20: the message names the sign-in ID to share.
    expect(status).toHaveTextContent('01712345678');
    expect(posted).toEqual([
      expect.objectContaining({
        fullName: 'New Person',
        password: 'a-long-shared-password',
        roleIds: ['Reception'],
      }),
    ]);
  });

  it('a password shorter than 10 characters is refused before sending', async () => {
    stubCatalogue();
    const posted: unknown[] = [];
    server.use(
      http.post('*/api/staff-accounts', async ({ request }) => {
        posted.push(await request.json());
        return HttpResponse.json({ staffAccountId: 'new-id' }, { status: 201 });
      }),
    );
    const user = userEvent.setup();
    renderWithClient(<StaffAccountFormWithHooks />);

    await user.type(screen.getByLabelText(/full name/i), 'New Person');
    await user.type(screen.getByLabelText(/mobile number/i), '01712345678');
    await user.type(screen.getByLabelText(/^password/i), 'short');
    await user.click(await screen.findByRole('checkbox', { name: 'Reception' }));
    await user.click(screen.getByRole('button', { name: /create account/i }));

    expect(await screen.findByText(/use at least 10 characters/i)).toBeInTheDocument();
    expect(posted).toEqual([]);
  });

  it('SEC gate MEDIUM (first pass): method="post"', () => {
    stubCatalogue();
    renderWithClient(<StaffAccountFormWithHooks />);
    expect(document.querySelector('form')).toHaveAttribute('method', 'post');
  });
});
