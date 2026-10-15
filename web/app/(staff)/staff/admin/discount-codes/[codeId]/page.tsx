'use client';

import { use } from 'react';
import { EditCodeScreen } from '@/components/ordering-admin/EditScreens';

/** `/staff/admin/discount-codes/[codeId]` — HO-ORD-002 §11.12. */
export default function Page({ params }: { params: Promise<{ codeId: string }> }) {
  const { codeId } = use(params);
  return <EditCodeScreen codeId={codeId} />;
}
