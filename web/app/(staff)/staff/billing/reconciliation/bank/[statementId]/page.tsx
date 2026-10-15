'use client';

import { use } from 'react';
import { BankMatchView } from '@/components/billing/BankMatchView';

/** /staff/billing/reconciliation/bank/[statementId] - HO-BIL-002 section 11.8: the match view. */
export default function BankStatementPage({ params }: { params: Promise<{ statementId: string }> }) {
  const { statementId } = use(params);
  return <BankMatchView statementId={statementId} />;
}
