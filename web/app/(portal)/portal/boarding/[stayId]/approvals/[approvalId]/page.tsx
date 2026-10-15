import { VetApprovalView } from '@/components/boarding/incidents/VetApprovalView';

/** `/portal/boarding/[stayId]/approvals/[approvalId]` — HO-BRD-002 §11.3 (US-BRD-14): approve or decline a vet cost. */
export default async function PortalApprovalPage({
  params,
}: {
  params: Promise<{ stayId: string; approvalId: string }>;
}) {
  const { stayId, approvalId } = await params;
  return <VetApprovalView stayId={stayId} approvalId={approvalId} />;
}
