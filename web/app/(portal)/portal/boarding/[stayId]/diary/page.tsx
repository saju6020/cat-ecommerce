import { StayDiary } from '@/components/boarding/stay/StayDiary';

/** `/portal/boarding/[stayId]/diary` — HO-BRD-002 §11.3 (US-BRD-12): the only customer stay diary (README C-6). */
export default async function PortalStayDiaryPage({
  params,
}: {
  params: Promise<{ stayId: string }>;
}) {
  const { stayId } = await params;
  return <StayDiary stayId={stayId} />;
}
