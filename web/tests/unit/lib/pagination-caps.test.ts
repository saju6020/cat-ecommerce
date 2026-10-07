import { describe, expect, it, beforeEach } from 'vitest';
import { apiFetch, ApiValidationError } from '@/lib/api/client';
import { getStaffAccounts } from '@/lib/api/staff-admin';
import { getDuplicateCustomerGroups } from '@/lib/api/staff-customers';
import { accountsFixtureIds, resetAccountsMockData } from '@/mocks/handlers/areas/accounts';

/**
 * Coordinator heads-up (2026-09-26): an integrator branch about to merge caps `pageSize` at 100 and
 * `pageNumber` at 1,000,000 on `GET /api/staff-accounts` and `GET /api/staff/customers/duplicates`
 * (400 otherwise). Two things to prove: `web/lib/api` never SENDS an oversized value itself (the
 * clamp in `getStaffAccounts`/`getDuplicateCustomerGroups`), and the mock returns the same 400 an
 * oversized value would get from the real API, for any caller that bypasses the clamp (a raw
 * `apiFetch` call, as a future caller elsewhere in this app might).
 */
async function signInAsAdmin(): Promise<void> {
  await apiFetch('/api/staff/auth/sign-in', {
    method: 'POST',
    body: {
      identifier: accountsFixtureIds.adminIdentifier,
      password: accountsFixtureIds.adminPassword,
    },
  });
}

beforeEach(async () => {
  resetAccountsMockData();
  await signInAsAdmin();
});

describe('pagination caps — GET /api/staff-accounts', () => {
  it('getStaffAccounts clamps an oversized pageSize/pageNumber before sending the request', async () => {
    const result = await getStaffAccounts({ pageSize: 99_999, pageNumber: 99_999_999 });
    // Clamped to 100/1_000_000 client-side -- if it had sent the raw values, the mock's own 400
    // below would have made this call throw instead of resolving.
    expect(result).toBeDefined();
  });

  it('the mock returns 400 for a raw pageSize over 100, bypassing the client-side clamp', async () => {
    const error = await apiFetch('/api/staff-accounts?pageSize=101').catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ApiValidationError);
    expect((error as ApiValidationError).response.errors[0]?.propertyName).toBe('PageSize');
  });

  it('the mock returns 400 for a raw pageNumber over 1,000,000, bypassing the client-side clamp', async () => {
    const error = await apiFetch('/api/staff-accounts?pageNumber=1000001').catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ApiValidationError);
    expect((error as ApiValidationError).response.errors[0]?.propertyName).toBe('PageNumber');
  });

  it('pageSize=100 and pageNumber=1000000 (the boundary itself) are still accepted', async () => {
    const result = await apiFetch('/api/staff-accounts?pageSize=100&pageNumber=1000000');
    expect(result).toBeDefined();
  });
});

describe('pagination caps — GET /api/staff/customers/duplicates', () => {
  it('getDuplicateCustomerGroups clamps an oversized pageSize/pageNumber before sending the request', async () => {
    const result = await getDuplicateCustomerGroups({ pageSize: 99_999, pageNumber: 99_999_999 });
    expect(result).toBeDefined();
  });

  it('the mock returns 400 for a raw pageSize over 100, bypassing the client-side clamp', async () => {
    const error = await apiFetch('/api/staff/customers/duplicates?pageSize=101').catch(
      (e: unknown) => e,
    );
    expect(error).toBeInstanceOf(ApiValidationError);
    expect((error as ApiValidationError).response.errors[0]?.propertyName).toBe('PageSize');
  });

  it('the mock returns 400 for a raw pageNumber over 1,000,000, bypassing the client-side clamp', async () => {
    const error = await apiFetch('/api/staff/customers/duplicates?pageNumber=1000001').catch(
      (e: unknown) => e,
    );
    expect(error).toBeInstanceOf(ApiValidationError);
    expect((error as ApiValidationError).response.errors[0]?.propertyName).toBe('PageNumber');
  });
});
