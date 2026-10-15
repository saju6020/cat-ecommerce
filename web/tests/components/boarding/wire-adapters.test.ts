import { describe, expect, it } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { getStayObservations, getTaskDayList } from '@/lib/api/boarding-care';
import { getMyStay, getMyStays } from '@/lib/api/my-boarding';
import { checkInStay, getBoardingOccupancy, getStaffStays } from '@/lib/api/staff-boarding';

/**
 * HO-BRD-002 section 1 Amended: the shapes the BUILT HO-BRD-001 sends (its `ho/BRD-001` view models,
 * 2026-10-14) fed through the adapters the screens depend on. These fixtures are written from the C#
 * view-model records, not from the mock.
 */

const money = (amount: string) => ({ amount, currencyCode: 'BDT' });
const API = '*/api';

describe('stay view (StayViewModel)', () => {
  it('maps unit, deposit, per-cat gate and check-in; fills what the built view omits', async () => {
    server.use(
      http.get(`${API}/customers/me/boarding/stays/s1`, () =>
        HttpResponse.json({
          id: 's1',
          version: 3,
          branchId: 'b',
          branchTimeZoneId: 'Asia/Dhaka',
          customerId: 'c',
          customerName: 'Sam',
          status: 'Reserved',
          allowedTransitions: ['Confirm', 'Cancel'],
          channel: 'Online',
          arrivalOn: '2026-10-20',
          departureOn: '2026-10-22',
          occupiedFrom: '2026-10-20T08:00:00Z',
          occupiedTo: '2026-10-22T08:00:00Z',
          nights: 2,
          requestedUnitType: 'Deluxe',
          unit: { unitId: 'u', code: 'D-1', name: 'Deluxe 1', unitType: 'Deluxe', block: 'Main' },
          requiresIsolation: false,
          needsAttention: true,
          attentionReason: 'CatOwnerChanged',
          quote: null,
          deposit: {
            required: money('900.00'),
            state: 'Awaiting',
            dueBy: '2026-10-16T10:00:00Z',
            received: money('0.00'),
            pending: money('0.00'),
            outstanding: money('900.00'),
          },
          balanceDue: money('3000.00'),
          cats: [
            {
              catId: 'k',
              catName: 'Mitu',
              catIsHighRisk: true,
              handlingNote: 'Bites',
              gate: {
                status: 'Blocked',
                reasons: [
                  {
                    vaccineCode: 'RAB',
                    vaccineName: 'Rabies',
                    reason: 'NeverAdministered',
                    dueOn: null,
                  },
                ],
                evaluatedOn: '2026-10-14',
                overridden: false,
              },
              careProfile: {
                feedings: [
                  {
                    time: '08:00',
                    portion: '1 cup',
                    foodSource: 'OwnFood',
                    foodDescription: 'Dry',
                    houseFoodSkuId: null,
                  },
                ],
                medications: [],
                litterType: 'Clumping',
                litterNote: null,
                vetVisitAuthorised: true,
                vetSpendingLimit: money('2000.00'),
                emergencyContact: { name: 'Sam', phone: '017', relationship: null },
                notes: null,
              },
              suspectedInfectious: false,
              infectiousNote: null,
              checkIn: {
                checkedInOn: '2026-10-20T09:00:00Z',
                weightKg: 4.2,
                noBelongings: true,
                belongings: [],
              },
            },
          ],
          statusHistory: [],
          cancelledOn: null,
          cancellationReason: null,
          depositForfeited: false,
          allocations: null,
          isolationEntries: null,
          inIsolationUnit: false,
        }),
      ),
    );
    const stay = await getMyStay('s1');
    expect(stay.unitName).toBe('Deluxe 1');
    expect(stay.unitBlock).toBe('Main');
    expect(stay.deposit.payBy).toBe('2026-10-16T10:00:00Z');
    expect(stay.deposit.accepted.amount).toBe('0.00');
    expect(stay.cats[0]).toMatchObject({
      gateStatus: 'Blocked',
      careProfileComplete: true,
      checkedInWeightKg: '4.2',
    });
    expect(stay.cats[0]!.gateReasons[0]!.reason).toBe('NeverAdministered');
    expect(stay.cats[0]!.careProfile!.feedings[0]!.localTime).toBe('08:00');
    expect(stay.cats[0]!.careProfile!.vetSpendingLimit.amount).toBe('2000.00');
    expect((stay as unknown as { attention: unknown }).attention).toEqual({
      needsAttention: true,
      reason: 'CatOwnerChanged',
      note: null,
    });
    expect(stay.cancellation).toEqual({ canCancel: true, depositOutcome: null });
    expect(stay.charges).toEqual([]);
  });

  it('maps a stay summary row (requestedUnitType, unitCode)', async () => {
    server.use(
      http.get(`${API}/staff/boarding/stays`, () =>
        HttpResponse.json({
          items: [
            {
              id: 's1',
              status: 'Confirmed',
              arrivalOn: '2026-10-20',
              departureOn: '2026-10-22',
              nights: 2,
              catNames: ['Mitu'],
              requestedUnitType: 'Family',
              unitCode: 'F-1',
              depositState: 'Received',
              balanceDue: money('10.00'),
              customerId: 'c',
              customerName: 'Sam',
            },
          ],
          pageNumber: 1,
          pageSize: 20,
          totalCount: 1,
        }),
      ),
      http.get(`${API}/customers/me/boarding/stays`, () =>
        HttpResponse.json({
          items: [
            {
              id: 's1',
              status: 'Enquiry',
              arrivalOn: '2026-10-20',
              departureOn: '2026-10-22',
              nights: 2,
              catNames: ['Mitu'],
              requestedUnitType: 'Family',
              unitCode: null,
              depositState: 'NotRequired',
              balanceDue: null,
            },
          ],
          pageNumber: 1,
          pageSize: 20,
          totalCount: 1,
        }),
      ),
    );
    const staff = await getStaffStays({});
    expect(staff.items[0]).toMatchObject({
      unitName: 'F-1',
      customerName: 'Sam',
      needsAttention: false,
    });
    const mine = await getMyStays('upcoming');
    expect(mine.items[0]).toMatchObject({ unitType: 'Family', balanceDue: null });
  });
});

