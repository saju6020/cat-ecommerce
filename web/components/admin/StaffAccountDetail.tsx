'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { RoleAssignmentField } from './RoleAssignmentField';
import { EditStaffDetailsPanel } from './EditStaffDetailsPanel';
import { describeApiError } from '@/lib/api/errors';
import type {
  StaffAccount,
  StaffRoleCatalogue,
  UpdateStaffAccountDetailsRequest,
} from '@/lib/api/staff-admin';

/**
 * `components/admin/StaffAccountDetail.tsx` — US-ACC-07, UI-3. Change roles sends the WHOLE set
 * (`PUT .../roles`); removing a role confirms, naming it and noting the member will be signed
 * out. `LastAdministratorCannotBeRemoved` is shown as the server's own message.
 *
 * ARCH gate HIGH 3 (first pass) — takes `catalogue`/`onChangeRoles`/`isPending` as props; the page
 * (`app/(staff)/staff/admin/staff-accounts/[staffAccountId]/page.tsx`) wires
 * `useStaffRoleCatalogue()`/`useChangeStaffRoles(account.id)`.
 */
export interface StaffAccountDetailProps {
  account: StaffAccount;
  catalogue: { data: StaffRoleCatalogue | undefined; isLoading: boolean };
  onChangeRoles: (roleIds: string[]) => Promise<unknown>;
  isPending: boolean;
  /** Owner, 2026-10-20: the administrator edits name, mobile number and email (`PUT /api/staff-accounts/{id}`). */
  onUpdateDetails?: (request: UpdateStaffAccountDetailsRequest) => Promise<unknown>;
  isUpdatingDetails?: boolean;
}

export function StaffAccountDetail({
  account,
  catalogue,
  onChangeRoles,
  isPending,
  onUpdateDetails,
  isUpdatingDetails,
}: StaffAccountDetailProps) {
  const [editing, setEditing] = useState(false);
  const [saved, setSaved] = useState(false);
  const [selectedRoleIds, setSelectedRoleIds] = useState<string[]>(account.roleIds);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [roleError, setRoleError] = useState<string | null>(null);

  const removedRoles = account.roleIds.filter((role) => !selectedRoleIds.includes(role));
  const isDirty = selectedRoleIds.length !== account.roleIds.length || removedRoles.length > 0;

  async function save() {
    setError(null);
    setRoleError(null);
    if (selectedRoleIds.length === 0) {
      setRoleError('Select at least one role.');
      return;
    }
    try {
      await onChangeRoles(selectedRoleIds);
      setConfirmOpen(false);
    } catch (err) {
      setError(describeApiError(err));
    }
  }

  function handleSaveClick() {
    if (removedRoles.length > 0) setConfirmOpen(true);
    else void save();
  }

  return (
    <div className="flex max-w-lg flex-col gap-5">
      <div className="flex flex-col gap-3">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <h1 className="font-display text-xl font-semibold text-ink">{account.fullName}</h1>
          {onUpdateDetails && !editing && (
            <Button variant="secondary" size="sm" onClick={() => setEditing(true)}>
              Edit details
            </Button>
          )}
        </div>
        {/* Owner, 2026-10-20: both sign-in identifiers, labelled, so the administrator can share them. */}
        <div className="flex flex-col gap-1">
          <p id="signs-in-with" className="m-0 text-sm font-semibold text-ink">
            Signs in with
          </p>
          <dl
            aria-labelledby="signs-in-with"
            className="m-0 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm"
          >
            <dt className="text-ink-muted">Mobile number</dt>
            <dd className="m-0 break-all text-ink">{account.mobileNumber ?? 'Not set'}</dd>
            <dt className="text-ink-muted">Email</dt>
            <dd className="m-0 break-all text-ink">{account.emailAddress ?? 'Not set'}</dd>
          </dl>
        </div>
        <Badge variant={account.hasPassword ? 'success' : 'warning'} className="w-fit">
          Password {account.hasPassword ? 'set' : 'not set'}
        </Badge>
        {saved && !editing && (
          <p role="status" aria-live="polite" className="m-0 text-sm text-ink">
            Details saved.
          </p>
        )}
        {onUpdateDetails && editing && (
          <EditStaffDetailsPanel
            account={account}
            onSave={async (request) => {
              await onUpdateDetails(request);
              setSaved(true);
            }}
            onDone={() => setEditing(false)}
            isPending={isUpdatingDetails ?? false}
          />
        )}
      </div>

      {error && (
        <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
          {error}
        </p>
      )}

      <RoleAssignmentField
        roleIds={catalogue.data?.roleIds ?? []}
        isLoading={catalogue.isLoading}
        value={selectedRoleIds}
        onChange={setSelectedRoleIds}
        error={roleError ?? undefined}
      />

      <Button
        onClick={handleSaveClick}
        disabled={!isDirty || isPending}
        aria-busy={isPending}
        className="w-fit"
      >
        {isPending ? 'Saving…' : 'Save roles'}
      </Button>

      <ConfirmDialog
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        title="Remove role(s)?"
        consequence={`This removes ${removedRoles.join(', ')} from ${account.fullName}. They will be signed out and their permissions will change immediately.`}
        confirmLabel="Remove"
        variant="destructive"
        onConfirm={save}
      />
    </div>
  );
}
