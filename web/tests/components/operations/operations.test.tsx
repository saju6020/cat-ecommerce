import { beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { resetXctMock } from '@/mocks/handlers/operations';
import { buildBackupStatus, MALFORMED_DRILL_JSON, VALID_DRILL_JSON } from '@/mocks/handlers/operations/fixtures';
import { BackupStatusCard, formatAge } from '@/components/operations/BackupStatusCard';
import { RestoreTestDueCard } from '@/components/operations/RestoreTestDueCard';
import { RecordRestoreTestForm } from '@/components/operations/RecordRestoreTestForm';
import { RestoreTestDetail } from '@/components/operations/RestoreTestDetail';
import { WebVitalsSummary } from '@/components/observability/WebVitalsSummary';
import { parseDrillOutput } from '@/lib/operations/drill';
import { buildWebVitals } from '@/mocks/handlers/operations/fixtures';
import { restoreTestStore } from '@/mocks/handlers/operations/state';
import type { RestoreTest } from '@/lib/api/operations';
import { renderWithClient } from '../offline/offlineTestUtils';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
  usePathname: () => '/staff/admin/operations/backup',
  useSearchParams: () => new URLSearchParams(),
}));
// recharts needs a layout engine jsdom does not have; the table beside it carries the data.
vi.mock('recharts', async () => {
  const actual = await vi.importActual<typeof import('recharts')>('recharts');
  return { ...actual, ResponsiveContainer: ({ children }: { children: React.ReactNode }) => <div>{children}</div> };
});

beforeEach(() => resetXctMock());

