import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/common/EmptyState';
import { formatMoney } from '@/lib/format/money';
import type { BookableService } from '@/lib/api/booking';

/**
 * `components/booking/ServiceStep.tsx` — HO-APT-002 §11.1 step 2. Online-bookable, priced
 * services. The price is for information only — **"Pay at the clinic"** (ADR-073); there is no
 * payment control anywhere in the booking flow (U-A10). A `category` (from a reminder link)
 * pre-filters, case-insensitively, with a way to see everything.
 */
export function ServiceStep({
  services,
  category,
  onClearCategory,
  onSelect,
}: {
  services: BookableService[];
  category: string | null;
  onClearCategory: () => void;
  onSelect: (serviceId: string) => void;
}) {
  const wanted = category?.trim().toLowerCase() ?? '';
  const filtered = wanted
    ? services.filter((service) => service.category.toLowerCase() === wanted)
    : services;

  if (services.length === 0) {
    return (
      <EmptyState
        title="No services can be booked online right now."
        description="Please call the clinic to make an appointment."
      />
    );
  }

  return (
    <section aria-labelledby="service-step-heading" className="flex flex-col gap-3">
      <h2 id="service-step-heading" className="text-sm font-semibold text-ink">
        {wanted && filtered.length > 0 ? `${category} services` : 'Choose a service'}
      </h2>
      {wanted && filtered.length === 0 && (
        <p role="status" className="text-sm text-ink-muted">
          No {category?.toLowerCase()} service can be booked online, so here is everything we offer.
        </p>
      )}
      {wanted && filtered.length > 0 && filtered.length < services.length && (
        <p className="text-sm text-ink-muted">
          Showing {category?.toLowerCase()} services only.{' '}
          <button
            type="button"
            onClick={onClearCategory}
            className="font-semibold text-accent-ink underline underline-offset-2 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          >
            Show all services
          </button>
        </p>
      )}
      <ul className="m-0 flex list-none flex-col gap-3 p-0">
        {(filtered.length > 0 ? filtered : services).map((service) => (
          <li
            key={service.id}
            className="flex flex-wrap items-center justify-between gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-4"
          >
            <div className="min-w-0 flex-1">
              <p className="text-[15px] font-semibold text-ink">{service.name}</p>
              {service.description && (
                <p className="mt-0.5 text-sm text-ink-muted">{service.description}</p>
              )}
              <p className="mt-1 text-sm text-ink-muted">
                {service.durationMinutes} min · {formatMoney(service.currentPrice)} ·{' '}
                <span className="font-semibold text-ink">Pay at the clinic</span>
              </p>
            </div>
            <Button aria-label={`Choose ${service.name}`} onClick={() => onSelect(service.id)}>
              Choose
            </Button>
          </li>
        ))}
      </ul>
    </section>
  );
}
