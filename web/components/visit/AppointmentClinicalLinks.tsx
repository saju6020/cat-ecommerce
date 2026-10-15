'use client';

import Link from 'next/link';
import { buttonVariants } from '@/components/ui/button';
import { ProvidersGate } from './ProvidersGate';
import { useVisitForAppointment } from '@/lib/hooks/useVisit';
import { useStaffSession } from '@/lib/hooks/useSession';
import type { AppointmentStatus } from '@/lib/api/types';

/**
 * `AppointmentClinicalLinks` — HO-EMR-002 §11.9, the two links `StaffAppointmentDetail` gains: **Visit**
 * (only when a visit exists for the appointment, and only for someone who may read visits) and
 * **Record consent** (for procedures done under anaesthesia — spay, neuter and dental appointments —
 * that have not been cancelled or finished). The service category is the appointment's own snapshot.
 */
/** The server accepts a consent for any appointment (there is no room-kind field), so the link is
 *  offered whenever the appointment is checked in or in consultation, or has an OPEN visit. */
const CONSENT_STATUSES: AppointmentStatus[] = ['CheckedIn', 'InConsultation'];

export function AppointmentClinicalLinks(props: AppointmentClinicalLinksProps) {
  return (
    <ProvidersGate>
      <AppointmentClinicalLinksInner {...props} />
    </ProvidersGate>
  );
}

interface AppointmentClinicalLinksProps {
  appointmentId: string;
  catId: string;
  status: AppointmentStatus;
}

function AppointmentClinicalLinksInner({
  appointmentId,
  catId,
  status,
}: AppointmentClinicalLinksProps) {
  const roles = useStaffSession().data?.roleIds ?? [];
  const canReadVisits = roles.some((r) => r === 'Nurse' || r === 'Vet' || r === 'Administrator');
  const visit = useVisitForAppointment(appointmentId, canReadVisits);
  const showConsent = CONSENT_STATUSES.includes(status) || visit.data?.status === 'Open';
  const visitId = visit.data?.visitId ?? null;
  if (!visitId && !showConsent) return null;
  return (
    <div className="flex flex-wrap gap-3">
      {visitId && (
        <Link
          href={`/staff/visits/${visitId}`}
          className={buttonVariants({ variant: 'secondary' })}
        >
          Visit
        </Link>
      )}
      {showConsent && (
        <Link
          href={`/staff/consents/new?catId=${catId}&appointmentId=${appointmentId}&kind=Surgery`}
          className={buttonVariants({ variant: 'secondary' })}
        >
          Record consent
        </Link>
      )}
    </div>
  );
}
