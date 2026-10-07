'use client';

import { useState } from 'react';
import { useNotificationPreferences } from '@/lib/hooks/useAccount';
import { useSaveNotificationPreferences } from '@/lib/hooks/useSaveNotificationPreferences';
import { NotificationPreferenceMatrix } from '@/components/account/NotificationPreferenceMatrix';
import { PreferredChannelField } from '@/components/account/PreferredChannelField';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import { ApiRateLimitedError, type ApiValidationError } from '@/lib/api/client';
import { PREFERRED_CHANNEL_FIELD_MAP } from '@/lib/api/errors';

// US-ACC-06, US-NTF-01.
// ARCH gate HIGH 3 (first pass) — this page wires the mutation; NotificationPreferenceMatrix itself
// takes the result as props.
//
// HO-NTF-002 §11.2 (the one scoped edit to this file) — `PreferredChannelField` sits above the
// matrix inside the SAME explicit Save (ADR-FE-008): one `PUT` carries the cell changes and the
// preferred channel, and nothing is sent before Save.
export default function NotificationsPage() {
  const { data: preferences, isLoading, isError, refetch } = useNotificationPreferences();
  const mutation = useSaveNotificationPreferences();
  const [chosenChannel, setChosenChannel] = useState<string | null>(null);
  const [channelError, setChannelError] = useState<string | null>(null);

  if (isLoading) return <LoadingState variant="table" rows={4} label="Loading your preferences…" />;
  if (isError || !preferences) {
    return (
      <ErrorState
        description="We couldn't load your preferences just now."
        onRetry={() => refetch()}
      />
    );
  }

  const available = preferences.availablePreferredChannels;
  const effective = preferences.effectivePreferredChannel ?? preferences.preferredChannel ?? null;
  const channelValue = chosenChannel ?? effective;
  const channelDirty = chosenChannel !== null && chosenChannel !== effective;

  return (
    <NotificationPreferenceMatrix
      preferences={preferences}
      onSave={async (changes) => {
        setChannelError(null);
        await mutation.mutateAsync({
          changes,
          preferredChannel: channelDirty ? (chosenChannel ?? undefined) : undefined,
        });
        setChosenChannel(null);
      }}
      isPending={mutation.isPending}
      rateLimitError={mutation.error instanceof ApiRateLimitedError ? mutation.error : null}
      extraDirty={channelDirty}
      onServerError={(error: ApiValidationError) => {
        const own = error.response.errors.filter(
          (e) => e.propertyName in PREFERRED_CHANNEL_FIELD_MAP,
        );
        if (own.length === 0) return false;
        setChannelError(own.map((e) => e.errorMessage).join(' '));
        return own.length === error.response.errors.length;
      }}
      beforeTable={
        available ? (
          <PreferredChannelField
            value={channelValue}
            available={available}
            error={channelError}
            disabled={mutation.isPending}
            onChange={(channel) => {
              setChosenChannel(channel);
              setChannelError(null);
            }}
          />
        ) : null
      }
    />
  );
}
