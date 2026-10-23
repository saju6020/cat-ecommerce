import { House, Package, ShoppingBag, Store, Truck } from 'lucide-react';
import { OrderTracker, type TrackerStep } from '@/components/hearth/OrderTracker';

/**
 * The white card around the five-node tracker (docs/design/orders/OrderDetail.dc.html `.trkcard`).
 * Renders nothing for an order that has no tracker (a cancelled order). The node statuses come from
 * `orderTrackerSteps`, which only reads the order's server-side status and history.
 */
export function OrderTrackerCard({ steps }: { steps: TrackerStep[] | null }) {
  if (!steps) return null;
  const pickup = steps[3]?.label === 'Ready to collect';
  const icons = [
    <ShoppingBag key="p" size={20} />,
    <Package key="c" size={20} />,
    <Package key="k" size={20} />,
    pickup ? <Store key="d" size={20} /> : <Truck key="d" size={20} />,
    <House key="h" size={20} />,
  ];
  const withIcons = steps.map((step, index) => ({ ...step, icon: icons[index] }));
  return (
    <section
      aria-label="Order progress"
      className="rounded-3xl border border-border bg-surface px-4 py-6 sm:px-8 sm:py-7"
    >
      <OrderTracker steps={withIcons} label="Order progress steps" />
    </section>
  );
}
