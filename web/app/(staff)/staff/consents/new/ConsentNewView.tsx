'use client';

import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { ErrorState } from '@/components/common/ErrorState';
import { buttonVariants } from '@/components/ui/button';
import { toast } from '@/components/ui/use-toast';
import { ConsentCaptureForm } from '@/components/consent/ConsentCaptureForm';
import { ApiForbiddenError } from '@/lib/api/client';
import { useRecordConsent } from '@/lib/hooks/useConsents';
import { useStaffAppointment, useCalendar } from '@/lib/hooks/useStaffAppointments';
import { useStaffCustomer } from '@/lib/hooks/useStaffCustomers';
import { todayInZone } from '@/lib/format/time';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { isGuid } from '@/lib/validation/guid';
import type { ConsentKind } from '@/lib/api/consents';

const KINDS: ConsentKind[] = ['Surgery', 'Anaesthesia', 'Euthanasia'];

/**
 * Container for consent capture (HO-EMR-002 §11.6). The cat and appointment come from the query string
 * (the appointment detail and the cat page link here); the owner's name is read from the appointment's
 * customer to pre-fill the signer; the witness list is the clinic's on-shift staff (the calendar's
 * performer resources — a receptionist cannot list staff accounts). Works on a tablet in portrait.
 */
export function ConsentNewView() {
  const router = useRouter();
  const search = useSearchParams();
  const catId = search.get('catId') ?? '';
  const appointmentId = search.get('appointmentId') ?? '';
  const kindParam = search.get('kind') as ConsentKind | null;
  const kind: ConsentKind = kindParam && KINDS.includes(kindParam) ? kindParam : 'Surgery';
  const valid = isGuid(catId) && isGuid(appointmentId);

  const zone = useBranchTimeZone();
  const appointment = useStaffAppointment(valid ? appointmentId : '');
  const customer = useStaffCustomer(appointment.data?.customerId ?? '');
  const performers = useCalendar(
    { from: todayInZone(zone), to: todayInZone(zone), view: 'performer' },
    { enabled: valid, poll: false },
  );
  const record = useRecordConsent();

  if (!valid) {
    return (
      <ErrorState
        headingLevel={1}
        title="Open consent capture from an appointment."
        description="Consent is recorded against a cat and the appointment for its procedure. Find the appointment and choose Record consent."
      />
    );
  }
  if (appointment.error instanceof ApiForbiddenError) return <PermissionDeniedState />;

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="font-display text-2xl font-semibold text-ink">Record consent</h1>
        <Link
          href={`/staff/appointments/${appointmentId}`}
          className={buttonVariants({ variant: 'ghost', size: 'sm' })}
        >
          Back to the appointment
        </Link>
      </div>
      <ConsentCaptureForm
        catId={catId}
        appointmentId={appointmentId}
        kind={kind}
        onKindChange={(next) =>
          router.replace(
            `/staff/consents/new?catId=${catId}&appointmentId=${appointmentId}&kind=${next}`,
          )
        }
        ownerName={customer.data?.fullName ?? ''}
        ownerCustomerId={appointment.data?.customerId ?? null}
        witnesses={(performers.data?.resources ?? []).map((r) => ({ id: r.id, name: r.name }))}
        onSubmit={async (request) => {
          const result = await record.mutateAsync(request);
          toast({ title: 'Consent recorded' });
          router.push(`/staff/consents/${result.consentId}`);
        }}
      />
    </div>
  );
}
