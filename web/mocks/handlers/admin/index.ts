import type { HttpHandler } from 'msw';
import { adminBranchesHandlers, resetAdminBranchesMockData } from './branches';
import { adminRostersHandlers, resetAdminRostersMockData } from './rosters';
import { adminLeaveHandlers, resetAdminLeaveMockData } from './leave';
import { adminCatalogueHandlers, resetAdminCatalogueMockData } from './catalogue';
import { adminSettingsHandlers, resetAdminSettingsMockData } from './settings';
import { adminAuditHandlers, resetAdminAuditMockData } from './audit';
import { adminContentHandlers, resetAdminContentMockData } from './content';
import { adminSystemHandlers, resetAdminSystemMockData } from './system';

export { administrationFixtureIds } from './fixtures';
export {
  resetAdminBranchesMockData,
  resetAdminRostersMockData,
  resetAdminLeaveMockData,
  resetAdminCatalogueMockData,
  resetAdminSettingsMockData,
  resetAdminAuditMockData,
  resetAdminContentMockData,
  resetAdminSystemMockData,
};

/** `mocks/handlers/admin/index.ts` — HO-ADM-002 §12/§13. Every admin MSW handler, combined. */
export const administrationHandlers: HttpHandler[] = [
  ...adminBranchesHandlers,
  ...adminRostersHandlers,
  ...adminLeaveHandlers,
  ...adminCatalogueHandlers,
  ...adminSettingsHandlers,
  ...adminAuditHandlers,
  ...adminContentHandlers,
  ...adminSystemHandlers,
];

/** Resets every admin resource's in-memory mock state in one call — used by component tests and
 *  e2e specs the same way `resetAccountsMockData()` is used for the accounts area. */
export function resetAdministrationMockData(): void {
  resetAdminBranchesMockData();
  resetAdminRostersMockData();
  resetAdminLeaveMockData();
  resetAdminCatalogueMockData();
  resetAdminSettingsMockData();
  resetAdminAuditMockData();
  resetAdminContentMockData();
  resetAdminSystemMockData();
}
