import { describe, expect, it, vi } from 'vitest';
import { type ReactNode } from 'react';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AppRouterContext } from 'next/dist/shared/lib/app-router-context.shared-runtime';
import { ClinicQueue } from '@/components/front-desk/ClinicQueue';
import { AppointmentClinicalLinks } from '@/components/visit/AppointmentClinicalLinks';
import { getClinicQueue } from '@/lib/api/front-desk';
import { ids, freshClinicBeforeEach } from './helpers';
import { signedInAs } from './session';
import { resetAppointmentsMockData } from '../../../mocks/handlers/appointments';

freshClinicBeforeEach();

const push = vi.fn();
const router = {
  push,
  replace: vi.fn(),
  back: vi.fn(),
  forward: vi.fn(),
  refresh: vi.fn(),
  prefetch: vi.fn(),
};

function Providers({ children }: { children: ReactNode }) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return (
    <AppRouterContext.Provider value={router}>
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    </AppRouterContext.Provider>
  );
}

async function renderQueue(onStart = vi.fn()) {
  resetAppointmentsMockData();
  const queue = await getClinicQueue();
  render(
    <Providers>
      <ClinicQueue
        queue={queue}
        zone="Asia/Dhaka"
        isLoading={false}
        isError={false}
        onRetry={() => undefined}
        allowedByAppointmentId={{}}
        onChangePriority={() => undefined}
        onStart={onStart}
        onComplete={() => undefined}
      />
    </Providers>,
  );
  const cards = await screen.findAllByRole('listitem');
  return {
    onStart,
    booked: cards.find(
      (c) => /Booked/.test(c.textContent ?? '') && /Mittens/.test(c.textContent ?? ''),
    )!,
  };
}

describe('Queue-card actions — HO-EMR-002 §11.9', () => {
  it('a nurse gets Record vitals and no Open consultation', async () => {
    signedInAs(['Nurse']);
    const { booked } = await renderQueue();
    expect(
      await within(booked).findByRole('button', { name: 'Record vitals for Mittens' }),
    ).toBeInTheDocument();
    expect(within(booked).queryByRole('button', { name: /open consultation/i })).toBeNull();
  });

  it('a receptionist gets neither', async () => {
    signedInAs(['Reception']);
    const { booked } = await renderQueue();
    await screen.findAllByRole('listitem');
    expect(within(booked).queryByRole('button', { name: /record vitals/i })).toBeNull();
    expect(within(booked).queryByRole('button', { name: /open consultation/i })).toBeNull();
  });

  it('U-B2/E-A1: Record vitals opens the visit, saves, and the card reads "Vitals recorded" with the time', async () => {
    const user = userEvent.setup();
    signedInAs(['Vet']);
    const { booked } = await renderQueue();
    await user.click(
      await within(booked).findByRole('button', { name: 'Record vitals for Mittens' }),
    );
    const dialog = await screen.findByRole('dialog', { name: /record vitals — mittens/i });
    await user.type(await within(dialog).findByLabelText('Weight, kilograms'), '3.9');
    await user.click(within(dialog).getByRole('button', { name: 'Save vitals' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(await within(booked).findByText(/Vitals recorded \d\d:\d\d/)).toBeInTheDocument();
  });

  it('Open consultation on a booked, assigned, checked-in cat starts it, opens the visit and goes to the workspace', async () => {
    const user = userEvent.setup();
    signedInAs(['Vet']);
    const { booked, onStart } = await renderQueue();
    await user.click(
      await within(booked).findByRole('button', { name: 'Open consultation for Mittens' }),
    );
    await waitFor(() =>
      expect(push).toHaveBeenCalledWith(expect.stringMatching(/^\/staff\/visits\/[0-9a-f-]{36}$/)),
    );
    expect(onStart).not.toHaveBeenCalled();
  });

  it('for a walk-in (needs a vet and a room) it opens the existing Start dialog instead of navigating', async () => {
    const user = userEvent.setup();
    signedInAs(['Vet']);
    push.mockClear();
    const { onStart } = await renderQueue();
    const walkIn = (await screen.findAllByRole('listitem')).find((c) =>
      /Walk-in/.test(c.textContent ?? ''),
    )!;
    await user.click(
      await within(walkIn).findByRole('button', { name: 'Open consultation for Mittens' }),
    );
    expect(onStart).toHaveBeenCalledTimes(1);
    expect(push).not.toHaveBeenCalled();
  });
});

describe('Appointment links — HO-EMR-002 §11.9', () => {
  it('shows Visit when one exists and Record consent for a procedure appointment', async () => {
    signedInAs(['Vet']);
    render(
      <Providers>
        <AppointmentClinicalLinks
          appointmentId={'a4000000-0000-0000-0000-00000000000c'}
          catId={ids.catWhiskersId}
          status="Booked"
        />
      </Providers>,
    );
    expect(await screen.findByRole('link', { name: 'Visit' })).toHaveAttribute(
      'href',
      `/staff/visits/${ids.visitOpenId}`,
    );
    expect(screen.getByRole('link', { name: 'Record consent' }).getAttribute('href')).toContain(
      '/staff/consents/new?catId=',
    );
  });

  it('shows nothing for a plain consultation with no visit yet', async () => {
    signedInAs(['Vet']);
    const { container } = render(
      <Providers>
        <AppointmentClinicalLinks
          appointmentId={'a4000000-0000-0000-0000-00000000000a'}
          catId={ids.catMittensId}
          status="Booked"
        />
      </Providers>,
    );
    await waitFor(() => expect(container).toBeEmptyDOMElement());
  });

  it('offers Record consent for ANY checked-in appointment, not only spay/neuter/dental (ARCH MEDIUM)', async () => {
    signedInAs(['Vet']);
    render(
      <Providers>
        <AppointmentClinicalLinks
          appointmentId={'a4000000-0000-0000-0000-00000000000a'}
          catId={ids.catMittensId}
          status="CheckedIn"
        />
      </Providers>,
    );
    expect(await screen.findByRole('link', { name: 'Record consent' })).toBeInTheDocument();
  });

  it('renders nothing at all without the app’s providers (HO-APT-002’s own tests render these bare)', () => {
    const { container } = render(
      <AppointmentClinicalLinks appointmentId="x" catId="y" status="Booked" />,
    );
    expect(container).toBeEmptyDOMElement();
  });
});
