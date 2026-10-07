import { Suspense } from 'react';
import { LoadingState } from '@/components/common/LoadingState';
import { CalendarView } from './CalendarView';

/** `/staff/appointments` — HO-APT-002 §11.6 (US-CLN-07): the day/week calendar per vet or room. */
export default function AppointmentsCalendarPage() {
  return (
    <Suspense fallback={<LoadingState variant="card" rows={6} label="Loading the calendar…" />}>
      <CalendarView />
    </Suspense>
  );
}
