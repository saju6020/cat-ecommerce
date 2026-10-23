import { describe, expect, it } from 'vitest';
import { activeStaffHref, groupStaffNav } from '@/components/common/staff-nav-groups';
import { navRegistry } from '@/nav';
import type { NavArea } from '@/nav/types';

// 2026-10-16 (owner: "make the staff left menu professional"): the shell regroups the per-area
// registry by daily job and marks the current page.
const staffAreas: NavArea[] = navRegistry.map((area) => ({
  ...area,
  items: area.items.filter((item) => (item.audience ?? area.audience) !== 'portal'),
}));

describe('groupStaffNav', () => {
  const groups = groupStaffNav(staffAreas);

  it('puts Home first, with no heading, then the job groups in working order', () => {
    expect(groups[0]).toMatchObject({ label: null, items: [{ href: '/staff', label: 'Home' }] });
    expect(groups.map((g) => g.label)).toEqual([
      null,
      'Front desk',
      'Clinic',
      'Pharmacy',
      'Boarding',
      'Shop & orders',
      'Stock',
      'Money & reports',
      'Team & settings',
    ]);
  });

  it('keeps every staff link exactly once (none lost, none doubled)', () => {
    const shown = groups.flatMap((g) => g.items.map((i) => i.href)).sort();
    const registered = staffAreas.flatMap((a) => a.items.map((i) => i.href)).sort();
    expect(shown).toEqual(registered);
  });

  it('gives every link an icon', () => {
    expect(groups.flatMap((g) => g.items).filter((i) => !i.icon)).toEqual([]);
  });

  it('puts a link no group lists under "More", so a new area still shows', () => {
    const extra: NavArea = {
      key: 'subscriptions',
      label: 'Subscriptions',
      audience: 'staff',
      items: [{ label: 'Plans', href: '/staff/plans', icon: 'receipt' }],
    };
    const withExtra = groupStaffNav([...staffAreas, extra]);
    expect(withExtra.at(-1)).toMatchObject({ label: 'More', items: [{ href: '/staff/plans' }] });
  });

  it('drops a group whose links the member of staff cannot see', () => {
    const onlyFrontDesk = groupStaffNav([
      {
        key: 'appointments',
        label: 'Appointments',
        audience: 'staff',
        items: [{ label: 'Front desk', href: '/staff/front-desk' }],
      },
    ]);
    expect(onlyFrontDesk.map((g) => g.label)).toEqual(['Front desk']);
  });
});

describe('activeStaffHref', () => {
  const groups = groupStaffNav(staffAreas);
  const cases: Array<[string, string | null]> = [
    ['/staff', '/staff'],
    ['/staff/orders', '/staff/orders'],
    ['/staff/orders/123', '/staff/orders'],
    ['/staff/boarding', '/staff/boarding'],
    ['/staff/boarding/tasks', '/staff/boarding/tasks'],
    ['/staff/pharmacy/controlled-drugs/register', '/staff/pharmacy/controlled-drugs'],
    ['/staff/shop/products/abc', '/staff/shop/products'],
    ['/staff/procurement/purchase-orders', '/staff/procurement/suppliers'],
    ['/staff/admin/settings', '/staff/admin/branches'],
    ['/staff/admin/staff-accounts/42', '/staff/admin/staff-accounts'],
    ['/staff/admin/boarding/units', '/staff/admin/boarding/units'],
    ['/staff/something-new', null],
  ];
  it.each(cases)('%s marks %s', (pathname, expected) => {
    expect(activeStaffHref(groups, pathname)).toBe(expected);
  });
});
