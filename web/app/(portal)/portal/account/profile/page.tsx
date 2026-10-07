'use client';

import { useCustomerProfile } from '@/lib/hooks/useAccount';
import { useUpdateCustomerProfile } from '@/lib/hooks/useAccountMutations';
import { ProfileForm } from '@/components/account/ProfileForm';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import { ApiRateLimitedError } from '@/lib/api/client';

// US-ACC-06.
// ARCH gate HIGH 3 (first pass) — this page wires useUpdateCustomerProfile(); ProfileForm itself
// takes the result as props.
export default function ProfilePage() {
  const { data: profile, isLoading, isError, refetch } = useCustomerProfile();
  const mutation = useUpdateCustomerProfile();

  if (isLoading) return <LoadingState variant="card" rows={5} label="Loading your profile…" />;
  if (isError || !profile) {
    return (
      <ErrorState description="We couldn't load your profile just now." onRetry={() => refetch()} />
    );
  }

  return (
    <ProfileForm
      profile={profile}
      onSave={(request) => mutation.mutateAsync(request)}
      isPending={mutation.isPending}
      rateLimitError={mutation.error instanceof ApiRateLimitedError ? mutation.error : null}
    />
  );
}
