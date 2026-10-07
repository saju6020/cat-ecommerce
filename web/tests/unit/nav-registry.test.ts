import { describe, expect, it } from 'vitest';
import { navRegistry } from '@/nav';

const EXPECTED_AREAS = [
  'accounts',
  'cat-profiles',
  'administration',
  'notifications',
  'appointments',
  'file-storage',
  'shop',
  'inventory',
  'consultation',
  'pharmacy',
  'surgery',
  'billing',
  'orders',
  'boarding',
  'subscriptions',
  'portal',
  'reporting',
  'cross-cutting',
];

describe('navRegistry', () => {
  it('pre-registers all 18 functional areas exactly once', () => {
    const keys = navRegistry.map((area) => area.key);
    expect(keys).toHaveLength(18);
    expect(new Set(keys).size).toBe(18);
    expect(keys.sort()).toEqual([...EXPECTED_AREAS].sort());
  });

  it("every area's registered items are well-formed (empty until that area ships its own screens)", () => {
    // Data-driven, not by area key — updated by the ARCHITECTURE gate on HO-CAT-002, which found
    // the previous version's `if (area.key === 'cat-profiles') continue` conflict-prone: every
    // later area running in a parallel worktree would have had to add its own skip to this same
    // shared file, which is exactly the merge conflict web/README.md "Adding a functional area"
    // exists to prevent. This asserts on shape only, so it never needs another area-specific edit.
    for (const area of navRegistry) {
      for (const item of area.items) {
        expect(item.label.length).toBeGreaterThan(0);
        expect(item.href.startsWith('/')).toBe(true);
      }
    }
  });

  it('at least one area has registered real screens', () => {
    const totalItems = navRegistry.reduce((sum, area) => sum + area.items.length, 0);
    expect(totalItems).toBeGreaterThan(0);
  });
});
