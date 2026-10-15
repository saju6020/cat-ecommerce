import { StayOverview } from '@/components/boarding/stay/StayOverview';

/** `/portal/boarding/[stayId]` — HO-BRD-002 §11.3 (US-BRD-06): the stay, its deposit and care profiles. */
export default async function PortalStayPage({ params }: { params: Promise<{ stayId: string }> }) {
  const { stayId } = await params;
  return <StayOverview stayId={stayId} />;
}
