import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AuditLogSearch } from '@/components/audit/AuditLogSearch';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace: vi.fn() }),
  usePathname: () => '/staff/admin/audit-log',
  useSearchParams: () => new URLSearchParams(),
}));

describe('AuditLogSearch — HO-ADM-002 §11.7, US-ADM-06', () => {
  it("U-G1: a range over 366 days is blocked with the server's rule, shown before submit", () => {
    render(
      <AuditLogSearch
        filters={{ from: '2020-01-01', to: '2027-01-01' }}
        onFilterChange={vi.fn()}
        entries={[]}
        totalCount={0}
        pageSize={50}
        isLoading={false}
        isError={false}
        onRetry={vi.fn()}
        exportUrl="https://api.example.com/export.csv"
        timeZoneId="Asia/Dhaka"
      />,
    );
    expect(screen.getByRole('alert')).toHaveTextContent(/366 days/);
  });

  it('U-G3: an export-size note is always visible next to the export link', () => {
    render(
      <AuditLogSearch
        filters={{ from: '2027-01-01', to: '2027-01-07' }}
        onFilterChange={vi.fn()}
        entries={[]}
        totalCount={0}
        pageSize={50}
        isLoading={false}
        isError={false}
        onRetry={vi.fn()}
        exportUrl="https://api.example.com/export.csv"
        timeZoneId="Asia/Dhaka"
      />,
    );
    expect(screen.getByText(/too many entries to export/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /export csv/i })).toHaveAttribute(
      'href',
      'https://api.example.com/export.csv',
    );
  });

  it('U-G4: filter inputs call back with the field name and value — the page owns URL round-tripping', async () => {
    const onFilterChange = vi.fn();
    const user = userEvent.setup();
    render(
      <AuditLogSearch
        filters={{ from: '2027-01-01', to: '2027-01-07' }}
        onFilterChange={onFilterChange}
        entries={[]}
        totalCount={0}
        pageSize={50}
        isLoading={false}
        isError={false}
        onRetry={vi.fn()}
        exportUrl="https://api.example.com/export.csv"
        timeZoneId="Asia/Dhaka"
      />,
    );
    await user.type(screen.getByLabelText('action'), 'X');
    expect(onFilterChange).toHaveBeenLastCalledWith('action', 'X');
  });

  it('a row can be expanded to show its before/after diff', async () => {
    const user = userEvent.setup();
    render(
      <AuditLogSearch
        filters={{ from: '2027-01-01', to: '2027-01-07' }}
        onFilterChange={vi.fn()}
        entries={[
          {
            occurredOn: '2027-01-05T10:00:00Z',
            actorUserId: 'a1',
            actorDisplayName: 'Ada Administrator',
            action: 'ServicePriceSet',
            category: 'DataChange',
            entityType: 'ServicePriceAggregate',
            entityId: 's1',
            reason: null,
            beforeValue: JSON.stringify({ amount: '1500.00' }),
            afterValue: JSON.stringify({ amount: '1800.00' }),
            ipAddress: '203.0.113.10',
          },
        ]}
        totalCount={1}
        pageSize={50}
        isLoading={false}
        isError={false}
        onRetry={vi.fn()}
        exportUrl="https://api.example.com/export.csv"
        timeZoneId="Asia/Dhaka"
      />,
    );
    await user.click(screen.getByRole('button', { name: /show details/i }));
    expect(screen.getByText('1500.00')).toBeInTheDocument();
    expect(screen.getByText('1800.00')).toBeInTheDocument();
  });
});
