import type { TabSpec } from '@/components/billing/Tabs';
import { AnaesthesiaTab } from '@/components/anaesthesia/AnaesthesiaTab';
import { DischargeTab } from '@/components/recovery/DischargeTab';
import { RecoveryTab } from '@/components/recovery/RecoveryTab';
import { ReportTab } from '@/components/surgical-report/ReportTab';
import type { SurgeryDetail } from '@/lib/api/surgeries';
import { PreOpTab } from './PreOpTab';

/**
 * The surgery page's working tabs (everything but History), in order. Each panel is its own component and
 * gets the server's `SurgeryDetail`; none of them works out what is allowed — they read
 * `allowedTransitions`, `startBlockers` and the checklist from it (ADR-FE-072).
 */
export function surgeryPanels(surgery: SurgeryDetail): TabSpec[] {
  return [
    { id: 'pre-op', label: 'Pre-op', panel: <PreOpTab surgery={surgery} /> },
    { id: 'anaesthesia', label: 'Anaesthesia', panel: <AnaesthesiaTab surgery={surgery} /> },
    { id: 'report', label: 'Report', panel: <ReportTab surgery={surgery} /> },
    { id: 'recovery', label: 'Recovery', panel: <RecoveryTab surgery={surgery} /> },
    { id: 'discharge', label: 'Discharge', panel: <DischargeTab surgery={surgery} /> },
  ];
}
