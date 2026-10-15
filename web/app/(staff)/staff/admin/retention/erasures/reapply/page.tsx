'use client';

import { ReapplyAfterRestoreForm } from '@/components/retention/ReapplyAfterRestoreForm';

/** `/staff/admin/retention/erasures/reapply` — US-XCT-06. A `403` shows on submit as the form's
 *  own error, because nothing is read before the person submits. */
export default function ReapplyPage() {
  return <ReapplyAfterRestoreForm />;
}
