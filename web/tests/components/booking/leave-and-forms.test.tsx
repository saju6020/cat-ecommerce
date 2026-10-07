import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ConfirmStep, type BookingSummary } from '@/components/booking/ConfirmStep';
import { LeaveHoldGuard } from '@/components/booking/LeaveHoldGuard';
import { ApiValidationError } from '@/lib/api/client';

const push = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push, replace: vi.fn() }),
}));

const SUMMARY: BookingSummary = {
  catName: 'Mitu',
  service: {
    name: 'FVRCP booster',
    category: 'Vaccination',
    durationMinutes: 30,
    priceAmount: 800,
    priceCurrencyCode: 'BDT',
  },
  whenLabel: 'Mon 12 Oct, 10:30',
  performerName: 'Dr Rahman',
  roomName: null,
  branchName: 'Main clinic',
  zone: 'Asia/Dhaka',
};

const FAR_FUTURE = new Date(Date.now() + 600_000).toISOString();

describe('LeaveHoldGuard — HO-APT-002 UI-3, U-X2 (the confirm step included)', () => {
  it('confirms a navigation away from a held time, stating what is released', async () => {
    const user = userEvent.setup();
    const onLeave = vi.fn();
    push.mockClear();
    render(
      <>
        {/* eslint-disable-next-line @next/next/no-html-link-for-pages -- a plain anchor on purpose: the guard intercepts raw link clicks */}
        <a href="/portal/cats">My cats</a>
        <LeaveHoldGuard
          when
          consequence="Leaving now releases Mon 12 Oct, 10:30 with Dr Rahman."
          onLeave={onLeave}
        />
      </>,
    );

    await user.click(screen.getByRole('link', { name: 'My cats' }));
    const dialog = await screen.findByRole('dialog');
    expect(
      within(dialog).getByText(/releases Mon 12 Oct, 10:30 with Dr Rahman/),
    ).toBeInTheDocument();
    expect(push).not.toHaveBeenCalled();

    await user.click(within(dialog).getByRole('button', { name: /^stay$/i }));
    expect(push).not.toHaveBeenCalled();
    expect(onLeave).not.toHaveBeenCalled();

    await user.click(screen.getByRole('link', { name: 'My cats' }));
    await user.click(
      within(await screen.findByRole('dialog')).getByRole('button', { name: /^leave$/i }),
    );
    expect(onLeave).toHaveBeenCalledTimes(1);
    expect(push).toHaveBeenCalledWith('/portal/cats');
  });

  it('does nothing when no time is held', async () => {
    const user = userEvent.setup();
    const handler = vi.fn((event: Event) => event.preventDefault());
    document.addEventListener('click', handler);
    render(
      <>
        {/* eslint-disable-next-line @next/next/no-html-link-for-pages -- a plain anchor on purpose: the guard intercepts raw link clicks */}
        <a href="/portal/cats">My cats</a>
        <LeaveHoldGuard when={false} consequence="x" onLeave={vi.fn()} />
      </>,
    );
    await user.click(screen.getByRole('link', { name: 'My cats' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    document.removeEventListener('click', handler);
  });
});

describe('ConfirmStep — HO-APT-002 §11.1 step 5', () => {
  function renderStep(onConfirm: (reason: string | null) => Promise<void>, onHoldEnded = vi.fn()) {
    render(
      <ConfirmStep
        summary={SUMMARY}
        holdExpiresOn={FAR_FUTURE}
        onHoldEnded={onHoldEnded}
        onConfirm={onConfirm}
        onBack={vi.fn()}
      />,
    );
    return onHoldEnded;
  }

  it('U-X1: a server 400 is mapped to its field, the typed text survives, and no error code is shown', async () => {
    const user = userEvent.setup();
    renderStep(() =>
      Promise.reject(
        new ApiValidationError({
          isValid: false,
          errors: [
            {
              propertyName: 'ReasonForVisit',
              errorCode: 'MaximumLengthValidator',
              errorMessage: 'The reason is too long.',
              resourceName: 'Clinic',
            },
          ],
        }),
      ),
    );
    await user.type(screen.getByLabelText(/reason for visit/i), 'Sneezing a lot');
    await user.click(screen.getByRole('button', { name: /confirm booking/i }));

    expect(await screen.findByText('The reason is too long.')).toBeInTheDocument();
    expect(screen.getByLabelText(/reason for visit/i)).toHaveValue('Sneezing a lot');
    expect(document.body.textContent).not.toContain('MaximumLengthValidator');
  });

  it('SlotHoldExpired takes the countdown-expiry path instead of showing an error', async () => {
    const user = userEvent.setup();
    const onHoldEnded = renderStep(() =>
      Promise.reject(
        new ApiValidationError({
          isValid: false,
          errors: [
            {
              propertyName: 'AppointmentId',
              errorCode: 'SlotHoldExpired',
              errorMessage: 'Expired.',
              resourceName: 'Clinic',
            },
          ],
        }),
      ),
    );
    await user.click(screen.getByRole('button', { name: /confirm booking/i }));
    expect(onHoldEnded).toHaveBeenCalledTimes(1);
    expect(screen.queryByText('Expired.')).not.toBeInTheDocument();
  });

  it('W8: starts from the reason typed before an earlier hold ended, and reports what is typed', async () => {
    const user = userEvent.setup();
    const onReasonChange = vi.fn();
    render(
      <ConfirmStep
        summary={SUMMARY}
        holdExpiresOn={FAR_FUTURE}
        onHoldEnded={vi.fn()}
        onConfirm={() => Promise.resolve()}
        onBack={vi.fn()}
        initialReason="Sneezing a lot"
        onReasonChange={onReasonChange}
      />,
    );
    const box = screen.getByLabelText(/reason for visit/i);
    expect(box).toHaveValue('Sneezing a lot');
    await user.type(box, '!');
    expect(onReasonChange).toHaveBeenLastCalledWith('Sneezing a lot!');
  });

  it('U-A10: shows the price with "Pay at the clinic" and has no payment control', () => {
    renderStep(() => Promise.resolve());
    expect(screen.getByText(/pay at the clinic/i)).toBeInTheDocument();
    expect(screen.queryByRole('textbox', { name: /card|payment/i })).toBeNull();
    expect(screen.queryByRole('button', { name: /pay/i })).toBeNull();
  });
});
