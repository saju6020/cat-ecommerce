import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ExportButtons } from '@/components/reports/ExportButtons';
import { ApiForbiddenError, ApiRateLimitedError, ApiValidationError } from '@/lib/api/client';

const download = vi.hoisted(() => ({
  calls: [] as unknown[][],
  behave: (async () => {}) as () => Promise<void>,
}));
vi.mock('@/lib/download/reportExport', () => ({
  downloadReportExport: (...args: unknown[]) => {
    download.calls.push(args);
    return download.behave();
  },
}));

const validation = (errorCode: string, errorMessage: string) =>
  new ApiValidationError({
    isValid: false,
    errors: [{ propertyName: '', errorCode, errorMessage, resourceName: '' }],
  });

const rejectWith = (make: () => unknown) => {
  download.behave = async () => {
    throw make();
  };
};

beforeEach(() => {
  download.calls = [];
  download.behave = async () => {};
});

function renderButtons() {
  return render(
    <ExportButtons
      reportKey="clinic-daily-schedule"
      query="from=2026-10-14&to=2026-10-14&pageNumber=3&pageSize=50&sort=cat"
      fallbackBaseName="purrcare-x"
    />,
  );
}

describe('ExportButtons — U-D series (ADR-FE-132)', () => {
  it('U-D1: one request with the current filters and no paging, saved under a fallback name', async () => {
    renderButtons();
    await userEvent.click(screen.getByRole('button', { name: 'Export CSV' }));
    expect(download.calls).toHaveLength(1);
    expect(download.calls[0]).toEqual([
      '/api/staff/reports/clinic-daily-schedule/export.csv?from=2026-10-14&to=2026-10-14&sort=cat',
      'purrcare-x.csv',
    ]);
    expect(await screen.findByText('Downloaded.')).toBeInTheDocument();
  });

  it('U-D2: a 413 on PDF shows the CSV advice, downloads nothing and returns to idle', async () => {
    rejectWith(() =>
      validation(
        'ReportTooLargeForPdf',
        'Too many rows for a PDF -- export CSV or narrow the filters.',
      ),
    );
    renderButtons();
    await userEvent.click(screen.getByRole('button', { name: 'Export PDF' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(/Too many rows for a PDF/);
    expect(screen.getByRole('button', { name: 'Export PDF' })).toBeEnabled();
  });

  it('U-D2: falls back to the mapped text when the server sends no message', async () => {
    rejectWith(() => validation('ReportTooLargeForPdf', ''));
    renderButtons();
    await userEvent.click(screen.getByRole('button', { name: 'Export PDF' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Too many rows for a PDF — export CSV instead.',
    );
  });

  it('U-D3: ReportTimedOut shows the shorter-range advice', async () => {
    rejectWith(() => validation('ReportTimedOut', ''));
    renderButtons();
    await userEvent.click(screen.getByRole('button', { name: 'Export CSV' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(/shorter range or a single branch/);
    expect(screen.queryByText('ReportTimedOut')).not.toBeInTheDocument();
  });

  it('says how long to wait on a 429', async () => {
    rejectWith(() => new ApiRateLimitedError(30));
    renderButtons();
    await userEvent.click(screen.getByRole('button', { name: 'Export CSV' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Try again in about 30 seconds');
  });

  it('words the 60 s fallback (Retry-After unreadable cross-origin) as an estimate', async () => {
    rejectWith(() => new ApiRateLimitedError(60));
    renderButtons();
    await userEvent.click(screen.getByRole('button', { name: 'Export CSV' }));
    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Try again in a minute or so');
    expect(alert).not.toHaveTextContent('60 seconds');
  });

  it('a 403 becomes PermissionDeniedState', async () => {
    rejectWith(
      () =>
        new ApiForbiddenError({
          errors: [{ errorCode: 'ActionNotPermitted', errorMessage: 'no' }],
        }),
    );
    renderButtons();
    await userEvent.click(screen.getByRole('button', { name: 'Export PDF' }));
    expect(await screen.findByText(/have permission/i)).toBeInTheDocument();
  });

  it('U-D4: while in flight the button is busy, disabled and announced', async () => {
    let finish: () => void = () => {};
    download.behave = () => new Promise<void>((resolve) => (finish = resolve));
    renderButtons();
    await userEvent.click(screen.getByRole('button', { name: 'Export CSV' }));
    const busy = screen.getByRole('button', { name: 'Preparing…' });
    expect(busy).toBeDisabled();
    expect(busy).toHaveAttribute('aria-busy', 'true');
    expect(screen.getByText(/Preparing your CSV file/)).toBeInTheDocument();
    finish();
    await waitFor(() => expect(screen.getByRole('button', { name: 'Export CSV' })).toBeEnabled());
  });
});
