import { describe, expect, it, vi } from 'vitest';
import { useState } from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { WalkInCustomerForm } from '@/components/customer/WalkInCustomerForm';
import { CustomerStatusBadge } from '@/components/customer/CustomerStatusBadge';
import { useRegisterWalkInCustomer } from '@/lib/hooks/useStaffCustomerMutations';
import { useFindCustomers } from '@/lib/hooks/useStaffCustomers';

/** ARCH gate HIGH 3 (first pass) — stand-in for app/(staff)/staff/customers/new/page.tsx; see
 *  register-form.test.tsx's own identical-purpose wrapper comment. */
function WalkInCustomerFormWithHooks({ onCreated }: { onCreated: (customerId: string) => void }) {
  const [duplicateMobile, setDuplicateMobile] = useState<string | null>(null);
  const registerMutation = useRegisterWalkInCustomer();
  const lookup = useFindCustomers({ mobile: duplicateMobile ?? undefined }, !!duplicateMobile);
  return (
    <WalkInCustomerForm
      onRegister={(request) => registerMutation.mutateAsync(request)}
      isPending={registerMutation.isPending}
      onCreated={onCreated}
      duplicateMobile={duplicateMobile}
      onDuplicateFound={setDuplicateMobile}
      existingCustomers={lookup.data}
    />
  );
}

function renderWithClient(children: React.ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}>{children}</QueryClientProvider>);
}

describe('WalkInCustomerForm — US-ACC-10', () => {
  it('U-H1: on a duplicate mobile, looks up and links the existing record; the form keeps its values', async () => {
    server.use(
      http.post('*/api/staff/customers', () =>
        HttpResponse.json(
          {
            isValid: false,
            errors: [
              {
                propertyName: 'MobileNumber',
                errorCode: 'MobileNumberAlreadyRegistered',
                errorMessage: 'This mobile number is already registered.',
                resourceName: '',
              },
            ],
          },
          { status: 400 },
        ),
      ),
      http.get('*/api/staff/customers', () =>
        HttpResponse.json([
          {
            id: 'existing-1',
            fullName: 'Existing Person',
            mobileNumber: '01712345678',
            emailAddress: null,
            recordStatus: 'Complete',
            status: 'Active',
            mobileMatchedOn: 'Primary',
          },
        ]),
      ),
    );
    const user = userEvent.setup();
    renderWithClient(<WalkInCustomerFormWithHooks onCreated={vi.fn()} />);
    await user.type(screen.getByLabelText(/full name/i), 'New Walk-in');
    await user.type(screen.getByLabelText(/mobile number/i), '01712345678');
    await user.click(screen.getByRole('button', { name: /register customer/i }));

    expect(await screen.findByRole('link', { name: 'Existing Person' })).toHaveAttribute(
      'href',
      '/staff/customers/existing-1',
    );
    expect((screen.getByLabelText(/full name/i) as HTMLInputElement).value).toBe('New Walk-in');
    expect((screen.getByLabelText(/mobile number/i) as HTMLInputElement).value).toBe('01712345678');
  });

  it('creates and hands off the new customer id on success', async () => {
    server.use(
      http.post('*/api/staff/customers', () =>
        HttpResponse.json({ customerId: 'new-1' }, { status: 201 }),
      ),
    );
    const onCreated = vi.fn();
    const user = userEvent.setup();
    renderWithClient(<WalkInCustomerFormWithHooks onCreated={onCreated} />);
    await user.type(screen.getByLabelText(/full name/i), 'New Walk-in');
    await user.type(screen.getByLabelText(/mobile number/i), '01799999998');
    await user.click(screen.getByRole('button', { name: /register customer/i }));

    await waitFor(() => expect(onCreated).toHaveBeenCalledWith('new-1'));
  });

  it('SEC gate MEDIUM (first pass): method="post"', () => {
    renderWithClient(<WalkInCustomerFormWithHooks onCreated={vi.fn()} />);
    expect(document.querySelector('form')).toHaveAttribute('method', 'post');
  });
});

describe('CustomerStatusBadge — U-H2, UI-6', () => {
  it('Incomplete carries text and an icon, not colour alone', () => {
    render(<CustomerStatusBadge recordStatus="Incomplete" />);
    const badge = screen.getByText('Incomplete');
    expect(badge.querySelector('svg')).toBeInTheDocument();
  });
});
