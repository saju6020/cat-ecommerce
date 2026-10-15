import { IncidentDetailView } from '@/components/boarding/incidents/IncidentDetailView';

/** `/portal/boarding/[stayId]/incidents/[incidentId]` — HO-BRD-002 §11.3 (US-BRD-13): read it, acknowledge it. */
export default async function PortalIncidentPage({
  params,
}: {
  params: Promise<{ stayId: string; incidentId: string }>;
}) {
  const { stayId, incidentId } = await params;
  return <IncidentDetailView stayId={stayId} incidentId={incidentId} />;
}
