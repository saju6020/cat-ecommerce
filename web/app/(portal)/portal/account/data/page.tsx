'use client';

import { PersonalDataExportCard } from '@/components/account/PersonalDataExportCard';
import { useExportPersonalData } from '@/lib/hooks/useAccountMutations';

// US-ACC-13.
// ARCH gate HIGH 3 (first pass) — this page wires useExportPersonalData(); PersonalDataExportCard
// itself takes the result as props.
export default function DataPage() {
  const mutation = useExportPersonalData();
  return (
    <PersonalDataExportCard
      onExport={() => mutation.mutateAsync()}
      isPending={mutation.isPending}
    />
  );
}
