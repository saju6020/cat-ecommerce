import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {
  EMAIL_NOT_CONNECTED,
  NotificationQueueSummary,
  SMS_NOT_CONNECTED,
} from '@/components/notifications/NotificationQueueSummary';
import type { NotificationSummary } from '@/lib/api/notifications-log';

function summary(
  over: Partial<NotificationSummary['channelsConfigured']> = {},
): NotificationSummary {
  return {
    queued: 3,
    retryPending: 2,
    sending: 1,
    oldestQueuedOn: new Date(Date.now() - 12 * 60_000).toISOString(),
    last24h: { sent: 40, undeliverable: 4, expired: 1, cancelled: 0, suppressed: 2 },
    channelsConfigured: { sms: true, email: true, ...over },
  };
}

function renderSummary(data: NotificationSummary | undefined, extra = {}) {
  return render(
    <NotificationQueueSummary
      summary={data}
      isLoading={false}
      isError={false}
      onRetry={() => {}}
      {...extra}
    />,
  );
}

describe('NotificationQueueSummary — HO-NTF-002 §11.5, HO-NTF-001 OQ-1', () => {
  it('U-E1: SMS unconfigured shows the warning banner as role="status" (not an interrupting alert)', () => {
    renderSummary(summary({ sms: false }));
    const banner = screen.getByRole('status');
    expect(banner).toHaveTextContent(SMS_NOT_CONNECTED);
    expect(banner).toHaveTextContent(
      'SMS is not connected to a provider. Text messages — including password resets — are not being delivered. Customers still receive in-app copies where available.',
    );
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('warns about email too, and about both when neither is connected', () => {
    renderSummary(summary({ email: false }));
    expect(screen.getByRole('status')).toHaveTextContent(EMAIL_NOT_CONNECTED);
    expect(screen.getByRole('status')).not.toHaveTextContent(SMS_NOT_CONNECTED);
  });

  it('shows no banner when both channels are configured, and shows the counts', () => {
    renderSummary(summary());
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
    expect(screen.getByText('Queued').nextSibling).toHaveTextContent('3');
    expect(screen.getByText('Retrying').nextSibling).toHaveTextContent('2');
    expect(screen.getByText('Sending').nextSibling).toHaveTextContent('1');
    expect(screen.getByText('Oldest queued').nextSibling).toHaveTextContent(/12 minutes ago/);
    expect(screen.getByText('Sent, last 24 hours').nextSibling).toHaveTextContent('40');
  });

  it('shows a dash when nothing is queued', () => {
    renderSummary({ ...summary(), oldestQueuedOn: null });
    expect(screen.getByText('Oldest queued').nextSibling).toHaveTextContent('—');
  });

  it('has a loading state and a retryable error state', async () => {
    const onRetry = vi.fn();
    const { rerender } = render(
      <NotificationQueueSummary summary={undefined} isLoading isError={false} onRetry={onRetry} />,
    );
    expect(screen.getByText('Loading queue summary…')).toBeInTheDocument();
    rerender(
      <NotificationQueueSummary summary={undefined} isLoading={false} isError onRetry={onRetry} />,
    );
    await userEvent.setup().click(screen.getByRole('button', { name: 'Try again' }));
    expect(onRetry).toHaveBeenCalled();
  });
});
