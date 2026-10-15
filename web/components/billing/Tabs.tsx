'use client';

import { useRef, type KeyboardEvent, type ReactNode } from 'react';
import { cn } from '@/lib/utils';

/**
 * `components/billing/Tabs.tsx` — a small accessible tab set (WAI-ARIA tabs pattern: arrow keys and
 * Home/End move between tabs, only the selected tab is in the tab order, the panel is labelled by
 * its tab). The selected tab lives in the caller's state (the account screen keeps it in the URL).
 */
export interface TabSpec {
  id: string;
  label: string;
  panel: ReactNode;
}

export function Tabs({
  label,
  tabs,
  selected,
  onSelect,
  idPrefix = 'tabs',
}: {
  label: string;
  tabs: TabSpec[];
  selected: string;
  onSelect: (id: string) => void;
  idPrefix?: string;
}) {
  const refs = useRef<Array<HTMLButtonElement | null>>([]);
  const index = Math.max(0, tabs.findIndex((tab) => tab.id === selected));

  function onKeyDown(event: KeyboardEvent<HTMLButtonElement>, current: number) {
    let next = current;
    if (event.key === 'ArrowRight') next = (current + 1) % tabs.length;
    else if (event.key === 'ArrowLeft') next = (current - 1 + tabs.length) % tabs.length;
    else if (event.key === 'Home') next = 0;
    else if (event.key === 'End') next = tabs.length - 1;
    else return;
    event.preventDefault();
    onSelect(tabs[next]!.id);
    refs.current[next]?.focus();
  }

  const active = tabs[index]!;
  return (
    <div className="flex min-w-0 flex-col gap-3">
      <div role="tablist" aria-label={label} className="flex flex-wrap gap-1 border-b border-border">
        {tabs.map((tab, position) => (
          <button
            key={tab.id}
            ref={(node) => {
              refs.current[position] = node;
            }}
            type="button"
            role="tab"
            id={`${idPrefix}-tab-${tab.id}`}
            aria-selected={position === index}
            aria-controls={`${idPrefix}-panel-${tab.id}`}
            tabIndex={position === index ? 0 : -1}
            onClick={() => onSelect(tab.id)}
            onKeyDown={(event) => onKeyDown(event, position)}
            className={cn(
              '-mb-px rounded-t-[var(--radius-sm)] border-b-2 px-3 py-2 text-sm font-semibold',
              position === index
                ? 'border-accent text-accent-ink'
                : 'border-transparent text-ink-muted hover:text-ink',
            )}
          >
            {tab.label}
          </button>
        ))}
      </div>
      <div
        role="tabpanel"
        id={`${idPrefix}-panel-${active.id}`}
        aria-labelledby={`${idPrefix}-tab-${active.id}`}
        tabIndex={0}
        className="min-w-0"
      >
        {active.panel}
      </div>
    </div>
  );
}
