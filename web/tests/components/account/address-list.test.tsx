import { describe, expect, it } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { AddressList } from '@/components/account/AddressList';
import { useDeliveryAddresses } from '@/lib/hooks/useAccount';
import {
  useAddDeliveryAddress,
  useUpdateDeliveryAddress,
  useRemoveDeliveryAddress,
  useSetDefaultDeliveryAddress,
} from '@/lib/hooks/useAccountMutations';
import type { DeliveryAddressBook } from '@/lib/api/account';

/** ARCH gate HIGH 3 (first pass) — stand-in for app/(portal)/portal/account/addresses/page.tsx;
 *  a full query+mutations wrapper (not just mutations) since U-E3 depends on the list re-fetching
 *  after a mutation invalidates it, the same as the real page. */
function AddressListWithHooks() {
  const { data: book } = useDeliveryAddresses();
  const addMutation = useAddDeliveryAddress();
  const updateMutation = useUpdateDeliveryAddress();
  const removeMutation = useRemoveDeliveryAddress();
  const setDefaultMutation = useSetDefaultDeliveryAddress();

  if (!book) return null;
  return (
    <AddressList
      book={book}
      onAdd={(values) => addMutation.mutateAsync(values)}
      onUpdate={(addressId, values) => updateMutation.mutateAsync({ addressId, request: values })}
      onRemove={(addressId) => removeMutation.mutateAsync(addressId)}
      onSetDefault={(addressId) => setDefaultMutation.mutate(addressId)}
      settingDefaultAddressId={
        setDefaultMutation.isPending ? (setDefaultMutation.variables ?? null) : null
      }
    />
  );
}

function renderWithClient(children: React.ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}>{children}</QueryClientProvider>);
}

const book: DeliveryAddressBook = {
  maxAddresses: 10,
  addresses: [
    {
      id: 'addr-1',
      label: 'Home',
      line1: '1 Rose Road',
      line2: null,
      city: 'Dhaka',
      district: 'Dhaka',
      postalCode: null,
      deliveryInstructions: null,
      isDefault: true,
    },
    {
      id: 'addr-2',
      label: 'Office',
      line1: '2 Palm Street',
      line2: null,
      city: 'Dhaka',
      district: 'Dhaka',
      postalCode: null,
      deliveryInstructions: null,
      isDefault: false,
    },
  ],
};

describe('AddressList/AddressCard — US-ACC-06', () => {
  it('U-E3: exactly one "Default" badge is shown, and setting another as default moves it', async () => {
    server.use(
      http.put(
        '*/api/customers/me/addresses/:addressId/default',
        () => new HttpResponse(null, { status: 204 }),
      ),
      http.get('*/api/customers/me/addresses', () =>
        HttpResponse.json({
          maxAddresses: 10,
          addresses: [
            { ...book.addresses[0]!, isDefault: false },
            { ...book.addresses[1]!, isDefault: true },
          ],
        }),
      ),
    );
    const user = userEvent.setup();
    renderWithClient(<AddressListWithHooks />);
    expect(await screen.findAllByText('Default')).toHaveLength(1);

    await user.click(screen.getByRole('button', { name: /set as default/i }));
    await waitFor(() => expect(screen.getAllByText('Default')).toHaveLength(1));
  });

  it('U-E4: removing confirms, naming the label, and DefaultAddressCannotBeRemoved shows the server message', async () => {
    server.use(
      http.get('*/api/customers/me/addresses', () => HttpResponse.json(book)),
      http.delete('*/api/customers/me/addresses/:addressId', () =>
        HttpResponse.json(
          {
            isValid: false,
            errors: [
              {
                propertyName: 'AddressId',
                errorCode: 'DefaultAddressCannotBeRemoved',
                errorMessage: 'Set another address as default before removing this one.',
                resourceName: '',
              },
            ],
          },
          { status: 400 },
        ),
      ),
    );
    const user = userEvent.setup();
    renderWithClient(<AddressListWithHooks />);

    const homeCard = (await screen.findByText('Home')).closest('div')!.parentElement!;
    await user.click(within(homeCard as HTMLElement).getByRole('button', { name: /remove/i }));

    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByRole('heading', { name: /remove "home"\?/i })).toBeInTheDocument();
    await userEvent.click(within(dialog).getByRole('button', { name: /^remove$/i }));

    expect(
      await screen.findByText(/set another address as default before removing this one/i),
    ).toBeInTheDocument();
  });

  it('U-E5: a 409 on saving an edit tells the user to reload, without discarding what they entered', async () => {
    server.use(
      http.get('*/api/customers/me/addresses', () => HttpResponse.json(book)),
      http.put(
        '*/api/customers/me/addresses/:addressId',
        () => new HttpResponse(null, { status: 409 }),
      ),
    );
    const user = userEvent.setup();
    renderWithClient(<AddressListWithHooks />);

    const homeCard = (await screen.findByText('Home')).closest('div')!.parentElement!;
    await user.click(within(homeCard as HTMLElement).getByRole('button', { name: 'Edit' }));

    const dialog = await screen.findByRole('dialog');
    const labelField = within(dialog).getByLabelText(/^label/i);
    await user.clear(labelField);
    await user.type(labelField, 'Home (updated)');
    await user.click(within(dialog).getByRole('button', { name: /save address/i }));

    expect(
      await screen.findByText(/someone else changed this record while you were editing it/i),
    ).toBeInTheDocument();
    // NFR-USA-5 — a conflict never resets the form; the entered change is still there.
    expect(within(dialog).getByLabelText(/^label/i)).toHaveValue('Home (updated)');
  });

  it('SEC gate MEDIUM (first pass): AddressForm uses method="post"', async () => {
    server.use(http.get('*/api/customers/me/addresses', () => HttpResponse.json(book)));
    const user = userEvent.setup();
    renderWithClient(<AddressListWithHooks />);
    await user.click(await screen.findByRole('button', { name: 'Add address' }));
    const dialog = await screen.findByRole('dialog');
    expect(dialog.querySelector('form')).toHaveAttribute('method', 'post');
  });
});
