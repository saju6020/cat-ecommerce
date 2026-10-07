'use client';

import { useState } from 'react';
import {
  useSettings,
  useChangeSettings,
  useClearSettingOverride,
} from '@/lib/hooks/useAdminSettings';
import { useAdminBranches } from '@/lib/hooks/useAdminBranches';
import { SettingsForm } from '@/components/settings/SettingsForm';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import { ApiForbiddenError } from '@/lib/api/client';

/** `/staff/admin/settings` — US-ADM-05. Scope switcher: global or a branch. */
export default function SettingsPage() {
  const [branchId, setBranchId] = useState<string>('');
  const branches = useAdminBranches();
  const { data, isLoading, error, refetch } = useSettings(branchId || undefined);
  const changeMutation = useChangeSettings(branchId || undefined);
  const clearMutation = useClearSettingOverride(branchId);

  if (error instanceof ApiForbiddenError) return <AccessRefusedState error={error} />;

  return (
    <div className="flex flex-col gap-6">
      <h1 className="font-display text-xl font-semibold text-ink">Settings</h1>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="settings-scope" className="text-sm font-semibold text-ink">
          Scope
        </label>
        <select
          id="settings-scope"
          value={branchId}
          onChange={(e) => setBranchId(e.target.value)}
          className="w-fit rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
        >
          <option value="">Global</option>
          {(branches.data ?? []).map((b) => (
            <option key={b.id} value={b.id}>
              {b.name}
            </option>
          ))}
        </select>
      </div>

      {isLoading ? (
        <LoadingState rows={6} />
      ) : !data ? (
        <ErrorState
          description="We couldn't load the settings just now."
          onRetry={() => void refetch()}
        />
      ) : (
        <SettingsForm
          definitions={data}
          scope={branchId ? { branchId } : 'global'}
          isPending={changeMutation.isPending || clearMutation.isPending}
          onSave={(changes) => changeMutation.mutateAsync(changes)}
          onClearOverride={(key) => clearMutation.mutateAsync(key)}
        />
      )}
    </div>
  );
}
