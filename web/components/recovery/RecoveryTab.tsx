'use client';

import { Banner } from '@/components/inventory/fields';
import { useStaffRoles } from '@/lib/hooks/useStaffRoles';
import type { SurgeryDetail } from '@/lib/api/surgeries';
import { RecoveryStatusControl } from './RecoveryStatusControl';

/** The Recovery tab (HO-SRG-002 §11.6): the status control and its history. */
export function RecoveryTab({ surgery }: { surgery: SurgeryDetail }) {
  const roles = useStaffRoles();
  const reached = ['Recovery', 'Discharged'].includes(surgery.status);
  if (!reached) {
    return <Banner tone="neutral">Recovery starts once the procedure has ended.</Banner>;
  }
  return <RecoveryStatusControl surgery={surgery} canSet={roles.has('Nurse', 'Vet')} />;
}
