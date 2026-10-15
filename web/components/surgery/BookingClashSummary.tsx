'use client';

import { forwardRef } from 'react';
import { AlertTriangle } from 'lucide-react';

/**
 * `BookingClashSummary` — HO-SRG-002 §11.1 part 5, US-SRG-01. Every clash the server returned, listed at
 * the top; focus moves here on a refusal so a keyboard or screen-reader user hears all of them at once.
 * Each clash is ALSO shown beside its own field by the picker.
 */
export const BookingClashSummary = forwardRef<HTMLDivElement, { messages: string[] }>(
  function BookingClashSummary({ messages }, ref) {
    if (messages.length === 0) return null;
    return (
      <div
        ref={ref}
        tabIndex={-1}
        role="alert"
        aria-labelledby="srg-clash-title"
        className="rounded-[var(--radius-md)] border border-danger-ink/30 bg-danger-bg p-3 text-sm text-danger-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-danger-ink"
      >
        <p id="srg-clash-title" className="flex items-center gap-2 font-semibold">
          <AlertTriangle size={16} aria-hidden="true" />
          {messages.length === 1
            ? 'This surgery can’t be booked yet.'
            : `These ${messages.length} things need fixing.`}
        </p>
        <ul className="mt-1 list-disc pl-6">
          {messages.map((message) => (
            <li key={message}>{message}</li>
          ))}
        </ul>
      </div>
    );
  },
);
