'use client';

import { useMemo } from 'react';
import Link from 'next/link';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { buttonVariants } from '@/components/ui/button';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import { useHashTab } from '@/lib/hooks/useHashTab';
import { useSurgery } from '@/lib/hooks/useSurgeries';
import type { SurgeryDetail } from '@/lib/api/surgeries';
import { SurgeryHeader } from './SurgeryHeader';
import { SurgeryHistoryTab } from './SurgeryHistoryTab';
import { SurgeryTabs } from './SurgeryTabs';
import { surgeryPanels } from './surgeryPanels';

/** Which tab opens by default: it follows the status (HO-SRG-002 §8.1). */
export function defaultTabFor(status: string): string {
  switch (status) {
    case 'InTheatre':
      return 'anaesthesia';
    case 'Recovery':
      return 'recovery';
    case 'Discharged':
      return 'discharge';
    default:
      return 'pre-op';
  }
}

const TAB_IDS = ['pre-op', 'anaesthesia', 'report', 'recovery', 'discharge', 'history'] as const;

/**
 * ⭐ `SurgeryWorkspace` — `/staff/surgeries/[surgeryId]`, HO-SRG-002 §8.1/§11.3. The surgery header and
 * its tabs (Pre-op, Anaesthesia, Report, Recovery, Discharge, History), driven by the URL hash. The page
 * refetches every 30 s while the surgery is in pre-op, theatre or recovery (UI-5); a poll updates the
 * cache and never a form's own state.
 */
export function SurgeryWorkspace({ surgeryId }: { surgeryId: string }) {
  const surgery = useSurgery(surgeryId);
  if (surgery.error instanceof ApiForbiddenError)
    return <AccessRefusedState error={surgery.error} />;
  if (surgery.error instanceof ApiNotFoundError) {
    return (
      <ErrorState
        headingLevel={1}
        title="We couldn’t find that surgery."
        description="It may belong to another branch. Choose the branch at the top of the page, or go back to the day list."
      />
    );
  }
  if (surgery.isPending)
    return <LoadingState variant="card" rows={4} label="Loading the surgery…" />;
  if (!surgery.data) {
    return (
      <ErrorState
        headingLevel={1}
        title="We couldn’t load this surgery."
        onRetry={() => void surgery.refetch()}
      />
    );
  }
  return <Loaded surgery={surgery.data} />;
}

function Loaded({ surgery }: { surgery: SurgeryDetail }) {
  const fallback = defaultTabFor(surgery.status);
  const [tab, select] = useHashTab(TAB_IDS, fallback);
  const tabs = useMemo(
    () => [
      ...surgeryPanels(surgery),
      { id: 'history', label: 'History', panel: <SurgeryHistoryTab surgery={surgery} /> },
    ],
    [surgery],
  );
  return (
    <div className="flex flex-col gap-5">
      <div>
        <Link href="/staff/surgeries" className={buttonVariants({ variant: 'ghost', size: 'sm' })}>
          ← Theatre day list
        </Link>
      </div>
      <SurgeryHeader surgery={surgery} />
      <SurgeryTabs tabs={tabs} selected={tab} onSelect={select} />
    </div>
  );
}
