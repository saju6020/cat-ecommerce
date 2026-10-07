'use client';

import { useRef } from 'react';
import { CHANNEL_LABELS } from '@/lib/validation/templateContent';
import { cn } from '@/lib/utils';
import type { TemplateChannel } from '@/lib/api/admin-notification-templates';

export function tabId(prefix: string, channel: TemplateChannel) {
  return `${prefix}-tab-${channel}`;
}
export function panelId(prefix: string, channel: TemplateChannel) {
  return `${prefix}-panel-${channel}`;
}

/**
 * `components/templates/ChannelTabs.tsx` — HO-NTF-002 §11.3, UI-6: the ARIA tabs pattern. A
 * `tablist` of `tab` buttons (`aria-selected`, `aria-controls`, roving `tabIndex`), arrow keys /
 * Home / End move between tabs, and every panel is rendered (inactive ones `hidden`) so the form
 * fields inside all of them stay registered and keep their values (NFR-USA-5).
 */
export function ChannelTabs({
  idPrefix,
  channels,
  active,
  onChange,
  renderPanel,
  tabBadges = {},
}: {
  idPrefix: string;
  channels: TemplateChannel[];
  active: TemplateChannel;
  onChange: (channel: TemplateChannel) => void;
  renderPanel: (channel: TemplateChannel) => React.ReactNode;
  /** Optional per-tab marker text, e.g. "Needs attention" when that tab holds an error. */
  tabBadges?: Partial<Record<TemplateChannel, string>>;
}) {
  const refs = useRef<Partial<Record<TemplateChannel, HTMLButtonElement | null>>>({});

  function move(to: number) {
    const next = channels[(to + channels.length) % channels.length]!;
    onChange(next);
    refs.current[next]?.focus();
  }

  function onKeyDown(event: React.KeyboardEvent, index: number) {
    if (event.key === 'ArrowRight') move(index + 1);
    else if (event.key === 'ArrowLeft') move(index - 1);
    else if (event.key === 'Home') move(0);
    else if (event.key === 'End') move(channels.length - 1);
    else return;
    event.preventDefault();
  }

  return (
    <div className="flex flex-col gap-3">
      <div
        role="tablist"
        aria-label="Message channels"
        className="flex flex-wrap gap-1 border-b border-border"
      >
        {channels.map((channel, index) => {
          const selected = channel === active;
          return (
            <button
              key={channel}
              ref={(element) => {
                refs.current[channel] = element;
              }}
              type="button"
              role="tab"
              id={tabId(idPrefix, channel)}
              aria-selected={selected}
              aria-controls={panelId(idPrefix, channel)}
              tabIndex={selected ? 0 : -1}
              onClick={() => onChange(channel)}
              onKeyDown={(event) => onKeyDown(event, index)}
              className={cn(
                '-mb-px rounded-t-[var(--radius-sm)] border border-b-0 px-4 py-2 text-sm font-semibold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent',
                selected
                  ? 'border-border bg-surface text-accent-ink'
                  : 'border-transparent text-ink-muted hover:bg-surface-sunken',
              )}
            >
              {CHANNEL_LABELS[channel]}
              {tabBadges[channel] && (
                <span className="ml-2 rounded-full bg-danger-bg px-2 py-0.5 text-[11px] font-bold text-danger-ink">
                  {tabBadges[channel]}
                </span>
              )}
            </button>
          );
        })}
      </div>
      {channels.map((channel) => (
        <div
          key={channel}
          role="tabpanel"
          id={panelId(idPrefix, channel)}
          aria-labelledby={tabId(idPrefix, channel)}
          hidden={channel !== active}
        >
          {renderPanel(channel)}
        </div>
      ))}
    </div>
  );
}
