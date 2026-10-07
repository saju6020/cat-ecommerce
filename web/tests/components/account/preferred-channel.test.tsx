import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { PreferredChannelField } from '@/components/account/PreferredChannelField';
import NotificationsPage from '@/app/(portal)/portal/account/notifications/page';
import type { NotificationPreferences } from '@/lib/api/account';

// UnsavedChangesGuard (rendered by the matrix whenever the form is dirty) calls useRouter().
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

const cells: NotificationPreferences['cells'] = [
  {
    channel: 'SMS',
    category: 'Appointments',
    isGranted: true,
    isEditable: false,
    isAlwaysOn: true,
  },
  { channel: 'SMS', category: 'Marketing', isGranted: false, isEditable: true, isAlwaysOn: false },
  {
    channel: 'Email',
    category: 'Marketing',
    isGranted: false,
    isEditable: true,
    isAlwaysOn: false,
  },
];

function servePreferences(over: Partial<NotificationPreferences> = {}) {
  const puts: unknown[] = [];
  let gets = 0;
  server.use(
    http.get('*/api/customers/me/notification-preferences', () => {
      gets += 1;
      return HttpResponse.json({
        cells,
        preferredChannel: null,
        effectivePreferredChannel: 'Sms',
        availablePreferredChannels: ['Sms', 'Email'],
        ...over,
      });
    }),
    http.put('*/api/customers/me/notification-preferences', async ({ request }) => {
      puts.push(await request.json());
      return new HttpResponse(null, { status: 204 });
    }),
  );
  return { puts, gets: () => gets };
}

function renderPage() {
  return render(
    <QueryClientProvider
      client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}
    >
      <NotificationsPage />
    </QueryClientProvider>,
  );
}

describe('PreferredChannelField — HO-NTF-002 §11.2', () => {
  it('U-B1: both identifiers -> both enabled; mobile only -> email disabled with the hint and a profile link', () => {
    const { rerender } = render(
      <PreferredChannelField value="Sms" available={['Sms', 'Email']} onChange={() => {}} />,
    );
    expect(screen.getByRole('radio', { name: 'Text message (SMS)' })).toBeEnabled();
    expect(screen.getByRole('radio', { name: 'Email' })).toBeEnabled();
    expect(screen.getByRole('radio', { name: 'Text message (SMS)' })).toBeChecked();
    expect(
      screen.getByText(
        'Password resets, reminders and updates are sent this way, and a copy always appears in your inbox.',
      ),
    ).toBeInTheDocument();

    rerender(<PreferredChannelField value="Sms" available={['Sms']} onChange={() => {}} />);
    expect(screen.getByRole('radio', { name: 'Email' })).toBeDisabled();
    expect(
      screen.getByText(/Add an email address to your profile to choose email/),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Go to your profile' })).toHaveAttribute(
      'href',
      '/portal/account/profile',
    );
    expect(screen.getByRole('radio', { name: 'Text message (SMS)' })).toBeEnabled();
  });

  it('selects the effective choice even when preferredChannel is null', async () => {
    servePreferences({ preferredChannel: null, effectivePreferredChannel: 'Email' });
    renderPage();
    expect(await screen.findByRole('radio', { name: 'Email' })).toBeChecked();
  });

  it('U-B2: a channel change and a matrix cell are saved in ONE PUT, and nothing is sent before Save', async () => {
    const { puts } = servePreferences();
    const user = userEvent.setup();
    renderPage();

    await user.click(await screen.findByRole('radio', { name: 'Email' }));
    await user.click(screen.getByRole('checkbox', { name: /Marketing by Email/ }));
    expect(puts).toHaveLength(0);

    await user.click(screen.getByRole('button', { name: 'Save changes' }));
    await waitFor(() => expect(puts).toHaveLength(1));
    expect(puts[0]).toEqual({
      changes: [{ channel: 'Email', category: 'Marketing', isGranted: true }],
      preferredChannel: 'Email',
    });
    expect(await screen.findByText('Your notification preferences have been saved.')).toBeVisible();
  });

  it('saves a channel change on its own (no cell changed)', async () => {
    const { puts } = servePreferences();
    const user = userEvent.setup();
    renderPage();
    await user.click(await screen.findByRole('radio', { name: 'Email' }));
    await user.click(screen.getByRole('button', { name: 'Save changes' }));
    await waitFor(() => expect(puts).toHaveLength(1));
    expect(puts[0]).toEqual({ changes: [], preferredChannel: 'Email' });
  });

  it('U-B3 / U-X1: PreferredChannelHasNoDestination is shown on the field, with no error code in the DOM', async () => {
    servePreferences();
    server.use(
      http.put('*/api/customers/me/notification-preferences', () =>
        HttpResponse.json(
          {
            errors: [
              {
                propertyName: 'PreferredChannel',
                errorCode: 'PreferredChannelHasNoDestination',
                errorMessage: 'Add an email address to your profile to choose email.',
                resourceName: 'Account',
              },
            ],
            isValid: false,
          },
          { status: 400 },
        ),
      ),
    );
    const user = userEvent.setup();
    const { container } = renderPage();
    await user.click(await screen.findByRole('radio', { name: 'Email' }));
    await user.click(screen.getByRole('button', { name: 'Save changes' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Add an email address to your profile to choose email.');
    expect(screen.getByRole('group', { name: 'Send important messages by' })).toContainElement(
      alert,
    );
    expect(container.textContent).not.toContain('PreferredChannelHasNoDestination');
  });

  it('U-X2: a dirty preferences page shows the unsaved-changes guard on in-app navigation', async () => {
    servePreferences();
    const user = userEvent.setup();
    render(
      <QueryClientProvider
        client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}
      >
        <a href="/somewhere-else">Elsewhere</a>
        <NotificationsPage />
      </QueryClientProvider>,
    );
    await user.click(await screen.findByRole('radio', { name: 'Email' }));
    await user.click(screen.getByRole('link', { name: 'Elsewhere' }));
    expect(
      await screen.findByText('You have unsaved changes. Leave this page without saving?'),
    ).toBeVisible();
  });
});