describe('U-B8 the drill JSON paste', () => {
  it('parser accepts restore-drill.sh output and rejects rubbish, naming what is wrong', () => {
    const ok = parseDrillOutput(VALID_DRILL_JSON);
    expect(ok.ok).toBe(true);
    expect(ok.ok && ok.fields.checks).toHaveLength(2);
    expect(ok.ok && ok.fields.checks[1]?.detail).toBeNull();
    const bad = parseDrillOutput(MALFORMED_DRILL_JSON);
    expect(bad).toMatchObject({ ok: false });
    expect(parseDrillOutput('[]')).toMatchObject({ ok: false });
    expect(parseDrillOutput('{"performedOn":"2026-10-10"}')).toMatchObject({ ok: false, message: expect.stringContaining('missing') });
    expect(parseDrillOutput(JSON.stringify({ ...JSON.parse(VALID_DRILL_JSON), checks: [] }))).toMatchObject({ ok: false });
  });

  it('fills the fields (still editable) from a valid paste', async () => {
    const user = userEvent.setup();
    renderWithClient(<RecordRestoreTestForm previousTests={[]} />);
    await user.click(screen.getByLabelText('Paste the drill JSON'));
    await user.paste(VALID_DRILL_JSON);
    await user.click(screen.getByRole('button', { name: 'Fill the fields from this' }));
    expect(screen.getByLabelText('Backup set label')).toHaveValue('20261010-020000F');
    expect(screen.getByLabelText('Check 1 name')).toHaveValue('Row counts match');
    await user.clear(screen.getByLabelText('Environment'));
    await user.type(screen.getByLabelText('Environment'), 'edited');
    expect(screen.getByLabelText('Environment')).toHaveValue('edited');
  });

  it('a parse error is shown and keeps what was pasted', async () => {
    const user = userEvent.setup();
    renderWithClient(<RecordRestoreTestForm previousTests={[]} />);
    await user.click(screen.getByLabelText('Paste the drill JSON'));
    await user.paste(MALFORMED_DRILL_JSON);
    await user.click(screen.getByRole('button', { name: 'Fill the fields from this' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(/isn't readable JSON/);
    expect(screen.getByLabelText('Paste the drill JSON')).toHaveValue(MALFORMED_DRILL_JSON);
  });
});

describe('U-B9 no RPO / RTO / outcome input; the result shows what the server decided', () => {
  it('has none of those inputs and shows the computed result after submit', async () => {
    const user = userEvent.setup();
    renderWithClient(<RecordRestoreTestForm previousTests={[]} />);
    for (const name of [/rpo/i, /rto/i, /outcome/i, /result/i, /passed or failed/i]) {
      expect(screen.queryByLabelText(name)).toBeNull();
    }
    await user.click(screen.getByLabelText('Paste the drill JSON'));
    await user.paste(VALID_DRILL_JSON);
    await user.click(screen.getByRole('button', { name: 'Fill the fields from this' }));
    await user.click(screen.getByRole('button', { name: 'Record restore test' }));
    expect(await screen.findByText(/PurrCare worked out/)).toBeInTheDocument();
    expect(screen.getByText(/Data lost 10 minutes · restored in 140 minutes/)).toBeInTheDocument();
    expect(within(screen.getByRole('status')).getByText('Passed')).toBeInTheDocument();
    expect(restoreTestStore.at(-1)?.detail.drillOutput).toBe(VALID_DRILL_JSON); // pasted text stored as text
  });

  it('sends every instant in UTC (the API answers a non-zero offset with a 500)', async () => {
    const seen = vi.fn();
    server.use(
      http.post('*/api/admin/operations/restore-tests', async ({ request }) => {
        seen(await request.json());
        return HttpResponse.json({ restoreTestId: 'x', outcome: 'Passed', achievedRpoMinutes: 10, achievedRtoMinutes: 140 }, { status: 201 });
      }),
    );
    const user = userEvent.setup();
    renderWithClient(<RecordRestoreTestForm previousTests={[]} />);
    await user.click(screen.getByLabelText('Paste the drill JSON'));
    await user.paste(VALID_DRILL_JSON);
    await user.click(screen.getByRole('button', { name: 'Fill the fields from this' }));
    await user.click(screen.getByRole('button', { name: 'Record restore test' }));
    await waitFor(() => expect(seen).toHaveBeenCalled());
    const body = seen.mock.calls[0]![0] as Record<string, string>;
    expect(body.simulatedFailureAt).toBe('2026-10-10T03:00:00.000Z');
    expect(body.restoreCompletedAt).toBe('2026-10-10T05:20:00.000Z');
    for (const key of ['simulatedFailureAt', 'recoveredTo', 'restoreStartedAt', 'restoreCompletedAt']) {
      expect(body[key]).toMatch(/Z$/);
    }
  });

  it('a failing drill shows Failed', async () => {
    const failing = JSON.stringify({ ...JSON.parse(VALID_DRILL_JSON), checks: [{ name: 'Row counts', passed: false, detail: 'off by 4' }] });
    const user = userEvent.setup();
    renderWithClient(<RecordRestoreTestForm previousTests={[]} />);
    await user.click(screen.getByLabelText('Paste the drill JSON'));
    await user.paste(failing);
    await user.click(screen.getByRole('button', { name: 'Fill the fields from this' }));
    await user.click(screen.getByRole('button', { name: 'Record restore test' }));
    expect(within(await screen.findByRole('status')).getByText('Failed')).toBeInTheDocument();
  });
});

describe('U-X1 a server 400 is mapped to its field and the error code never shows', () => {
  it('InvalidRestoreTestTimes is shown as a sentence', async () => {
    server.use(
      http.post('*/api/admin/operations/restore-tests', () =>
        HttpResponse.json(
          { isValid: false, errors: [{ propertyName: 'RestoreCompletedAt', errorCode: 'InvalidRestoreTestTimes', errorMessage: '', resourceName: 'x' }] },
          { status: 400 },
        ),
      ),
    );
    const user = userEvent.setup();
    renderWithClient(<RecordRestoreTestForm previousTests={[]} />);
    await user.click(screen.getByLabelText('Paste the drill JSON'));
    await user.paste(VALID_DRILL_JSON);
    await user.click(screen.getByRole('button', { name: 'Fill the fields from this' }));
    await user.click(screen.getByRole('button', { name: 'Record restore test' }));
    await waitFor(() => expect(document.body.textContent).toMatch(/The times do not run in order/));
    expect(document.body.textContent).not.toMatch(/InvalidRestoreTestTimes/);
  });
});

describe('ARCHITECTURE HIGH H2: drillOutput is the pasted drill text, never made up', () => {
  it('refuses to submit without a paste, says why, and sends nothing', async () => {
    const posted = vi.fn();
    server.use(http.post('*/api/admin/operations/restore-tests', () => { posted(); return HttpResponse.json({}, { status: 201 }); }));
    const user = userEvent.setup();
    renderWithClient(<RecordRestoreTestForm previousTests={[]} />);
    await user.click(screen.getByRole('button', { name: 'Record restore test' }));
    expect(await screen.findByText(/Paste the drill output first/)).toBeInTheDocument();
    expect(posted).not.toHaveBeenCalled();
  });

  it('sends the original paste exactly as entered, even after the fields were edited', async () => {
    const seen = vi.fn();
    server.use(http.post('*/api/admin/operations/restore-tests', async ({ request }) => {
      seen(await request.json());
      return HttpResponse.json({ restoreTestId: 'x', outcome: 'Passed', achievedRpoMinutes: 1, achievedRtoMinutes: 2 }, { status: 201 });
    }));
    const paste = '  ' + VALID_DRILL_JSON + '\n\n  ';
    const user = userEvent.setup();
    renderWithClient(<RecordRestoreTestForm previousTests={[]} />);
    await user.click(screen.getByLabelText('Paste the drill JSON'));
    await user.paste(paste);
    await user.click(screen.getByRole('button', { name: 'Fill the fields from this' }));
    await user.clear(screen.getByLabelText('Environment'));
    await user.type(screen.getByLabelText('Environment'), 'edited');
    await user.click(screen.getByRole('button', { name: 'Record restore test' }));
    await waitFor(() => expect(seen).toHaveBeenCalled());
    const body = seen.mock.calls[0]![0] as Record<string, string>;
    expect(body.drillOutput).toBe(paste);
    expect(body.environment).toBe('edited');
  });

  it('refuses an instant typed without an offset', async () => {
    const posted = vi.fn();
    server.use(http.post('*/api/admin/operations/restore-tests', () => { posted(); return HttpResponse.json({}, { status: 201 }); }));
    const user = userEvent.setup();
    renderWithClient(<RecordRestoreTestForm previousTests={[]} />);
    await user.click(screen.getByLabelText('Paste the drill JSON'));
    await user.paste(VALID_DRILL_JSON);
    await user.click(screen.getByRole('button', { name: 'Fill the fields from this' }));
    await user.clear(screen.getByLabelText('Restore started at'));
    await user.type(screen.getByLabelText('Restore started at'), '2026-10-10T09:10:00');
    await user.click(screen.getByRole('button', { name: 'Record restore test' }));
    expect(await screen.findByText(/with its offset/)).toBeInTheDocument();
    expect(posted).not.toHaveBeenCalled();
  });

  it('the paste parser also rejects an instant with no offset', () => {
    const noOffset = JSON.stringify({ ...JSON.parse(VALID_DRILL_JSON), restoreStartedAt: '2026-10-10T09:10:00' });
    expect(parseDrillOutput(noOffset)).toMatchObject({ ok: false, message: expect.stringContaining('no offset') });
  });
});

describe('U-B10 BackupStatusCard', () => {
  it('a 27-hour-old backup is Stale as text plus icon; a fresh one is not', () => {
    const { unmount, container } = renderWithClient(<BackupStatusCard status={buildBackupStatus('stale')} zone="Asia/Dhaka" />);
    expect(screen.getAllByText('Stale').length).toBeGreaterThan(0);
    expect(screen.getByRole('alert')).toHaveTextContent(/Backups are stale/);
    expect(container.querySelector('svg')).toBeTruthy();
    unmount();
    renderWithClient(<BackupStatusCard status={buildBackupStatus('healthy')} zone="Asia/Dhaka" />);
    expect(screen.queryByText('Stale')).toBeNull();
    expect(screen.getAllByText('Up to date').length).toBe(4);
    expect(screen.getByText(/lose at most 15 minutes of data \(RPO\) and be back within 4 hours/)).toBeInTheDocument();
    // A13 (backlog XCT row 114): the last test's achieved values sit beside the objectives.
    expect(
      screen.getByText(/Last restore test: 9 minutes of data lost, restored in 140 minutes \(passed\)\./),
    ).toBeInTheDocument();
  });

  it('says so when no restore test has been recorded', () => {
    renderWithClient(<BackupStatusCard status={buildBackupStatus('neverTested')} zone="Asia/Dhaka" />);
    expect(screen.getByText(/No restore test has been recorded yet\./)).toBeInTheDocument();
  });

  it('formats an age, not a clock', () => {
    const now = Date.parse('2026-10-14T12:00:00Z');
    expect(formatAge('2026-10-13T09:00:00Z', now)).toBe('27 hours ago');
    expect(formatAge('2026-10-14T11:48:00Z', now)).toBe('12 minutes ago');
  });
});

describe('U-B11 RestoreTestDueCard', () => {
  it('no test ever: overdue; overdue: overdue; due in 40 days: fine', () => {
    const { unmount } = renderWithClient(<RestoreTestDueCard status={buildBackupStatus('neverTested')} />);
    expect(screen.getByRole('alert')).toHaveTextContent('Restore test overdue');
    expect(screen.getByRole('alert')).toHaveTextContent('No restore test has ever been recorded');
    unmount();
    const second = renderWithClient(<RestoreTestDueCard status={buildBackupStatus('overdue')} />);
    expect(screen.getByRole('alert')).toHaveTextContent('Restore test overdue');
    second.unmount();
    renderWithClient(<RestoreTestDueCard status={buildBackupStatus('dueLater')} />);
    expect(screen.queryByRole('alert')).toBeNull();
    expect(screen.getByText(/Next restore test due/)).toBeInTheDocument();
  });
});

describe('RestoreTestDetail', () => {
  it('renders the drill output as text in a <pre>, never as markup', () => {
    const detail = restoreTestStore[0]!.detail as unknown as RestoreTest;
    const { container } = renderWithClient(<RestoreTestDetail test={detail} zone="Asia/Dhaka" />);
    const pre = container.querySelector('pre')!;
    expect(pre.textContent).toContain('<script>alert(1)</script>');
    expect(container.querySelector('script')).toBeNull();
    expect(screen.getByText('Worked out by PurrCare from the times in the drill output.')).toBeInTheDocument();
  });
});

describe('U-B12 WebVitalsSummary', () => {
  it('shows Pass / Fail / Not enough data as text with the budget beside p75', () => {
    renderWithClient(<WebVitalsSummary summary={buildWebVitals('2026-09-17', '2026-10-14')} />);
    expect(screen.getByText('Pass')).toBeInTheDocument();
    expect(screen.getByText('Fail')).toBeInTheDocument();
    expect(screen.getByText('Not enough data')).toBeInTheDocument();
    expect(screen.getAllByText('2500 ms').length).toBeGreaterThan(0); // the budget, beside p75
    expect(screen.getByText('2940 ms')).toBeInTheDocument();
    expect(screen.getByText(/fewer than 50 samples gets no verdict/)).toBeInTheDocument();
  });

  it('switches metric tabs', async () => {
    const user = userEvent.setup();
    renderWithClient(<WebVitalsSummary summary={buildWebVitals('2026-09-17', '2026-10-14')} />);
    await user.click(screen.getByRole('tab', { name: /CLS/ }));
    expect(screen.getByText('0.040')).toBeInTheDocument();
    expect(screen.getByText('0.100')).toBeInTheDocument();
  });
});
