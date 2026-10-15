import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ExportButton } from '@/components/inventory/ExportButton';
import { SupplierForm } from '@/components/procurement/SupplierForm';
import { EMPTY_ERRORS, errorsForRow, stampRowKeys } from '@/components/inventory/lineErrors';
import type { SupplierDetail } from '@/lib/api/procurement-suppliers';

vi.mock('next/navigation', async () => {
  const { navigation } = await import('./testUtils');
  return {
    useRouter: () => ({ push: navigation.push, replace: navigation.replace, back: vi.fn() }),
    usePathname: () => navigation.pathname,
    useSearchParams: () => navigation.search,
  };
});

vi.mock('@/lib/branch', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/lib/branch')>()),
  getActiveBranchId: () => 'branch-two',
}));

interface Sent {
  method: string;
  url: string;
  headers: Record<string, string>;
}

function installXhr(response: { status: number; body: string; disposition?: string }): Sent[] {
  const sent: Sent[] = [];
  class FakeXhr {
    status = 0;
    response: Blob | null = null;
    withCredentials = false;
    responseType = '';
    onload: (() => void) | null = null;
    onerror: (() => void) | null = null;
    ontimeout: (() => void) | null = null;
    private method = '';
    private url = '';
    private headers: Record<string, string> = {};
    open(method: string, url: string) {
      this.method = method;
      this.url = url;
    }
    setRequestHeader(name: string, value: string) {
      this.headers[name] = value;
    }
    getResponseHeader(name: string) {
      return name.toLowerCase() === 'content-disposition' ? (response.disposition ?? null) : null;
    }
    send() {
      sent.push({ method: this.method, url: this.url, headers: { ...this.headers } });
      this.status = response.status;
      this.response = new Blob([response.body]);
      queueMicrotask(() => this.onload?.());
    }
  }
  vi.stubGlobal('XMLHttpRequest', FakeXhr);
  return sent;
}

afterEach(() => {
  vi.unstubAllGlobals();
  document.cookie = 'pc_csrf=; expires=Thu, 01 Jan 1970 00:00:00 GMT';
});

describe('ExportButton (ARCH HIGH: branch-scoped CSV)', () => {
  it('requests the file with X-Branch-Id and the CSRF token, then saves what the server returned', async () => {
    document.cookie = 'pc_csrf=csrf-123';
    const sent = installXhr({ status: 200, body: 'a,b\n1,2', disposition: 'attachment; filename="ledger.csv"' });
    const createObjectURL = vi.fn(() => 'blob:x');
    const revokeObjectURL = vi.fn();
    vi.stubGlobal('URL', Object.assign(URL, { createObjectURL, revokeObjectURL }));
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
    vi.useFakeTimers({ shouldAdvanceTime: true });

    render(<ExportButton path="/api/staff/inventory/ledger/export.csv?from=a" fallbackName="x.csv" />);
    await userEvent.setup({ advanceTimers: vi.advanceTimersByTime }).click(screen.getByRole('button', { name: /export csv/i }));

    await waitFor(() => expect(click).toHaveBeenCalled());
    expect(sent).toHaveLength(1);
    expect(sent[0]!.method).toBe('GET');
    expect(sent[0]!.url).toContain('/api/staff/inventory/ledger/export.csv?from=a');
    expect(sent[0]!.headers['X-Branch-Id']).toBe('branch-two');
    expect(sent[0]!.headers['X-CSRF-Token']).toBe('csrf-123');
    expect(createObjectURL).toHaveBeenCalledTimes(1);
    vi.advanceTimersByTime(11_000);
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:x');
    vi.useRealTimers();
    click.mockRestore();
  });

  it('shows a plain-language error when the server refuses', async () => {
    installXhr({ status: 403, body: '{}' });
    render(<ExportButton path="/api/staff/inventory/reports/near-expiry.csv" fallbackName="x.csv" />);
    await userEvent.click(screen.getByRole('button', { name: /export csv/i }));
    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /export csv/i })).toBeEnabled();
  });
});

describe('line errors follow their row key (U-X1)', () => {
  it('an error stays with its row when an earlier row is removed', () => {
    const stamped = stampRowKeys({ ...EMPTY_ERRORS, lines: { 1: { quantity: 'Too many.' } } }, ['a', 'b', 'c']);
    // Row "a" was removed: "b" is now index 0 but keeps its error; "c" (index 1) must not inherit it.
    expect(errorsForRow(stamped, 'b', 0)).toEqual({ quantity: 'Too many.' });
    expect(errorsForRow(stamped, 'c', 1)).toEqual({});
    expect(errorsForRow(stamped, 'a', 0)).toEqual({});
  });

  it('unstamped errors fall back to the index', () => {
    expect(errorsForRow({ ...EMPTY_ERRORS, lines: { 0: { batchId: 'x' } } }, 'k', 0)).toEqual({ batchId: 'x' });
  });
});

describe('SupplierForm re-seeding (CODE: pristine or new record only)', () => {
  const supplier: SupplierDetail = {
    id: 's1',
    code: 'MERIDIAN',
    name: 'Meridian Pet Supplies',
    countryCode: 'BD',
    contactName: null,
    phone: null,
    email: null,
    address: null,
    currencyCode: 'BDT',
    leadTimeDays: 7,
    paymentTerms: 'Net 30',
    paymentTermDays: 30,
    notes: null,
    isActive: true,
    version: 1,
  };
  const props = { onSubmit: vi.fn(), isPending: false, submitLabel: 'Save' };

  it('keeps what is being typed when the same record is refetched', async () => {
    const { rerender } = render(<SupplierForm {...props} initial={supplier} />);
    const name = screen.getByLabelText(/^name/i);
    await userEvent.clear(name);
    await userEvent.type(name, 'Typed name');
    rerender(<SupplierForm {...props} initial={{ ...supplier, version: 2 }} />);
    expect(screen.getByLabelText(/^name/i)).toHaveValue('Typed name');
  });

  it('takes the refetched values when the form is untouched', () => {
    const { rerender } = render(<SupplierForm {...props} initial={supplier} />);
    rerender(<SupplierForm {...props} initial={{ ...supplier, name: 'Renamed upstream', version: 2 }} />);
    expect(screen.getByLabelText(/^name/i)).toHaveValue('Renamed upstream');
  });

  it('switches to a different record even when the form is dirty', async () => {
    const { rerender } = render(<SupplierForm {...props} initial={supplier} />);
    await userEvent.type(screen.getByLabelText(/^name/i), ' extra');
    rerender(<SupplierForm {...props} initial={{ ...supplier, id: 's2', name: 'Other supplier' }} />);
    expect(screen.getByLabelText(/^name/i)).toHaveValue('Other supplier');
  });
});
