import { describe, expect, it } from 'vitest';
import { getPublicBranches } from '@/lib/api/admin-branches';
import { getPublicServices } from '@/lib/api/admin-catalogue';
import { getClinicBranches } from '@/lib/api/booking';

/** A-APT1 (HO-ADM-002): the public types and the shared mock use the API's own field names. */
describe('public branch / service wire shapes', () => {
  it('GET /api/branches sends addressLine/phoneNumber, and getClinicBranches still maps them', async () => {
    const [raw] = await getPublicBranches();
    expect(raw).toHaveProperty('addressLine');
    expect(raw).toHaveProperty('phoneNumber');
    expect(raw).not.toHaveProperty('address');
    expect(raw).not.toHaveProperty('phone');

    const [clinic] = await getClinicBranches();
    expect(clinic!.address).toBe(raw!.addressLine);
    expect(clinic!.phone).toBe(raw!.phoneNumber);
  });

  it('GET /api/services carries a description', async () => {
    const services = await getPublicServices();
    expect(services.length).toBeGreaterThan(0);
    for (const service of services) expect(typeof service.description).toBe('string');
  });
});
