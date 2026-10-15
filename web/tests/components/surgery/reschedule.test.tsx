import { beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { RescheduleDialog } from '@/components/surgery/RescheduleDialog';
import { surgeryFixtureIds as ids } from '@/mocks/handlers/surgeries';
import { server } from '@/mocks/server';
import { renderWithClient, resetSurgeryTest, signInAs, surgeryAs } from './helpers';

vi.mock('next/navigation', async () =>
  (await import('../common/pharmacyTestUtils')).navigationMock(),
);
vi.setConfig({ testTimeout: 40_000 });

beforeEach(() => {
  resetSurgeryTest();
  signInAs(['Reception']);
});

async function open() {
  const surgery = await surgeryAs(ids.surgeryTomorrowId, ['Reception']);
  const onClose = vi.fn();
  renderWithClient(<RescheduleDialog surgery={surgery} onClose={onClose} />);
  return { surgery, onClose };
}

describe('reschedule (US-SRG-01)', () => {
  it('is prefilled with the current team and asks for availability without the surgery’s own slot', async () => {
    const urls: string[] = [];
    server.events.on('request:start', ({ request }) => {
      if (request.url.includes('/availability')) urls.push(request.url);
    });
    const { surgery } = await open();
    await waitFor(() => expect(screen.getByLabelText('Surgeon')).toHaveValue(surgery.surgeon.id));
    await waitFor(() =>
      expect(screen.getByLabelText('Assistant')).toHaveValue(surgery.assistant.id),
    );
    await waitFor(() => expect(urls.length).toBeGreaterThan(0));
    expect(urls[0]).toContain(`excludeSurgeryId=${surgery.id}`);
    // The surgery's own 09:00 slot is therefore offered again.
    expect(
      await screen.findByRole('button', { name: /^09:00–10:00/ }, { timeout: 15_000 }),
    ).toBeInTheDocument();
    server.events.removeAllListeners();
  });

  it('confirms old → new and sends the version it was opened with', async () => {
    const user = userEvent.setup();
    const bodies: Array<Record<string, unknown>> = [];
    server.events.on('request:start', async ({ request }) => {
      if (request.method === 'POST' && request.url.endsWith('/reschedule')) {
        bodies.push((await request.clone().json()) as Record<string, unknown>);
      }
    });
    const { surgery, onClose } = await open();
    const starts = await screen.findByRole(
      'group',
      { name: /available starts/i },
      { timeout: 15_000 },
    );
    await user.click(within(starts).getByRole('button', { name: /^11:00–12:00/ }));
    await user.click(screen.getByRole('button', { name: 'Review' }));
    expect(await screen.findByText(/Move Biscuit’s mass removal from/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Reschedule' }));
    await waitFor(() => expect(onClose).toHaveBeenCalled());
    expect(bodies[0]).toMatchObject({
      expectedVersion: surgery.version,
      theatreId: surgery.theatre.id,
    });
    server.events.removeAllListeners();
  });

  it('a stale version asks for a reload and keeps the dialog open', async () => {
    const user = userEvent.setup();
    server.use(
      http.post('*/api/staff/surgeries/:id/reschedule', () =>
        HttpResponse.json(
          {
            isValid: false,
            errors: [
              {
                propertyName: 'ExpectedVersion',
                errorCode: 'StaleVersion',
                errorMessage: 'This surgery has changed.',
                resourceName: 'Surgery',
              },
            ],
          },
          { status: 409 },
        ),
      ),
    );
    const { onClose } = await open();
    const starts = await screen.findByRole(
      'group',
      { name: /available starts/i },
      { timeout: 15_000 },
    );
    await user.click(within(starts).getByRole('button', { name: /^11:00–12:00/ }));
    await user.click(screen.getByRole('button', { name: 'Review' }));
    await user.click(await screen.findByRole('button', { name: 'Reschedule' }));
    expect(await screen.findByText(/changed while you were working/)).toBeInTheDocument();
    expect(onClose).not.toHaveBeenCalled();
  });

  it('asks for a start before reviewing', async () => {
    const user = userEvent.setup();
    await open();
    await screen.findByRole('group', { name: /available starts/i }, { timeout: 15_000 });
    await user.click(screen.getByRole('button', { name: 'Review' }));
    expect(await screen.findByText('Choose a start time.')).toBeInTheDocument();
  });
});
