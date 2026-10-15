import type { ReactElement } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import type { ReportCatalogueEntry, ReportColumn } from '@/lib/api/reports';

export function renderWithClient(ui: ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return { client, ...render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>) };
}

/** Answers the staff role for the mock handlers (they fall back to Administrator with no session). */
export function asAdministrator() {
  server.use(
    http.get('*/api/staff/branches/mine', () =>
      HttpResponse.json({
        branchIds: ['b0000000-0000-0000-0000-00000000a001'],
        primaryBranchId: 'b0000000-0000-0000-0000-00000000a001',
        activeBranchId: 'b0000000-0000-0000-0000-00000000a001',
        activeBranchRefusal: null,
      }),
    ),
  );
}

export const col = (
  key: string,
  label: string,
  type: string,
  extra: Partial<ReportColumn> = {},
): ReportColumn => ({ key, label, type, unit: null, sortable: true, ...extra });

export function definition(partial: Partial<ReportCatalogueEntry> = {}): ReportCatalogueEntry {
  return {
    key: 'demo',
    title: 'Demo report',
    description: 'A demo.',
    category: 'Stock',
    kind: 'Period',
    timeBucketed: false,
    defaultPeriod: 'Today',
    defaultRange: { from: '2026-10-14', to: '2026-10-14' },
    maxRangeDays: 31,
    formats: ['Csv', 'Pdf'],
    parameters: [],
    columns: [col('a', 'A', 'Text')],
    canUseAllBranches: false,
    canSchedule: false,
    ...partial,
  };
}
