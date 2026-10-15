'use client';

import { MoneyField, SelectField, TextField } from '@/components/billing/Fields';
import { Section } from '@/components/billing/PageHeader';
import { Button } from '@/components/ui/button';
import type { DeliveryZoneInput } from '@/lib/api/ordering-admin';
import { serviceLevelLabel } from '@/lib/format/orders';

/**
 * `components/ordering-admin/ZoneServiceEditor.tsx` — HO-ORD-002 §11.12, US-ORD-06. One service level of
 * a zone (Standard or Express): active, base charge, free-over order value, lead time, **weight bands**
 * (rows of *up to N g → + charge*; the last band can be open-ended) and **slot windows** per weekday.
 * All money is typed text; the server validates the combination and its message is shown by the form.
 */
export type ZoneService = DeliveryZoneInput['services'][number];
const DAYS = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];

export function ZoneServiceEditor({ service, onChange }: { service: ZoneService; onChange: (next: ZoneService) => void }) {
  const level = service.serviceLevel;
  const patch = (change: Partial<ZoneService>) => onChange({ ...service, ...change });
  return (
    <Section title={serviceLevelLabel(level)} headingId={`service-${level}`}>
      <label className="flex items-center gap-2 text-sm">
        <input type="checkbox" className="h-4 w-4 accent-[var(--accent)]" checked={service.isActive} onChange={(e) => patch({ isActive: e.target.checked })} />
        Offer {serviceLevelLabel(level).toLowerCase()}
      </label>
      <div className="grid gap-3 sm:grid-cols-3">
        <MoneyField id={`${level}-base`} label="Base charge" value={service.baseCharge} onChange={(e) => patch({ baseCharge: e.target.value })} />
        <MoneyField id={`${level}-free`} label="Free over" hint="Optional." value={service.freeOverOrderValue} onChange={(e) => patch({ freeOverOrderValue: e.target.value })} />
        <TextField id={`${level}-lead`} label="Lead time (days)" type="number" min={0} value={service.leadTimeDays} onChange={(e) => patch({ leadTimeDays: Number.parseInt(e.target.value, 10) || 0 })} />
      </div>
      <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
        <legend className="mb-1 text-sm font-semibold">Weight bands</legend>
        {service.weightBands.map((band, index) => (
          <div key={index} className="flex flex-wrap items-end gap-2">
            <TextField id={`${level}-band-${index}-g`} label={`Band ${index + 1}: up to (grams)`} type="number" min={1} hint="Empty means no upper limit." value={band.upToGrams ?? ''} onChange={(e) => patch({ weightBands: service.weightBands.map((b, i) => (i === index ? { ...b, upToGrams: e.target.value === '' ? null : Number.parseInt(e.target.value, 10) } : b)) })} />
            <MoneyField id={`${level}-band-${index}-s`} label={`Band ${index + 1}: extra charge`} value={band.surcharge} onChange={(e) => patch({ weightBands: service.weightBands.map((b, i) => (i === index ? { ...b, surcharge: e.target.value } : b)) })} />
            <Button variant="ghost" size="sm" onClick={() => patch({ weightBands: service.weightBands.filter((_, i) => i !== index) })}>Remove band {index + 1}</Button>
          </div>
        ))}
        <div><Button variant="secondary" size="sm" onClick={() => patch({ weightBands: [...service.weightBands, { upToGrams: null, surcharge: '0.00' }] })}>Add a weight band</Button></div>
      </fieldset>
      <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
        <legend className="mb-1 text-sm font-semibold">Delivery windows (branch time)</legend>
        {service.slotWindows.map((slot, index) => (
          <div key={index} className="flex flex-wrap items-end gap-2">
            <SelectField id={`${level}-slot-${index}-d`} label={`Window ${index + 1}: day`} value={slot.dayOfWeek} onChange={(e) => patch({ slotWindows: service.slotWindows.map((s, i) => (i === index ? { ...s, dayOfWeek: e.target.value } : s)) })}>
              {DAYS.map((d) => <option key={d} value={d}>{d}</option>)}
            </SelectField>
            <TextField id={`${level}-slot-${index}-s`} label={`Window ${index + 1}: from`} type="time" value={slot.start} onChange={(e) => patch({ slotWindows: service.slotWindows.map((s, i) => (i === index ? { ...s, start: e.target.value } : s)) })} />
            <TextField id={`${level}-slot-${index}-e`} label={`Window ${index + 1}: to`} type="time" value={slot.end} onChange={(e) => patch({ slotWindows: service.slotWindows.map((s, i) => (i === index ? { ...s, end: e.target.value } : s)) })} />
            <Button variant="ghost" size="sm" onClick={() => patch({ slotWindows: service.slotWindows.filter((_, i) => i !== index) })}>Remove window {index + 1}</Button>
          </div>
        ))}
        <div><Button variant="secondary" size="sm" onClick={() => patch({ slotWindows: [...service.slotWindows, { dayOfWeek: 'Monday', start: '10:00', end: '13:00' }] })}>Add a delivery window</Button></div>
      </fieldset>
    </Section>
  );
}
