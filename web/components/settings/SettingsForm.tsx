'use client';

import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { describeApiError } from '@/lib/api/errors';
import {
  settingLabel,
  type SettingDefinitionView,
  type SettingChange,
} from '@/lib/api/admin-settings';

/**
 * `components/settings/SettingsForm.tsx` — HO-ADM-002 §11.5, US-ADM-05. Rendered **entirely**
 * from the `GET /api/admin/settings` payload — one section per `group`, never a hardcoded list
 * (U-E1). In branch scope, an overridable setting shows the global value beside its override with
 * "Use global value" to clear it (`DELETE .../settings/{key}`); a non-overridable setting is
 * read-only there, with a note. Save sends only changed keys; the confirm lists each as
 * `"{name}: {old} → {new}"` (UI-3).
 */
export interface SettingsFormProps {
  definitions: SettingDefinitionView[];
  scope: 'global' | { branchId: string };
  onSave: (changes: SettingChange[]) => Promise<unknown>;
  onClearOverride: (key: string) => Promise<unknown>;
  isPending: boolean;
}

function inputTypeFor(type: SettingDefinitionView['type']): string {
  if (type === 'Integer' || type === 'Decimal') return 'number';
  return 'text';
}

export function SettingsForm({
  definitions,
  scope,
  onSave,
  onClearOverride,
  isPending,
}: SettingsFormProps) {
  const isBranchScope = scope !== 'global';
  const [drafts, setDrafts] = useState<Record<string, string>>({});
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [clearError, setClearError] = useState<string | null>(null);

  useEffect(() => {
    setDrafts(Object.fromEntries(definitions.map((d) => [d.key, d.effectiveValue])));
    setSaved(false);
  }, [definitions]);

  const changedKeys = definitions.filter(
    (d) => drafts[d.key] !== undefined && drafts[d.key] !== d.effectiveValue,
  );
  const isDirty = changedKeys.length > 0;

  const groups = Array.from(new Set(definitions.map((d) => d.group)));

  function setDraft(key: string, value: string) {
    setDrafts((current) => ({ ...current, [key]: value }));
    setSaved(false);
  }

  async function handleConfirmSave() {
    setError(null);
    try {
      await onSave(
        changedKeys.map((d) => ({ key: d.key, value: drafts[d.key] ?? d.effectiveValue })),
      );
      setConfirmOpen(false);
      setSaved(true);
    } catch (err) {
      setError(describeApiError(err));
    }
  }

  async function handleUseGlobal(key: string) {
    setClearError(null);
    try {
      await onClearOverride(key);
    } catch (err) {
      setClearError(describeApiError(err));
    }
  }

  return (
    <div className="flex flex-col gap-8">
      <UnsavedChangesGuard when={isDirty && !saved} />

      {error && (
        <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
          {error}
        </p>
      )}
      {clearError && (
        <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
          {clearError}
        </p>
      )}
      {saved && (
        <p role="status" aria-live="polite" className="text-sm font-semibold text-ink">
          Settings saved.
        </p>
      )}

      {groups.map((group) => (
        <section key={group} className="flex flex-col gap-4">
          <h2 className="font-display text-lg font-semibold text-ink">{group}</h2>
          {definitions
            .filter((d) => d.group === group)
            .map((definition) => {
              const readOnlyInBranchScope = isBranchScope && !definition.branchOverridable;
              const hasOverride = isBranchScope && definition.branchValue !== null;
              const draft = drafts[definition.key] ?? definition.effectiveValue;
              const inputId = `setting-${definition.key}`;

              return (
                <div
                  key={definition.key}
                  className="flex flex-col gap-1.5 border-b border-border pb-4"
                >
                  <label htmlFor={inputId} className="text-sm font-semibold text-ink">
                    {settingLabel(definition)}
                    {definition.unit ? ` (${definition.unit})` : ''}
                  </label>
                  <p className="text-xs text-ink-muted">Default: {definition.default}</p>

                  {readOnlyInBranchScope ? (
                    <>
                      <input
                        id={inputId}
                        readOnly
                        value={definition.effectiveValue}
                        className="w-64 rounded-[var(--radius-sm)] border border-border bg-surface-sunken px-3 py-2 text-sm text-ink-muted"
                      />
                      <p className="text-xs text-ink-muted">
                        This setting cannot be overridden per branch.
                      </p>
                    </>
                  ) : definition.type === 'Boolean' ? (
                    <label className="flex items-center gap-2 text-sm text-ink">
                      <input
                        id={inputId}
                        type="checkbox"
                        checked={draft === 'true'}
                        onChange={(e) =>
                          setDraft(definition.key, e.target.checked ? 'true' : 'false')
                        }
                      />
                      Enabled
                    </label>
                  ) : (
                    <input
                      id={inputId}
                      type={inputTypeFor(definition.type)}
                      min={definition.min ?? undefined}
                      max={definition.max ?? undefined}
                      value={draft}
                      onChange={(e) => setDraft(definition.key, e.target.value)}
                      className="w-64 rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
                    />
                  )}

                  {isBranchScope && definition.branchOverridable && (
                    <div className="flex items-center gap-3 text-xs text-ink-muted">
                      <span>Global value: {definition.globalValue}</span>
                      {hasOverride && (
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => handleUseGlobal(definition.key)}
                        >
                          Use global value
                        </Button>
                      )}
                    </div>
                  )}
                </div>
              );
            })}
        </section>
      ))}

      <Button
        type="button"
        onClick={() => setConfirmOpen(true)}
        disabled={!isDirty || isPending}
        className="w-fit"
      >
        Save changes
      </Button>

      <ConfirmDialog
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        title="Save these changes?"
        consequence={
          <ul className="list-disc pl-5">
            {changedKeys.map((d) => (
              <li key={d.key}>
                {settingLabel(d)}: {d.effectiveValue} → {drafts[d.key]}
              </li>
            ))}
          </ul>
        }
        confirmLabel="Save changes"
        onConfirm={handleConfirmSave}
      />
    </div>
  );
}
