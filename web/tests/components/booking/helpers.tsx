import type { ReactElement } from 'react';
import { render } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '../../../mocks/server';
import { specialDays } from '../../../mocks/handlers/appointments';
import { addDaysIso } from '@/lib/format/appointment';
import { todayInZone } from '@/lib/format/time';

export function renderWithClient(ui: ReactElement) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: 0 }, mutations: { retry: false } },
  });
  return {
    ...render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>),
    client,
  };
}

/** The first Monday-to-Thursday at least `minOffset` days ahead that is neither the mock's
 *  "full day" nor its holiday, in the main branch's zone — a day the mock reliably has times on. */
export function openMockDate(minOffset = 8): string {
  const { fullDay, holiday } = specialDays();
  let date = addDaysIso(todayInZone('Asia/Dhaka'), minOffset);
  for (let guard = 0; guard < 21; guard += 1) {
    const weekday = new Date(`${date}T00:00:00Z`).getUTCDay();
    if (weekday >= 1 && weekday <= 4 && date !== fullDay && date !== holiday) return date;
    date = addDaysIso(date, 1);
  }
  return date;
}

export const SINGLE_CAT = [
  {
    id: '20000000-0000-0000-0000-000000000001',
    ownerId: 'o1',
    name: 'Momo',
    species: 'Cat',
    breed: null,
    sex: 'Female',
    isHighRisk: false,
    handlingNote: null,
    lifeStatus: 'Alive',
  },
];

/** One cat and one branch, so steps 1 and 3 are skipped (U-A1). */
export function serveSingleCatAndBranch() {
  server.use(
    http.get('*/api/cats', () =>
      HttpResponse.json({ items: SINGLE_CAT, pageNumber: 1, pageSize: 100, totalCount: 1 }),
    ),
    http.get('*/api/branches', () =>
      HttpResponse.json([
        {
          id: 'b0000000-0000-0000-0000-00000000a001',
          name: 'Main clinic',
          addressLine: '12 Gulshan Avenue, Dhaka',
          phoneNumber: '+880212345678',
          email: 'main@purrcare.test',
          timeZoneId: 'Asia/Dhaka',
          weeklyHours: [],
        },
      ]),
    ),
  );
}
