'use client';

import { LoadingState } from '@/components/common/LoadingState';

/**
 * `components/admin/RoleAssignmentField.tsx` — ⭐ US-ACC-07, HO-ACC-002 §11.10, ADR-FE-009. The
 * role catalogue comes from `GET /api/staff-roles`, never a transcribed copy of SRS Appendix D.
 * At least one role is required.
 */
export interface RoleAssignmentFieldProps {
  roleIds: string[];
  isLoading: boolean;
  value: string[];
  onChange: (roleIds: string[]) => void;
  error?: string;
}

export function RoleAssignmentField({
  roleIds,
  isLoading,
  value,
  onChange,
  error,
}: RoleAssignmentFieldProps) {
  function toggle(roleId: string) {
    onChange(value.includes(roleId) ? value.filter((r) => r !== roleId) : [...value, roleId]);
  }

  return (
    <fieldset className="flex flex-col gap-2">
      <legend className="text-sm font-semibold text-ink">
        Roles{' '}
        <span aria-hidden="true" className="text-danger-ink">
          *
        </span>
      </legend>
      {isLoading ? (
        <LoadingState rows={4} variant="table" label="Loading roles…" />
      ) : (
        <div className="flex flex-col gap-1.5">
          {roleIds.map((roleId) => (
            <label key={roleId} className="flex items-center gap-2 text-sm text-ink">
              <input
                type="checkbox"
                checked={value.includes(roleId)}
                onChange={() => toggle(roleId)}
              />
              {roleId}
            </label>
          ))}
        </div>
      )}
      {error && (
        <p role="alert" className="text-xs font-semibold text-danger-ink">
          {error}
        </p>
      )}
    </fieldset>
  );
}
