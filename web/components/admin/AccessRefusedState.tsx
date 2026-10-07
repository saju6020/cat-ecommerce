import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { BranchAssignmentBanner } from '@/components/branches/BranchAssignmentBanner';
import { getBranchAssignmentErrorCode } from '@/lib/api/errors';

/**
 * `components/admin/AccessRefusedState.tsx` — HO-ADM-002 §11.9 (ADM backlog 34, 41). What a page
 * shows for a `403`: the "ask to be assigned / pick a branch" banner when the refusal is
 * `BranchNotAssigned`/`BranchAssignmentRequired` (the caller holds the action, they only lack a
 * branch), otherwise the plain `PermissionDeniedState`. Pass the `ApiForbiddenError` itself.
 */
export function AccessRefusedState({ error }: { error: unknown }) {
  const code = getBranchAssignmentErrorCode(error);
  return code ? <BranchAssignmentBanner code={code} /> : <PermissionDeniedState />;
}
