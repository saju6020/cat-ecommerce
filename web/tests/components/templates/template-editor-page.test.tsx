import { Suspense } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import {
  resetNotificationsMockData,
  notificationFixtureIds as ids,
} from '@/mocks/handlers/notifications';
import EditorPage from '@/app/(staff)/staff/admin/notification-templates/[key]/page';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

const KEY = '*/api/admin/notification-templates/:key';
const REFRESH_FAILED = /couldn.t refresh this template just now/;

async function renderPage(sessionGate?: Promise<void>) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  server.use(
    http.get('*/api/staff/session', async () => {
      if (sessionGate) await sessionGate;
      return HttpResponse.json({
        staffAccountId: 's-1',
        fullName: 'Ada Administrator',
        roleIds: ['Administrator'],
        issuedOn: new Date().toISOString(),
        lastActivityOn: new Date().toISOString(),
        idleExpiresOn: new Date(Date.now() + 3_600_000).toISOString(),
        absoluteExpiresOn: new Date(Date.now() + 3_600_000).toISOString(),
        idleTimeoutMinutes: 30,
        serverTime: new Date().toISOString(),
      });
    }),
  );
  // `use(params)` suspends once; render inside an awaited act so React can resume.
  await act(async () => {
    render(
      <QueryClientProvider client={client}>
        <Suspense fallback={<p>suspended</p>}>
          <EditorPage params={Promise.resolve({ key: ids.vaccinationKey })} />
        </Suspense>
      </QueryClientProvider>,
    );
  });
  return client;
}

beforeEach(() => resetNotificationsMockData());

describe('Template editor page — NTF backlog N4 / N15', () => {
  it('N4: shows loading, never a read-only editor, until the session is known', async () => {
    let release!: () => void;
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    await renderPage(gate);
    // The template itself has loaded by now; only the session is outstanding.
    await new Promise((r) => setTimeout(r, 150));
    expect(screen.queryByLabelText('Email subject')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Save changes' })).not.toBeInTheDocument();
    release();
    expect(await screen.findByRole('button', { name: 'Save changes' })).toBeInTheDocument();
  });

  it("N15: the 'couldn't refresh' banner has a Retry that refetches and clears it", async () => {
    const user = userEvent.setup();
    const client = await renderPage();
    await screen.findByLabelText('Email subject');

    server.use(http.get(KEY, () => new HttpResponse(null, { status: 500 })));
    await act(async () => {
      await client.invalidateQueries({
        queryKey: ['admin', 'notification-templates', ids.vaccinationKey],
        exact: true,
      });
    });
    expect(await screen.findByText(REFRESH_FAILED)).toBeInTheDocument();

    server.resetHandlers(); // the default handler serves the template again
    await user.click(screen.getByRole('button', { name: 'Retry' }));
    await waitFor(() => expect(screen.queryByText(REFRESH_FAILED)).not.toBeInTheDocument());
    expect(screen.getByLabelText('Email subject')).toBeInTheDocument();
  });
});

describe('Template editor page — NFR-USA-5 under refetch failures (CODE_REVIEW gate HIGH)', () => {
  it('U-C7-R: a failed background refetch keeps the editor and the typed value, and says so', async () => {
    const client = await renderPage();
    const subject = (await screen.findByLabelText('Email subject')) as HTMLInputElement;
    fireEvent.change(subject, { target: { value: 'Typed {{catName}} subject' } });

    server.use(http.get(KEY, () => new HttpResponse(null, { status: 500 })));
    await act(async () => {
      await client.invalidateQueries({
        queryKey: ['admin', 'notification-templates', ids.vaccinationKey],
        exact: true,
      });
    });

    expect(await screen.findByText(REFRESH_FAILED)).toBeInTheDocument();
    const still = screen.getByLabelText('Email subject') as HTMLInputElement;
    expect(still.value).toBe('Typed {{catName}} subject');
    expect(screen.queryByText("We couldn't load this template just now.")).not.toBeInTheDocument();
  });

  it('U-C7-R: a failed Reload from the 409 banner reports in place and keeps the editor and typed value', async () => {
    const user = userEvent.setup();
    await renderPage();
    const subject = (await screen.findByLabelText('Email subject')) as HTMLInputElement;
    fireEvent.change(subject, { target: { value: 'My unsaved subject {{catName}}' } });

    server.use(http.put(KEY, () => new HttpResponse(null, { status: 409 })));
    await user.click(screen.getByRole('button', { name: 'Save changes' }));
    await user.click(
      (await screen.findByRole('dialog')).querySelector('button:last-of-type') as HTMLButtonElement,
    );
    await screen.findByText('Someone else changed this template. Reload to see their version.');

    server.use(http.get(KEY, () => new HttpResponse(null, { status: 500 })));
    await user.click(screen.getByRole('button', { name: 'Reload' }));

    await waitFor(() =>
      expect(screen.getByText('Something went wrong. Please try again.')).toBeInTheDocument(),
    );
    expect((screen.getByLabelText('Email subject') as HTMLInputElement).value).toBe(
      'My unsaved subject {{catName}}',
    );
    expect(screen.getByRole('button', { name: 'Reload' })).toBeEnabled(); // pending state ended
    expect(screen.queryByText("We couldn't load this template just now.")).not.toBeInTheDocument();
  });

  it('Reload is disabled while it is pending', async () => {
    const user = userEvent.setup();
    await renderPage();
    await screen.findByLabelText('Email subject');
    server.use(http.put(KEY, () => new HttpResponse(null, { status: 409 })));
    await user.click(screen.getByRole('button', { name: 'Save changes' }));
    await user.click(
      (await screen.findByRole('dialog')).querySelector('button:last-of-type') as HTMLButtonElement,
    );
    await screen.findByText('Someone else changed this template. Reload to see their version.');

    let release!: () => void;
    const gate = new Promise<void>((r) => (release = r));
    server.use(
      http.get(KEY, async () => {
        await gate;
        return new HttpResponse(null, { status: 500 });
      }),
    );
    await user.click(screen.getByRole('button', { name: 'Reload' }));
    expect(await screen.findByRole('button', { name: 'Reloading…' })).toBeDisabled();
    release();
    await waitFor(() => expect(screen.getByRole('button', { name: 'Reload' })).toBeEnabled());
  });

  it('a template that never loaded still gets the full-page error with Try again', async () => {
    server.use(http.get(KEY, () => new HttpResponse(null, { status: 500 })));
    await renderPage();
    expect(
      await screen.findByText("We couldn't load this template just now.", undefined, {
        timeout: 4000,
      }),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument();
  });
});
