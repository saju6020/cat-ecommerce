import { describe, expect, it } from 'vitest';
import { resetAdminContentMockData } from '@/mocks/handlers/admin/content';

/** ACCEPTANCE gate F-H3 — the shared seeded customer has no pending acceptance by default, so the
 *  portal prompt does not open in other areas' e2e; publishing a new required version makes it
 *  pending again (what E-F2 relies on). */
const base = 'http://localhost/api';

describe('policy-acceptances/pending fixture', () => {
  it('is empty for the seeded customer by default, and lists a newly required version', async () => {
    resetAdminContentMockData();
    const pending = () =>
      fetch(`${base}/customers/me/policy-acceptances/pending`).then((r) => r.json());
    expect(await pending()).toEqual([]);

    const created = await fetch(`${base}/admin/policies/terms/versions`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ body: 'v3', changeSummary: 'Update.' }),
    });
    expect(created.ok).toBe(true);
    expect(await pending()).toEqual([
      expect.objectContaining({ policyKey: 'terms', title: 'Terms of service', versionNumber: 3 }),
    ]);
    resetAdminContentMockData();
  });
});
