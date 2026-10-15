'use client';

import { useState } from 'react';
import Link from 'next/link';
import { Printer } from 'lucide-react';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { buttonVariants } from '@/components/ui/button';
import { Banner, Panel } from '@/components/inventory/fields';
import { formatAppointmentWhen } from '@/lib/format/appointment';
import { useStaffRoles } from '@/lib/hooks/useStaffRoles';
import { useDischargeDraft } from '@/lib/hooks/useSurgeries';
import type { SurgeryDetail } from '@/lib/api/surgeries';
import { DischargeForm } from './DischargeForm';
import { FollowUpList } from './FollowUpList';

/**
 * The Discharge tab (HO-SRG-002 §11.6). In recovery: the discharge form, filled from the protocol (nurses
 * and vets). After discharge: the success line, **Print post-op sheet**, the instructions as text and the
 * follow-ups. Before recovery there is nothing to discharge yet.
 */
export function DischargeTab({ surgery }: { surgery: SurgeryDetail }) {
  const roles = useStaffRoles();
  const [justDischarged, setJustDischarged] = useState(false);
  const canManage = roles.has('Nurse', 'Vet');
  const inRecovery = surgery.allowedTransitions.includes('Discharge');
  const draft = useDischargeDraft(surgery.id, inRecovery && canManage);

  if (surgery.status === 'Discharged') {
    return (
      <div className="flex max-w-3xl flex-col gap-4">
        {justDischarged && (
          <Banner tone="info" role="status">
            Discharged. The post-op instructions have been sent to {surgery.customerName} and are in
            their inbox.
          </Banner>
        )}
        <div className="flex flex-wrap items-center justify-between gap-2">
          <p className="text-sm text-ink-muted">
            Discharged
            {surgery.dischargedOn
              ? ` ${formatAppointmentWhen(surgery.dischargedOn, surgery.branchTimeZoneId)}`
              : ''}
            .
          </p>
          <Link
            href={`/staff/surgeries/${surgery.id}/post-op-sheet`}
            className={buttonVariants({ variant: 'secondary' })}
          >
            <Printer size={16} aria-hidden="true" />
            Print post-op sheet
          </Link>
        </div>
        <Panel title="Post-op instructions" headingId="srg-postop">
          <p className="whitespace-pre-wrap break-words text-sm text-ink">
            {surgery.postOpInstructions || 'None were recorded.'}
          </p>
        </Panel>
        <FollowUpList surgery={surgery} canClose={canManage} />
      </div>
    );
  }

  if (!inRecovery) {
    return <Banner tone="neutral">Discharge opens once the cat is in recovery.</Banner>;
  }
  if (!canManage) {
    return <Banner tone="neutral">Nurses and vets discharge the cat.</Banner>;
  }
  if (draft.isPending)
    return <LoadingState variant="card" rows={3} label="Loading the discharge form…" />;
  if (!draft.data) {
    return (
      <ErrorState
        title="We couldn’t load the discharge form."
        onRetry={() => void draft.refetch()}
      />
    );
  }
  return (
    <DischargeForm
      surgery={surgery}
      draft={draft.data}
      onDischarged={() => setJustDischarged(true)}
    />
  );
}
