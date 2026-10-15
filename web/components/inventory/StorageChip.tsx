import { Thermometer } from 'lucide-react';
import type { Storage } from '@/lib/api/inventory-lookup';
import { describeStorage } from './labels';

/** `components/inventory/StorageChip.tsx` — US-IMP-16 AC1: how the product must be kept, as text
 *  (*"Refrigerated 2–8 °C"*). Renders nothing when no profile exists. */
export function StorageChip({ storage }: { storage: Storage | null | undefined }) {
  const label = describeStorage(storage);
  if (!label) return null;
  return (
    <span className="inline-flex max-w-full items-center gap-1 rounded-[var(--radius-sm)] bg-accent-soft px-2 py-0.5 text-xs font-semibold text-accent-ink">
      <Thermometer size={12} aria-hidden="true" className="flex-none" />
      <span className="min-w-0 break-words">{label}</span>
    </span>
  );
}
