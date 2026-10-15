import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReportTab } from '@/components/surgical-report/ReportTab';
import { saveReportDraft } from '@/lib/api/surgical-reports';
import { surgeryFixtureIds as ids } from '@/mocks/handlers/surgeries';
import { server } from '@/mocks/server';
import { http } from 'msw';
import { resetSurgeryTest, signInAs, surgeryAs } from '../surgery/helpers';

vi.mock('next/navigation', async () =>
  (await import('../common/pharmacyTestUtils')).navigationMock(),
);
vi.setConfig({ testTimeout: 40_000 });

beforeEach(() => {
  resetSurgeryTest();
  signInAs(['Vet']);
});

/** A client that KEEPS its cache (the app's default gcTime is 5 minutes), unlike the test default of 0. */
function retainingClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 300_000 }, mutations: { retry: false } },
  });
}

describe('R1 — coming back to the Report tab shows what was saved and keeps saving', () => {
  it('a remount in the same cache shows the saved text and the next save is not a conflict', async () => {
    const user = userEvent.setup();
    const surgery = await surgeryAs(ids.surgeryRecoveryId, ['Vet']);
    const client = retainingClient();
    const puts: Array<{ expectedVersion?: number; findings: string }> = [];
    server.events.on('request:start', async ({ request }) => {
      if (request.method === 'PUT' && request.url.endsWith('/report')) {
        puts.push((await request.clone().json()) as (typeof puts)[number]);
      }
    });
    const tree = (shown: boolean) => (
      <QueryClientProvider client={client}>
        {shown ? <ReportTab surgery={surgery} /> : <p>other tab</p>}
      </QueryClientProvider>
    );
    const view = render(tree(true));

    // First visit: write and save (the first save creates the draft).
    await user.type(await screen.findByLabelText('Findings'), 'First thoughts');
    await user.click(screen.getByRole('button', { name: 'Save draft now' }));
    await screen.findByText(/Draft saved/);

    // The vet goes to another tab and comes straight back, inside the cache time.
    view.rerender(tree(false));
    await screen.findByText('other tab');
    view.rerender(tree(true));
    expect(await screen.findByLabelText('Findings')).toHaveValue('First thoughts');

    // Keep typing: this save carries the version of the last one, so the server accepts it.
    await user.type(screen.getByLabelText('Findings'), ' and more');
    await user.click(screen.getByRole('button', { name: 'Save draft now' }));
    await waitFor(() => expect(puts).toHaveLength(2));
    expect(puts[0]!.expectedVersion).toBeUndefined();
    expect(puts[1]!.expectedVersion).toBe(1);
    expect(puts[1]!.findings).toBe('First thoughts and more');
    expect(screen.queryByText(/Someone else changed this report/)).not.toBeInTheDocument();
    expect(await screen.findByText(/Draft saved/)).toBeInTheDocument();
    server.events.removeAllListeners();
  });

  it('a save on an EXISTING draft does not remount the editor under the typist', async () => {
    const user = userEvent.setup();
    const surgery = await surgeryAs(ids.surgeryRecoveryId, ['Vet']);
    await seedDraft(surgery.id, 'Seeded findings');
    render(
      <QueryClientProvider client={retainingClient()}>
        <ReportTab surgery={surgery} />
      </QueryClientProvider>,
    );
    const findings = await screen.findByLabelText('Findings');
    expect(findings).toHaveValue('Seeded findings');
    await user.type(findings, ' abc');
    await user.click(screen.getByRole('button', { name: 'Save draft now' }));
    await screen.findByText(/Draft saved/);
    // The save moved the cached version from 1 to 2. Keying the editor on that version would remount it
    // here and drop what is typed next; it must be the same element.
    await user.type(screen.getByLabelText('Findings'), ' def');
    expect(screen.getByLabelText('Findings')).toHaveValue('Seeded findings abc def');
    expect(screen.getByLabelText('Findings')).toBe(findings);
    // Leave nothing unsaved: an unmount flush would otherwise land in the next test's database.
    await user.click(screen.getByRole('button', { name: 'Save draft now' }));
    await waitFor(() => expect(screen.getByText(/Draft saved/)).toBeInTheDocument());
    await new Promise((resolve) => setTimeout(resolve, 300));
  });
});

async function seedDraft(surgeryId: string, findings: string): Promise<void> {
  await saveReportDraft(surgeryId, {
    procedurePerformed: 'Mass removal',
    findings,
    technique: '',
    sutureMaterial: '',
    hadComplications: false,
    surgeonId: ids.surgeonRahmanId,
    assistantId: ids.nurseTanvirId,
  });
}