describe('care reads (CareTaskDayListViewModel, StayObservationsViewModel)', () => {
  it('groups tasks by unit and reads the cats off the tasks', async () => {
    server.use(
      http.get(`${API}/staff/boarding/tasks`, () =>
        HttpResponse.json({
          date: '2026-10-14',
          branchTimeZoneId: 'Asia/Dhaka',
          total: 1,
          pending: 1,
          overdue: 1,
          unassigned: 0,
          units: [
            {
              unitId: 'u',
              code: 'ISO-1',
              name: 'Isolation 1',
              unitType: 'Isolation',
              block: 'Iso',
              tasks: [
                {
                  id: 't',
                  stayId: 's',
                  catId: 'k',
                  catName: 'Kitty',
                  catIsHighRisk: true,
                  handlingNote: 'Bites',
                  kind: 'Feed',
                  title: 'Feed',
                  dueOn: '2026-10-14T03:00:00Z',
                  dueLocalTime: '09:00',
                  status: 'Pending',
                  isOverdue: true,
                  assignedCaretakerId: null,
                  assignedCaretakerName: 'Tanvir',
                  completedOn: null,
                  completedBy: null,
                  note: null,
                  skipReason: null,
                },
              ],
            },
          ],
        }),
      ),
    );
    const list = await getTaskDayList();
    expect(list.units[0]).toMatchObject({
      unitCode: 'ISO-1',
      unitName: 'Isolation 1',
      isIsolation: true,
    });
    expect(list.units[0]!.cats[0]).toMatchObject({
      catName: 'Kitty',
      catIsHighRisk: true,
      handlingNote: 'Bites',
    });
    expect(list.units[0]!.tasks[0]).toMatchObject({
      assignedToName: 'Tanvir',
      isOverdue: true,
      minutesLate: null,
    });
  });

  it('turns notEating ids into per-cat flags', async () => {
    server.use(
      http.get(`${API}/staff/boarding/stays/s/observations`, () =>
        HttpResponse.json({
          stayId: 's',
          notEating: ['k'],
          items: [
            {
              id: 'o',
              catId: 'k',
              catName: 'Noodle',
              recordedOn: '2026-10-14T03:00:00Z',
              recordedBy: 'x',
              portionEaten: 'None',
              waterIntake: 'Normal',
              litterOutput: 'Normal',
              mood: 'Withdrawn',
              notes: null,
            },
          ],
        }),
      ),
    );
    const obs = await getStayObservations('s');
    expect(obs.cats).toEqual([{ catId: 'k', catName: 'Noodle', notEating: true }]);
  });
});

describe('occupancy (BoardingOccupancyViewModel) and the check-in body', () => {
  it('builds bars and maintenance bands from the per-day cells', async () => {
    server.use(
      http.get(`${API}/staff/boarding/occupancy`, () =>
        HttpResponse.json({
          from: '2026-10-12',
          to: '2026-10-15',
          units: [
            {
              unitId: 'u',
              code: 'F-1',
              name: 'Family 1',
              unitType: 'Family',
              block: 'G',
              isActive: true,
              cells: [
                { date: '2026-10-12', state: 'Free', stayId: null, label: null },
                { date: '2026-10-13', state: 'Occupied', stayId: 's', label: 'CheckedIn' },
                { date: '2026-10-14', state: 'Occupied', stayId: 's', label: 'CheckedIn' },
                { date: '2026-10-15', state: 'Maintenance', stayId: null, label: 'Repaint' },
              ],
            },
          ],
        }),
      ),
    );
    const board = await getBoardingOccupancy('2026-10-12', '2026-10-15');
    expect(board.days).toHaveLength(4);
    expect(board.units[0]!.stays).toEqual([
      expect.objectContaining({
        stayId: 's',
        fromOn: '2026-10-13',
        toOn: '2026-10-14',
        status: 'CheckedIn',
      }),
    ]);
    expect(board.units[0]!.maintenance[0]).toMatchObject({
      fromOn: '2026-10-15',
      reason: 'Repaint',
    });
  });

  it('sends `signature` and belongings carrying the uploaded `fileIds`', async () => {
    let body: { cats: Array<Record<string, unknown>> } | undefined;
    server.use(
      http.post(`${API}/staff/boarding/stays/s/check-in`, async ({ request }) => {
        body = (await request.json()) as typeof body;
        return HttpResponse.json({
          stay: { id: 's', status: 'CheckedIn', cats: [], deposit: {}, balanceDue: money('0.00') },
        });
      }),
    );
    await checkInStay('s', [
      {
        catId: 'k',
        identityConfirmed: true,
        vaccinationProofVerified: true,
        weightKg: 4,
        arrivalHealthObservation: 'ok',
        noBelongings: false,
        belongings: [{ kind: 'Carrier', description: 'Blue', fileIds: ['f1'] }],
        ownerSignature: { signerName: 'Sam', method: 'Typed', confirmedStatement: true },
      },
    ]);
    expect(body!.cats[0]).toMatchObject({
      signature: { signerName: 'Sam' },
      belongings: [{ kind: 'Carrier', fileIds: ['f1'] }],
    });
    expect(body!.cats[0]).not.toHaveProperty('ownerSignature');
  });
});
