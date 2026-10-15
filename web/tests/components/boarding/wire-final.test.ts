import { describe, expect, it } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { getStayIncidents } from '@/lib/api/boarding-care';
import {
  createEnquiry,
  getMyIncident,
  getMyStay,
  getMyStayDiary,
  getMyStaySummary,
  toInternalLink,
} from '@/lib/api/my-boarding';
import {
  getBoardingDashboard,
  getBoardingOccupancy,
  getCheckOutPreview,
  getDaycareDayList,
  getDaycareRateCards,
  getStaffStay,
} from '@/lib/api/staff-boarding';
import built from './built-wire.json';

/**
 * HO-BRD-002 section 1 Amended: the FINAL HO-BRD-001 wire (its 1.1 Amended, 79b1236d / 95a97bab), captured
 * from the real API run against `catecommerce_brd002e2e` and fed through the adapters.
 */

const API = '*/api';

describe('the final stay view', () => {
  it('maps charges, incidents, referrals, approvals, belongings and transitions for a customer', async () => {
    server.use(
      http.get(`${API}/customers/me/boarding/stays/s1`, () =>
        HttpResponse.json(built.customerStay),
      ),
    );
    const stay = await getMyStay('s1');
    expect(stay.allowedTransitions).toEqual(['CheckOut', 'Extend', 'Relocate']);
    expect(stay.unitName).toBe('Standard 1');
    expect(stay.charges).toHaveLength(1);
    expect(stay.charges[0]!).toMatchObject({ kind: 'RateUnit', amount: { amount: '3000.00' } });
    expect(stay.cats[0]!.awayFromUnit).toBe(true);
    expect(stay.cats[0]!.awayReason).toBe('At the clinic');
    expect(stay.cats[0]!.belongings?.[0]).toMatchObject({ kind: 'Carrier', returned: null });
    expect(stay.cats[0]!.checkedInWeightKg).toBe('4.2');
    // the incident carries no cat name or acknowledged flag on the wire; its occurredOn of 0001 is unset
    expect(stay.incidents[0]!).toMatchObject({ catName: 'Mittens', acknowledged: false });
    expect(stay.incidents[0]!.occurredOn).toBeNull();
    expect(stay.pendingApprovals).toHaveLength(1);
    expect(stay.pendingApprovals[0]!.amount.amount).toBe('3500.00');
    expect(stay.invoices).toEqual([]);
  });

  it('maps the staff view: referral without referredBy, allocations, history by id', async () => {
    server.use(
      http.get(`${API}/staff/boarding/stays/s1`, () => HttpResponse.json(built.staffStay)),
    );
    const stay = await getStaffStay('s1');
    expect(stay.referrals[0]!).toMatchObject({ catName: 'Mittens', returnedOn: null });
    expect(stay.referrals[0]!.referredBy ?? null).toBeNull(); // not on the wire: never invented
    expect(stay.allocations[0]!.unitName).toBeTruthy();
    expect(typeof stay.statusHistory[0]!.changedBy).toBe('string');
  });
});

