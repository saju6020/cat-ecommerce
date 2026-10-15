'use client';

import * as React from 'react';
import { Badge } from '@/components/ui/badge';
import { ShieldAlert } from 'lucide-react';
import { useFormularyList } from '@/lib/hooks/useFormulary';
import type { FormularyMedicineSummary } from '@/lib/api/formulary';
import { formLabel } from '@/lib/format/pharmacy';
import { ListboxPicker } from './ListboxPicker';

/**
 * `FormularyPicker` — HO-PHM-002 §11.2. Search the ACTIVE formulary by name or code. Each option shows
 * name, strength (the server's text, unit included), form, a *Controlled* badge (text and icon) and
 * *Not stocked* when the medicine has no linked SKUs: it is still prescribable, but the vet is told it
 * cannot be dispensed here. `exclude` keeps a medicine out of an interaction row's own picker.
 */
export function FormularyPicker({
  id,
  label = 'Medicine',
  onSelect,
  exclude,
  error,
  hint,
  required,
  disabled,
  clearOnSelect = true,
}: {
  id: string;
  label?: string;
  onSelect: (medicine: FormularyMedicineSummary) => void;
  exclude?: string;
  error?: string;
  hint?: string;
  required?: boolean;
  disabled?: boolean;
  clearOnSelect?: boolean;
}) {
  const [term, setTerm] = React.useState('');
  const query = useFormularyList({ search: term, isActive: true, pageSize: 20 }, term.length >= 2);
  const items = (query.data?.items ?? []).filter((medicine) => medicine.id !== exclude);

  return (
    <ListboxPicker
      id={id}
      label={label}
      items={items}
      isLoading={query.isLoading}
      isError={query.isError}
      getKey={(medicine) => medicine.id}
      getLabel={(medicine) => `${medicine.name} — ${medicine.strengthText}`}
      renderDetail={(medicine) => (
        <>
          <span>{formLabel(medicine.form)}</span>
          <span>{medicine.code}</span>
          {medicine.isControlled && (
            <Badge variant="warning" icon={<ShieldAlert size={12} aria-hidden="true" />}>
              Controlled
            </Badge>
          )}
          {!medicine.isStocked && <Badge variant="neutral">Not stocked</Badge>}
        </>
      )}
      onSearch={setTerm}
      onSelect={onSelect}
      placeholder="Type a medicine name or code"
      noun="medicines"
      hint={hint}
      error={error}
      required={required}
      disabled={disabled}
      clearOnSelect={clearOnSelect}
    />
  );
}
