import { Suspense } from 'react';
import { StayWorkspace } from '@/components/boarding/stay/StayWorkspace';

/** `/staff/boarding/stays/[stayId]` — HO-BRD-002 §11.8: the stay workspace (tabbed). */
export default async function BoardingStayPage({
  params,
}: {
  params: Promise<{ stayId: string }>;
}) {
  const { stayId } = await params;
  return (
    <Suspense fallback={null}>
      <StayWorkspace stayId={stayId} />
    </Suspense>
  );
}
