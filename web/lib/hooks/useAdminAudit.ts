import { useQuery } from '@tanstack/react-query';
import { searchAuditEntries, type AuditSearchParams } from '@/lib/api/admin-audit';
import { ApiForbiddenError } from '@/lib/api/client';

export function useAuditEntries(params: AuditSearchParams) {
  return useQuery({
    queryKey: ['admin', 'audit-entries', params],
    queryFn: () => searchAuditEntries(params),
    retry: (failureCount, error) => !(error instanceof ApiForbiddenError) && failureCount < 1,
  });
}
