'use client';

import type { ReactNode } from 'react';
import { Tabs, type TabSpec } from '@/components/billing/Tabs';
import { useNarrowContainer } from '@/lib/hooks/useIsNarrow';
import { inputClass, labelClass } from './styles';

/**
 * `SurgeryTabs` — HO-SRG-002 §8.1, UI-1. The WAI-ARIA tab set from the billing area on a wide page and a
 * labelled `<select>` on a phone (the tabs become a select at 360 px). Both are driven by the same
 * `selected` id, which the page keeps in the URL hash.
 */
export function SurgeryTabs({
  tabs,
  selected,
  onSelect,
}: {
  tabs: TabSpec[];
  selected: string;
  onSelect: (id: string) => void;
}) {
  const [ref, narrow] = useNarrowContainer<HTMLDivElement>(560);
  const active = tabs.find((tab) => tab.id === selected) ?? tabs[0]!;
  let body: ReactNode;
  if (narrow) {
    body = (
      <div className="flex flex-col gap-4">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="surgery-section" className={labelClass}>
            Section
          </label>
          <select
            id="surgery-section"
            className={inputClass}
            value={active.id}
            onChange={(event) => onSelect(event.target.value)}
          >
            {tabs.map((tab) => (
              <option key={tab.id} value={tab.id}>
                {tab.label}
              </option>
            ))}
          </select>
        </div>
        <div role="tabpanel" aria-label={active.label}>
          {active.panel}
        </div>
      </div>
    );
  } else {
    body = (
      <Tabs
        label="Surgery sections"
        tabs={tabs}
        selected={active.id}
        onSelect={onSelect}
        idPrefix="surgery"
      />
    );
  }
  return <div ref={ref}>{body}</div>;
}
