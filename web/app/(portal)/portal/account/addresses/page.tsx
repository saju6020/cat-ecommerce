'use client';

import { useDeliveryAddresses } from '@/lib/hooks/useAccount';
import {
  useAddDeliveryAddress,
  useUpdateDeliveryAddress,
  useRemoveDeliveryAddress,
  useSetDefaultDeliveryAddress,
} from '@/lib/hooks/useAccountMutations';
import { AddressList } from '@/components/account/AddressList';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';

// US-ACC-06.
// ARCH gate HIGH 3 (first pass) — this page wires all four address mutations (one instance each,
// shared across every card); AddressList/AddressCard take the results as props.
export default function AddressesPage() {
  const { data: book, isLoading, isError, refetch } = useDeliveryAddresses();
  const addMutation = useAddDeliveryAddress();
  const updateMutation = useUpdateDeliveryAddress();
  const removeMutation = useRemoveDeliveryAddress();
  const setDefaultMutation = useSetDefaultDeliveryAddress();

  if (isLoading) return <LoadingState variant="card" rows={5} label="Loading your addresses…" />;
  if (isError || !book) {
    return (
      <ErrorState
        description="We couldn't load your addresses just now."
        onRetry={() => refetch()}
      />
    );
  }

  return (
    <AddressList
      book={book}
      onAdd={(values) => addMutation.mutateAsync(values)}
      onUpdate={(addressId, values) => updateMutation.mutateAsync({ addressId, request: values })}
      onRemove={(addressId) => removeMutation.mutateAsync(addressId)}
      onSetDefault={(addressId) => setDefaultMutation.mutate(addressId)}
      settingDefaultAddressId={
        setDefaultMutation.isPending ? (setDefaultMutation.variables ?? null) : null
      }
    />
  );
}
