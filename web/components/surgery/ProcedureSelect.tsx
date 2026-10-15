'use client';

import { Badge } from '@/components/ui/badge';
import { Scissors } from 'lucide-react';
import { formatMoney } from '@/lib/format/money';
import { formatMinutes } from '@/lib/format/surgery';
import type { SurgeryService } from '@/lib/api/surgeries';
import { errorClass, hintClass, inputClass, labelClass } from './styles';

/**
 * `ProcedureSelect` — HO-SRG-002 §11.1 part 2. The server's theatre services: name, duration, the price
 * shown *"for information — paid at the clinic"* (there is no payment step and no deposit, ADR-170) and a
 * sterilisation badge. A sex-mismatch answer lands under this field.
 */
export function ProcedureSelect({
  services,
  serviceId,
  onChange,
  error,
  disabled,
}: {
  services: SurgeryService[];
  serviceId: string;
  onChange: (serviceId: string) => void;
  error?: string;
  disabled?: boolean;
}) {
  const chosen = services.find((service) => service.id === serviceId);
  return (
    <section aria-labelledby="srg-step-procedure" className="flex flex-col gap-3">
      <h2 id="srg-step-procedure" className="font-display text-lg font-semibold text-ink">
        2. Procedure
      </h2>
      <div className="flex flex-col gap-1.5">
        <label htmlFor="surgery-procedure" className={labelClass}>
          Procedure
        </label>
        <select
          id="surgery-procedure"
          className={inputClass}
          disabled={disabled}
          value={serviceId}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? 'surgery-procedure-error' : undefined}
          onChange={(event) => onChange(event.target.value)}
        >
          <option value="">Choose…</option>
          {services.map((service) => (
            <option key={service.id} value={service.id}>
              {service.name}
            </option>
          ))}
        </select>
        {error && (
          <p id="surgery-procedure-error" role="alert" className={errorClass}>
            {error}
          </p>
        )}
      </div>
      {chosen && (
        <p className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-ink">
          <span>{formatMinutes(chosen.durationMinutes)}</span>
          <span>
            <strong>{formatMoney(chosen.price)}</strong>{' '}
            <span className={hintClass}>for information — paid at the clinic</span>
          </span>
          {chosen.isSterilisation && (
            <Badge variant="success" icon={<Scissors size={12} aria-hidden="true" />}>
              Sterilisation
            </Badge>
          )}
        </p>
      )}
    </section>
  );
}
