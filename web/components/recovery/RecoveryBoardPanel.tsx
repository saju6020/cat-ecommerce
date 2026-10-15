'use client';

import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { ApiForbiddenError } from '@/lib/api/client';
import { useRecoveryBoard } from '@/lib/hooks/useSurgeries';
import { RecoveryBoard } from './RecoveryBoard';

/** The front desk's *Recovery* tab body (HO-SRG-002 §11.8): the board, polled every 30 s. */
export function RecoveryBoardPanel({ zone }: { zone: string }) {
  const board = useRecoveryBoard();
  if (board.error instanceof ApiForbiddenError) return <AccessRefusedState error={board.error} />;
  return (
    <RecoveryBoard
      board={board.data}
      zone={zone}
      isLoading={board.isPending}
      isError={board.isError}
      onRetry={() => void board.refetch()}
    />
  );
}
