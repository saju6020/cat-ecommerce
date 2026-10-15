import { MyStaySummaryView } from '@/components/boarding/stay/StaySummaryView';

/** `/portal/boarding/[stayId]/summary` — HO-BRD-002 §11.3 (US-BRD-17): the check-out summary and charges (printable). */
export default async function PortalStaySummaryPage({
  params,
}: {
  params: Promise<{ stayId: string }>;
}) {
  const { stayId } = await params;
  return <MyStaySummaryView stayId={stayId} />;
}
