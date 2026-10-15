'use client';

import * as React from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  useDialogReturnFocus,
} from '@/components/ui/dialog';
import { ActionDialog } from './ReasonDialog';
import { Panel, SelectField, TextField } from './fields';
import { StackedTable } from './StackedTable';
import { LOCATION_KIND_LABELS, movementLabel } from './labels';
import { describeInventoryError, inventoryViolations, toFieldName } from '@/lib/api/errors';
import type { CreateLocationRequest, StockLocation, StockLocationKind } from '@/lib/api/inventory-lookup';

type Editing = { mode: 'create' } | { mode: 'rename'; location: StockLocation };

function LocationDialog({
  editing,
  onClose,
  onCreate,
  onRename,
}: {
  editing: Editing;
  onClose: () => void;
  onCreate: (request: CreateLocationRequest) => Promise<unknown>;
  onRename: (id: string, name: string) => Promise<unknown>;
}) {
  const creating = editing.mode === 'create';
  const [code, setCode] = React.useState('');
  const [name, setName] = React.useState(editing.mode === 'rename' ? editing.location.name : '');
  const [kind, setKind] = React.useState<StockLocationKind>('Warehouse');
  const [errors, setErrors] = React.useState<Record<string, string>>({});
  const [general, setGeneral] = React.useState<string | null>(null);
  const [pending, setPending] = React.useState(false);
  const returnFocusRef = useDialogReturnFocus(true);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    const local: Record<string, string> = {};
    if (creating && code.trim() === '') local.code = 'Enter a short code, such as WH1.';
    if (name.trim() === '') local.name = 'Enter a name.';
    setErrors(local);
    if (Object.keys(local).length > 0) return;
    setPending(true);
    setGeneral(null);
    try {
      if (editing.mode === 'create') await onCreate({ code: code.trim().toUpperCase(), name: name.trim(), kind });
      else await onRename(editing.location.id, name.trim());
      onClose();
    } catch (caught) {
      const next: Record<string, string> = {};
      let other: string | null = null;
      for (const v of inventoryViolations(caught)) {
        const field = toFieldName(v.propertyName);
        if (field === 'code' || field === 'name' || field === 'kind') next[field] ??= v.message;
        else other = v.message;
      }
      setErrors(next);
      setGeneral(Object.keys(next).length === 0 ? (other ?? describeInventoryError(caught)) : other);
    } finally {
      setPending(false);
    }
  }

  return (
    <Dialog open onOpenChange={(open) => !open && !pending && onClose()}>
      <DialogContent hideClose returnFocusRef={returnFocusRef}>
        <form onSubmit={submit} noValidate className="flex flex-col gap-3">
          <DialogHeader>
            <DialogTitle>{creating ? 'New location' : `Rename ${editing.location.code}`}</DialogTitle>
            <DialogDescription>
              {creating ? 'A location is a place stock is held. Its kind decides which movements it takes.' : 'The code and kind cannot be changed.'}
            </DialogDescription>
          </DialogHeader>
          {creating && <TextField id="location-code" label="Code" required maxLength={20} value={code} error={errors.code} onChange={(e) => setCode(e.target.value)} />}
          <TextField id="location-name" label="Name" required value={name} error={errors.name} onChange={(e) => setName(e.target.value)} />
          {creating && (
            <SelectField id="location-kind" label="Kind" required value={kind} error={errors.kind} onChange={(e) => setKind(e.target.value as StockLocationKind)}>
              {Object.entries(LOCATION_KIND_LABELS).map(([value, label]) => (<option key={value} value={value}>{label}</option>))}
            </SelectField>
          )}
          {general && <p role="alert" className="text-sm font-semibold text-danger-ink">{general}</p>}
          <DialogFooter>
            <Button variant="secondary" onClick={onClose} disabled={pending}>Cancel</Button>
            <Button type="submit" disabled={pending} aria-busy={pending}>{pending ? 'Saving…' : creating ? 'Create location' : 'Save name'}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

/**
 * `components/inventory/LocationManager.tsx` — HO-IMP-002 §11.8, US-IMP-06. The branch's stock
 * locations: code, name, kind, active, and what each kind takes, as text. Create / rename / deactivate
 * for the administrator (the controls are presentation; the server decides). Deactivating a location
 * that still holds stock is refused in the server's words (`StockLocationHasStock`), inside the confirm.
 */
export function LocationManager({
  locations,
  canManage,
  onCreate,
  onRename,
  onSetActive,
}: {
  locations: StockLocation[];
  canManage: boolean;
  onCreate: (request: CreateLocationRequest) => Promise<unknown>;
  onRename: (id: string, name: string) => Promise<unknown>;
  onSetActive: (id: string, isActive: boolean) => Promise<unknown>;
}) {
  const [editing, setEditing] = React.useState<Editing | null>(null);
  const [deactivating, setDeactivating] = React.useState<StockLocation | null>(null);

  return (
    <Panel title="Locations" actions={canManage ? <Button onClick={() => setEditing({ mode: 'create' })}>New location</Button> : undefined}>
      <StackedTable
        caption="Stock locations"
        rows={locations}
        getKey={(l) => l.id}
        columns={[
          { id: 'name', header: 'Location', rowHeader: true, cell: (l: StockLocation) => `${l.name} (${l.code})` },
          { id: 'kind', header: 'Kind', cell: (l) => LOCATION_KIND_LABELS[l.kind] ?? l.kind },
          { id: 'status', header: 'Status', cell: (l) => <Badge variant={l.isActive ? 'success' : 'neutral'}>{l.isActive ? 'Active' : 'Inactive'}</Badge> },
          { id: 'takes', header: 'Takes', cell: (l) => l.permittedMovements.map(movementLabel).join(', ') },
          ...(canManage
            ? [
                {
                  id: 'actions',
                  header: 'Actions',
                  cell: (l: StockLocation) => (
                    <span className="flex flex-wrap gap-2">
                      <Button size="sm" variant="secondary" onClick={() => setEditing({ mode: 'rename', location: l })} aria-label={`Rename ${l.name}`}>Rename</Button>
                      <Button size="sm" variant="secondary" onClick={() => (l.isActive ? setDeactivating(l) : void onSetActive(l.id, true))} aria-label={`${l.isActive ? 'Deactivate' : 'Reactivate'} ${l.name}`}>
                        {l.isActive ? 'Deactivate' : 'Reactivate'}
                      </Button>
                    </span>
                  ),
                },
              ]
            : []),
        ]}
      />
      {editing && <LocationDialog editing={editing} onClose={() => setEditing(null)} onCreate={onCreate} onRename={onRename} />}
      {deactivating && (
        <ActionDialog
          open
          onOpenChange={(open) => !open && setDeactivating(null)}
          title={`Deactivate ${deactivating.name}`}
          consequence="No stock can be received, moved or counted into this location while it is inactive. A location that still holds stock cannot be deactivated."
          confirmLabel="Deactivate location"
          variant="destructive"
          onConfirm={() => onSetActive(deactivating.id, false)}
        />
      )}
    </Panel>
  );
}
