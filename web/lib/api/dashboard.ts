import { apiFetch } from './client';
import type { ReportMoneyCell } from './reports';

/**
 * `lib/api/dashboard.ts` — HO-RPT-002 §8.2, US-RPT-05. Shape verified against
 * `StaffDashboardController` and `DashboardViewModel`. `GET /api/staff/dashboard` uses
 * `X-Branch-Id` (sent by `client.ts`). The tile list is exactly what the caller may see
 * (ADR-FE-137): a tile that is not sent is not rendered, and there is no placeholder for it.
 */
export type DashboardFigureType = 'Integer' | 'Decimal' | 'Money' | 'Percent' | 'Text';

/** A figure's value: a number (`Integer`), a money object, text, or `null` = not recorded. */
export type DashboardFigureValue = string | number | ReportMoneyCell | null;

export interface DashboardFigure {
  key: string;
  label: string;
  value: DashboardFigureValue;
  type: DashboardFigureType | (string & {});
  unit?: string | null;
}

export interface DashboardDrillDown {
  reportKey: string;
  from: string;
  to: string;
  parameters: Record<string, string>;
}

export interface DashboardTile {
  key: string;
  title: string;
  figures: DashboardFigure[];
  drillDown: DashboardDrillDown | null;
  note?: string | null;
}

export interface Dashboard {
  branchId: string;
  branchName: string;
  localDate: string;
  timeZoneId: string;
  generatedOn: string;
  refreshSeconds: number;
  tiles: DashboardTile[];
}

export function getDashboard(): Promise<Dashboard> {
  return apiFetch<Dashboard>('/api/staff/dashboard');
}
