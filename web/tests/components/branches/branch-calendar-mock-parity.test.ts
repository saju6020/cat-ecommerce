import { describe, expect, it } from 'vitest';
import { getBranchCalendar, getBranchClosures } from '@/lib/api/admin-branches';
import { administrationFixtureIds } from '@/mocks/handlers/admin';

const { mainBranchId, londonBranchId } = administrationFixtureIds;

/**
 * U-CAL14 — HO-ADM-002-FU1 §12.2/F11: the MSW calendar and closures handlers return what the real
 * API returns (instants in the branch zone, part-day subtraction, `200 []` for an unknown branch,
 * `400` over 62 days, overlap-filtered closures).
 */
describe('U-CAL14 — mock parity with the real calendar and closures endpoints', () => {
  it('main branch Monday 2027-02-15: the part-day closure leaves the morning only, as real instants', async () => {
    const result = await getBranchCalendar(mainBranchId, '2027-02-15', '2027-02-15');
    expect(result).toEqual([
      { start: '2027-02-15T03:00:00.000Z', end: '2027-02-15T07:00:00.000Z' },
    ]);
  });

  it('main branch 2027-01-01: the public holiday gives no intervals', async () => {
    expect(await getBranchCalendar(mainBranchId, '2027-01-01', '2027-01-01')).toEqual([]);
  });

  it('main branch Tuesday 2027-02-16: both intervals, Dhaka wall-clock converted to UTC', async () => {
    expect(await getBranchCalendar(mainBranchId, '2027-02-16', '2027-02-16')).toEqual([
      { start: '2027-02-16T03:00:00.000Z', end: '2027-02-16T07:00:00.000Z' },
      { start: '2027-02-16T08:00:00.000Z', end: '2027-02-16T12:00:00.000Z' },
    ]);
  });

  it('London Friday 2027-10-29 (BST) is 07:00Z-16:00Z', async () => {
    expect(await getBranchCalendar(londonBranchId, '2027-10-29', '2027-10-29')).toEqual([
      { start: '2027-10-29T07:00:00.000Z', end: '2027-10-29T16:00:00.000Z' },
    ]);
  });

  it('London Monday 2027-11-01 (GMT) is 08:00Z-17:00Z', async () => {
    expect(await getBranchCalendar(londonBranchId, '2027-11-01', '2027-11-01')).toEqual([
      { start: '2027-11-01T08:00:00.000Z', end: '2027-11-01T17:00:00.000Z' },
    ]);
  });

  it('a range of 63 days is a 400, 62 days is accepted', async () => {
    await expect(
      getBranchCalendar(mainBranchId, '2027-01-01', '2027-03-05'),
    ).rejects.toBeInstanceOf(Error);
    await expect(
      getBranchCalendar(mainBranchId, '2027-01-01', '2027-03-04'),
    ).resolves.toBeDefined();
  });

  it('an unknown branch id is 200 []', async () => {
    expect(
      await getBranchCalendar('00000000-0000-0000-0000-000000000000', '2027-02-01', '2027-02-28'),
    ).toEqual([]);
  });

  it('closures honour from/to: February holds only the half-day closure', async () => {
    const list = await getBranchClosures(mainBranchId, { from: '2027-02-01', to: '2027-02-28' });
    expect(list).toHaveLength(1);
    expect(list[0]).toMatchObject({
      startsOn: '2027-02-15',
      partialFrom: '13:00',
      partialTo: '18:00',
    });
    const all = await getBranchClosures(mainBranchId);
    expect(all.length).toBeGreaterThanOrEqual(2);
  });
});