describe('the final reads', () => {
  it('maps the dashboard rows without customer names, kinds or reasons', async () => {
    server.use(
      http.get(`${API}/staff/boarding/dashboard`, () => HttpResponse.json(built.dashboard)),
    );
    const dash = await getBoardingDashboard();
    expect(dash.occupancy.byType.find((t) => t.unitType === 'Standard')).toMatchObject({
      occupied: 1,
      active: 2,
    });
    expect(dash.notEating[0]!).toMatchObject({ catName: 'Mittens', unitName: 'S-1' });
    expect(dash.unassignedTasks[0]!.dueLocalTime).toMatch(/^\d\d:\d\d$/);
    expect(dash.unassignedTasks[0]!.minutesLate).toBeNull();
    expect(dash.openIncidents[0]!.kind).toBeUndefined();
  });

  it('maps the diary (completedTasks, no observation ids) and the summary (billing ids, numbers)', async () => {
    server.use(
      http.get(`${API}/customers/me/boarding/stays/s1/diary`, () => HttpResponse.json(built.diary)),
      http.get(`${API}/customers/me/boarding/stays/s1/summary`, () =>
        HttpResponse.json(built.summary),
      ),
    );
    const diary = await getMyStayDiary('s1');
    expect(diary.days[0]!.completedTaskCount).toBe(1);
    expect(diary.days[0]!.observations[0]!.id).toBeTruthy();
    expect(diary.catHeaders[0]!.catName).toBe('Mittens');
    const summary = await getMyStaySummary('s1');
    expect(summary.cats[0]!.weightAtCheckInKg).toBe('4.2');
    expect(summary.cats[0]!.dailyLogs[0]!.observations).toHaveLength(2);
    expect(summary.cats[0]!.belongings[0]!.kind).toBe('Carrier');
    expect(summary.invoices).toEqual([]);
    expect(summary.depositReceived.amount).toBe('900.00');
  });

  it('keeps the occupancy cat names and turnaround, and a flat daycare rate list', async () => {
    server.use(
      http.get(`${API}/staff/boarding/occupancy`, () => HttpResponse.json(built.occupancy)),
      http.get(`${API}/staff/boarding/daycare-rate-cards`, () =>
        HttpResponse.json(built.daycareRates),
      ),
      http.get(`${API}/staff/daycare`, () => HttpResponse.json(built.daycareDay)),
    );
    const grid = await getBoardingOccupancy('2026-10-15', '2026-10-17');
    const standard = grid.units.find((u) => u.code === 'S-1')!;
    expect(standard.stays[0]!.catNames).toEqual(['Mittens']);
    const rates = await getDaycareRateCards();
    expect(rates.current?.dayRate.amount).toBe('800.00');
    const day = await getDaycareDayList('2026-10-16');
    expect(day.visits[0]!).toMatchObject({
      catName: 'Biscuit',
      basis: 'Day',
      catIsHighRisk: false,
    });
    expect(day.branchTimeZoneId).toBe('Asia/Dhaka');
  });

  it('reads the incident queue by stay and fills the customer fields', async () => {
    server.use(
      http.get(`${API}/staff/boarding/incidents`, ({ request }) => {
        expect(new URL(request.url).searchParams.get('stayId')).toBe('s1');
        return HttpResponse.json(built.incidents);
      }),
      http.get(`${API}/customers/me/boarding/stays/s1/incidents/i1`, () =>
        HttpResponse.json(built.incidents[0]!),
      ),
    );
    const incident = (await getStayIncidents('s1'))[0]!;
    expect(incident.occurredOn).toBe(incident.reportedOn);
    expect(incident.customerName).toBeNull();
    expect((await getMyIncident('s1', 'i1')).branchTimeZoneId).toBe('Asia/Dhaka');
  });

  it('reads the check-out preview as the server sent it', async () => {
    server.use(
      http.get(`${API}/staff/boarding/stays/s1/check-out-preview`, () =>
        HttpResponse.json(built.preview),
      ),
    );
    const preview = await getCheckOutPreview('s1');
    expect(preview).toMatchObject({ isEarly: true, nightsBooked: 3, nightsCharged: 1 });
    expect(preview.lines.map((l) => l.action)).toEqual(['Void', 'Record']);
    expect(preview.creditOutcome).toBe('None');
    expect(preview.newTotal.amount).toBe('1000.00');
  });
});

describe('the gate link', () => {
  it('turns the absolute booking link into a site-relative one', async () => {
    expect(toInternalLink('http://localhost:3117/book?catId=1&category=Vaccination')).toBe(
      '/book?catId=1&category=Vaccination',
    );
    expect(toInternalLink('/book?x=1')).toBe('/book?x=1');
    server.use(
      http.post(`${API}/customers/me/boarding/enquiries`, () =>
        HttpResponse.json({
          stayId: 's',
          vaccinationGate: {
            passed: false,
            blockedCats: [
              {
                catId: 'c',
                catName: 'Tiger',
                bookingLink: 'https://evil.example/book?catId=c',
                reasons: [],
              },
            ],
          },
          quote: null,
        }),
      ),
    );
    const result = await createEnquiry({
      branchId: 'b',
      arrivalOn: '2026-10-20',
      departureOn: '2026-10-22',
      unitType: 'Standard',
      catIds: ['c'],
    });
    // only the path and query are followed: the host is dropped
    expect(result.vaccinationGate.blockedCats[0]!.bookingLink).toBe('/book?catId=c');
  });
});
