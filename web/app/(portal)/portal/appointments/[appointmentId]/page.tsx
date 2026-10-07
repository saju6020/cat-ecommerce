'use client';

import { use } from 'react';
import { MyAppointmentView } from './MyAppointmentView';

/** `/portal/appointments/[appointmentId]` — HO-APT-002 §8.1/§11.2 (US-CLN-04, 09): detail,
 *  reschedule, cancel and claiming a waitlist offer. */
export default function MyAppointmentPage({
  params,
}: {
  params: Promise<{ appointmentId: string }>;
}) {
  const { appointmentId } = use(params);
  return <MyAppointmentView appointmentId={appointmentId} />;
}
