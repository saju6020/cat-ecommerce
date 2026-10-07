import { beforeEach, describe, expect, it } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import {
  notificationFixtureIds as ids,
  resetNotificationsMockData,
} from '@/mocks/handlers/notifications';
import { NotificationDetail } from '@/components/notifications/NotificationDetail';
import { useNotificationDetail, useResendNotification } from '@/lib/hooks/useNotificationLog';

/** Stand-in for app/(staff)/staff/notifications/[notificationId]/page.tsx — real hooks over MSW. */
function Harness({ id }: { id: string }) {
  const { data } = useNotificationDetail(id);
  const resend = useResendNotification(id);
  if (!data) return <p>Loading</p>;
  return (
    <NotificationDetail
      detail={data}
      timeZoneId="Asia/Dhaka"
      onResend={(channel) => resend.mutateAsync(channel)}
    />
  );
}

function renderDetail(id: string) {
  return render(
    <QueryClientProvider
      client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}
    >
      <Harness id={id} />
    </QueryClientProvider>,
  );
}

beforeEach(() => resetNotificationsMockData());

describe('NotificationDetail — HO-NTF-002 §11.5', () => {
  it('shows the full destination, recipient link, template, every attempt and the chain', async () => {
    renderDetail(ids.failedSmsId);
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Vaccination due' }),
    ).toBeInTheDocument();
    expect(screen.getByText('+8801799999999')).toBeInTheDocument(); // full, not masked
    expect(screen.getByRole('link', { name: 'Sam Rahman' })).toHaveAttribute(
      'href',
      `/staff/customers/${ids.customerId}`,
    );
    expect(screen.getByText('care.vaccination-due')).toBeInTheDocument();

    const attempts = screen.getByRole('table', { name: 'Delivery attempts' });
    expect(within(attempts).getAllByRole('row')).toHaveLength(1 + 3); // header + 3 attempts
    expect(within(attempts).getAllByText('SMS is not connected to a provider.')).toHaveLength(3);
    expect(screen.getByText('Delivery attempts (3)')).toBeInTheDocument();

    // The fallback chain links to the email that was sent instead.
    expect(screen.getByText('Fallbacks sent')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'View message' })).toHaveAttribute(
      'href',
      `/staff/notifications/${ids.fallbackEmailId}`,
    );
  });

  it('shows a resend chain in both directions', async () => {
    const { unmount } = renderDetail(ids.resendCopyId);
    expect(await screen.findByText('Resend of')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'View message' })).toHaveAttribute(
      'href',
      `/staff/notifications/${ids.resendOriginalId}`,
    );
    unmount();
    renderDetail(ids.resendOriginalId);
    expect(await screen.findByText('Resent as')).toBeInTheDocument();
  });

  it('U-E4: a redacted reset message shows the removal notice and the resend refusal sentence', async () => {
    renderDetail(ids.redactedResetId);
    expect(
      await screen.findByText('Content removed — it contained a password-reset link'),
    ).toBeInTheDocument();
    expect(screen.queryByTestId('notification-body')).not.toBeInTheDocument();
    expect(screen.queryByText('[redacted]')).not.toBeInTheDocument();
    expect(screen.getByTestId('cannot-resend')).toHaveTextContent(
      "This message contained a password-reset link and can't be resent. Ask the customer to request a new one.",
    );
    expect(screen.queryByRole('button', { name: 'Resend' })).not.toBeInTheDocument();
  });

  it('U-E5: markup characters in subject and body, and a <script> in a provider response, are plain text', async () => {
    const { container } = renderDetail(ids.markupId);
    const body = await screen.findByTestId('notification-body');
    expect(body.textContent).toContain(
      'Hello <i>there</i>, **not bold** [not a link](https://evil.example)',
    );
    expect(body.textContent).toContain('{{notAPlaceholder}}');
    expect(screen.getByText('<b>Subject</b> & "quotes"')).toBeInTheDocument();
    expect(
      within(screen.getByRole('table', { name: 'Delivery attempts' })).getByText(
        '550 <script>alert(1)</script> mailbox unavailable',
      ),
    ).toBeInTheDocument();
    expect(container.querySelector('script, i, b, strong, a[href*="evil"]')).toBeNull();
  });
});