describe('N1 — returning to an EXISTING draft takes the setQueryData branch', () => {
  it('shows the saved text and saves with the right expectedVersion', async () => {
    const user = userEvent.setup();
    const surgery = await surgeryAs(ids.surgeryRecoveryId, ['Vet']);
    await seedDraft(surgery.id, 'Seeded findings'); // version 1 on the server
    const client = retainingClient();
    const puts: Array<{ expectedVersion?: number; findings: string }> = [];
    server.events.on('request:start', async ({ request }) => {
      if (request.method === 'PUT' && request.url.endsWith('/report')) {
        puts.push((await request.clone().json()) as (typeof puts)[number]);
      }
    });
    const tree = (shown: boolean) => (
      <QueryClientProvider client={client}>
        {shown ? <ReportTab surgery={surgery} /> : <p>other tab</p>}
      </QueryClientProvider>
    );
    const view = render(tree(true));
    await user.type(await screen.findByLabelText('Findings'), ' plus one');
    await user.click(screen.getByRole('button', { name: 'Save draft now' }));
    await screen.findByText(/Draft saved/);
    expect(puts[0]!.expectedVersion).toBe(1);

    view.rerender(tree(false));
    await screen.findByText('other tab');
    view.rerender(tree(true));
    expect(await screen.findByLabelText('Findings')).toHaveValue('Seeded findings plus one');

    await user.type(screen.getByLabelText('Findings'), ' plus two');
    await user.click(screen.getByRole('button', { name: 'Save draft now' }));
    await waitFor(() => expect(puts).toHaveLength(2));
    expect(puts[1]!.expectedVersion).toBe(2);
    expect(puts[1]!.findings).toBe('Seeded findings plus one plus two');
    expect(screen.queryByText(/Someone else changed this report/)).not.toBeInTheDocument();
    server.events.removeAllListeners();
  });
});

describe('N2 — coming back before the flush has answered', () => {
  async function slowSaves() {
    const { delay } = await import('msw');
    server.use(
      http.put('*/api/staff/surgeries/:id/report', async () => {
        await delay(700);
        // returning nothing falls through to the mock handler once the delay has passed
      }),
    );
  }

  it('first draft: waits for the in-flight save, then shows the saved text', async () => {
    const user = userEvent.setup();
    const surgery = await surgeryAs(ids.surgeryRecoveryId, ['Vet']);
    const client = retainingClient();
    const tree = (shown: boolean) => (
      <QueryClientProvider client={client}>
        {shown ? <ReportTab surgery={surgery} /> : <p>other tab</p>}
      </QueryClientProvider>
    );
    const view = render(tree(true));
    await user.type(await screen.findByLabelText('Findings'), 'typed just before leaving');
    await slowSaves();
    view.rerender(tree(false)); // the unmount flush is sent, and takes 700 ms to answer
    view.rerender(tree(true)); // ... and the vet is straight back
    expect(screen.queryByLabelText('Findings')).not.toBeInTheDocument(); // not opened on the old text
    expect(screen.getByText('Loading the report…')).toBeInTheDocument();
    expect(await screen.findByLabelText('Findings', undefined, { timeout: 10_000 })).toHaveValue(
      'typed just before leaving',
    );
  });

  it('existing draft: the same, and the next save carries the new version', async () => {
    const user = userEvent.setup();
    const surgery = await surgeryAs(ids.surgeryRecoveryId, ['Vet']);
    await seedDraft(surgery.id, 'Seeded');
    const client = retainingClient();
    const puts: Array<{ expectedVersion?: number }> = [];
    server.events.on('request:start', async ({ request }) => {
      if (request.method === 'PUT' && request.url.endsWith('/report')) {
        puts.push((await request.clone().json()) as (typeof puts)[number]);
      }
    });
    const tree = (shown: boolean) => (
      <QueryClientProvider client={client}>
        {shown ? <ReportTab surgery={surgery} /> : <p>other tab</p>}
      </QueryClientProvider>
    );
    const view = render(tree(true));
    await user.type(await screen.findByLabelText('Findings'), ' edited');
    await slowSaves();
    view.rerender(tree(false));
    view.rerender(tree(true));
    expect(screen.queryByLabelText('Findings')).not.toBeInTheDocument();
    expect(await screen.findByLabelText('Findings', undefined, { timeout: 10_000 })).toHaveValue(
      'Seeded edited',
    );
    await user.type(screen.getByLabelText('Findings'), ' again');
    await user.click(screen.getByRole('button', { name: 'Save draft now' }));
    await waitFor(() => expect(puts.length).toBeGreaterThanOrEqual(2));
    expect(puts.at(-1)!.expectedVersion).toBe(2);
    expect(screen.queryByText(/Someone else changed this report/)).not.toBeInTheDocument();
    server.events.removeAllListeners();
  });
});
