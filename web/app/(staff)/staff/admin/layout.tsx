'use client';

import { AdminNav } from '@/components/admin/AdminNav';
import { useStaffSession } from '@/lib/hooks/useSession';

/**
 * `app/(staff)/staff/admin/layout.tsx` — HO-ADM-002 §8.1, ADR-FE-014. A route-based section under
 * `(staff)/staff/admin/`, with its own nav rather than a modal stack — every admin resource is a
 * deep-linkable route, and each page loads only its own data.
 */
export default function AdminLayout({ children }: { children: React.ReactNode }) {
  const { data: session } = useStaffSession();

  return (
    <div className="flex flex-col gap-6">
      <AdminNav roleIds={session?.roleIds ?? []} />
      <div>{children}</div>
    </div>
  );
}