describe('ResendPanel — HO-NTF-002 §11.5, US-NTF-06 AC2', () => {
  it('U-E6: the confirm names channel and destination; a 201 held for quiet hours shows the time and a link', async () => {
    const user = userEvent.setup();
    renderDetail(ids.heldId); // a deworming SMS: the mock holds its resend for quiet hours
    await user.click(await screen.findByRole('button', { name: 'Resend' }));

    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent(
      'Send this message again by text message (SMS) to +8801799999999?',
    );
    await user.click(within(dialog).getByRole('button', { name: 'Resend' }));

    const status = await screen.findByRole('status');
    expect(status).toHaveTextContent('Held for quiet hours until 08:00');
    expect(within(status).getByRole('link', { name: 'View the new notification' })).toHaveAttribute(
      'href',
      expect.stringMatching(/^\/staff\/notifications\/[0-9a-f-]{36}$/),
    );
  });

  it('resending by another channel names whose contact detail will be used; a 201 queued says Queued', async () => {
    const user = userEvent.setup();
    renderDetail(ids.failedSmsId);
    await user.selectOptions(await screen.findByLabelText('Resend by'), 'Email');
    await user.click(screen.getByRole('button', { name: 'Resend' }));
    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent(
      "Send this message again by email to Sam Rahman's email address on file?",
    );
    await user.click(within(dialog).getByRole('button', { name: 'Resend' }));
    expect(await screen.findByRole('status')).toHaveTextContent(/^Queued\./);
  });

  it('nothing is sent before the confirm, and cancelling sends nothing', async () => {
    let posts = 0;
    server.use(
      http.post('*/api/admin/notifications/:id/resend', () => {
        posts += 1;
        return HttpResponse.json(
          { notificationId: 'x', status: 'Queued', notBefore: null },
          { status: 201 },
        );
      }),
    );
    const user = userEvent.setup();
    renderDetail(ids.sentSmsId);
    await user.click(await screen.findByRole('button', { name: 'Resend' }));
    await user.click(
      within(await screen.findByRole('dialog')).getByRole('button', { name: 'Cancel' }),
    );
    expect(posts).toBe(0);
  });

  it('U-E7 / U-X1: ResendLimitReached shows the limit sentence and no error code', async () => {
    const user = userEvent.setup();
    const { container } = renderDetail(ids.limitReachedId);
    await user.click(await screen.findByRole('button', { name: 'Resend' }));
    await user.click(
      within(await screen.findByRole('dialog')).getByRole('button', { name: 'Resend' }),
    );
    expect(
      await screen.findByText('This message has already been resent 3 times.'),
    ).toBeInTheDocument();
    expect(container.textContent).not.toContain('ResendLimitReached');
  });

  it('ResendChannelNotAvailable and a 403 are plain sentences', async () => {
    const user = userEvent.setup();
    server.use(
      http.post('*/api/admin/notifications/:id/resend', () =>
        HttpResponse.json(
          {
            errors: [
              {
                propertyName: 'Channel',
                errorCode: 'ResendChannelNotAvailable',
                errorMessage: 'x',
                resourceName: 'Notification',
              },
            ],
            isValid: false,
          },
          { status: 400 },
        ),
      ),
    );
    renderDetail(ids.sentSmsId);
    await user.click(await screen.findByRole('button', { name: 'Resend' }));
    await user.click(
      within(await screen.findByRole('dialog')).getByRole('button', { name: 'Resend' }),
    );
    expect(
      await screen.findByText("That channel isn't available for this message. Choose another one."),
    ).toBeInTheDocument();

    server.use(
      http.post('*/api/admin/notifications/:id/resend', () =>
        HttpResponse.json(
          { errors: [{ errorCode: 'ActionNotPermitted', errorMessage: 'no' }] },
          { status: 403 },
        ),
      ),
    );
    await user.click(screen.getByRole('button', { name: 'Resend' }));
    await user.click(
      within(await screen.findByRole('dialog')).getByRole('button', { name: 'Resend' }),
    );
    await waitFor(() =>
      expect(
        screen.getByText(
          "You don't have permission to do this. Ask an administrator if you need access.",
        ),
      ).toBeInTheDocument(),
    );
  });

  it('an in-app copy cannot be resent and says why', async () => {
    renderDetail(ids.inboxDeliveredId);
    expect(await screen.findByTestId('cannot-resend')).toHaveTextContent(
      'This is the in-app copy. Resend the text or email message instead.',
    );
  });
});
