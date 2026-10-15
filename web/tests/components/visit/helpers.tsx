import { beforeEach } from 'vitest';
import {
  emrFixtureIds,
  resetEmrMockData,
  setMockCallerRoles,
  setMockSignedUrlTtlSeconds,
} from '../../../mocks/handlers/visits';
import { renderWithClient } from '../booking/helpers';

export { emrFixtureIds as ids, renderWithClient, setMockCallerRoles, setMockSignedUrlTtlSeconds };

/** Every clinical component test starts from the seeded mock clinic, calling as a Vet. */
export function freshClinicBeforeEach(): void {
  beforeEach(() => {
    resetEmrMockData();
    setMockCallerRoles(null);
    setMockSignedUrlTtlSeconds(300);
  });
}
