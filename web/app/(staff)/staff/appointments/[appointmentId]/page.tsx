'use client';

import { use } from 'react';
import { StaffAppointmentView } from './StaffAppointmentView';

/** `/staff/appointments/[appointmentId]` — HO-APT-002 §11.4 (US-CLN-07, 10). */
export default function StaffAppointmentPage({
  params,
}: {
  params: Promise<{ appointmentId: string }>;
}) {
  const { appointmentId } = use(params);
  return <StaffAppointmentView appointmentId={appointmentId} />;
}
