import { IncidentQueue } from '@/components/boarding/incidents/IncidentQueue';

/** `/staff/boarding/incidents` — HO-BRD-002 §11.9 (US-BRD-13): the incident queue and Call owner. */
export default function BoardingIncidentsPage() {
  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-2xl font-semibold text-ink">Incidents</h1>
      <IncidentQueue />
    </div>
  );
}
